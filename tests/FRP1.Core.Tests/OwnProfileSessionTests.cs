using FRP1.Core.Persistence;
using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.RpBar;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class OwnProfileSessionTests
{
    [Fact]
    public async Task ExistingProfilesAreNeverAutomaticallyActivated()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        Assert.Single(harness.Session.Profiles);
        Assert.Null(harness.Session.ActiveProfile);
        Assert.Empty(harness.Session.Settings.ActiveProfiles);
        Assert.False(File.Exists(Path.Combine(harness.Directory.Path, "rp-bar.json")));
    }

    [Fact]
    public async Task ExplicitSelectionPersistsSeparatelyForEachCharacter()
    {
        var first = RichProfile();
        var second = RichProfile() with { DisplayName = "Second local profile" };
        await using var harness = await OwnProfileHarness.CreateAsync([first, second]);
        await harness.Session.SelectActiveAsync(first.Id);
        await harness.ReadyAsync();
        Assert.Equal(first.Id, harness.Session.ActiveProfile!.Id);
        harness.Session.ObserveContext(2, true);
        await harness.ReadyAsync();
        Assert.Null(harness.Session.ActiveProfile);
        await harness.Session.SelectActiveAsync(second.Id);
        await harness.ReadyAsync();
        Assert.Equal(second.Id, harness.Session.ActiveProfile!.Id);
        harness.Session.ObserveContext(1, true);
        await harness.ReadyAsync();
        Assert.Equal(first.Id, harness.Session.ActiveProfile!.Id);
        var reloaded = await new RpBarSettingsStore(harness.Directory.Path).LoadAsync();
        Assert.Equal(first.Id, reloaded.ActiveProfiles[1]);
        Assert.Equal(second.Id, reloaded.ActiveProfiles[2]);
        var snapshot = harness.Session.ActiveProfile!;
        snapshot.Glances.Clear();
        Assert.Single(harness.Session.ActiveProfile!.Glances);
    }

    [Fact]
    public async Task AccessGateHidesWholeObjectsAndPolicyDowngradeLeavesOnlyGeneralProfiles()
    {
        var general = RichProfile();
        var mature = RichProfile() with { ContentRating = ContentRating.Mature18, DisplayName = "Synthetic restricted profile" };
        await using var harness = await OwnProfileHarness.CreateAsync([general, mature]);
        await harness.Session.SelectActiveAsync(mature.Id);
        await harness.ReadyAsync();
        Assert.Equal(mature.Id, harness.Session.ActiveProfile!.Id);
        harness.Session.ObserveContext(1, false);
        Assert.Empty(harness.Session.Profiles);
        Assert.Null(harness.Session.ActiveProfile);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.UpdateStatusAsync(RpStatus.Ic));
        harness.Session.ObserveContext(1, true);
        await harness.ReadyAsync();
        harness.Policy.SetAudience(new AudiencePreferences(AudienceSelection.Under18, false));
        Assert.Null(harness.Session.ActiveProfile);
        Assert.DoesNotContain(harness.Session.Profiles, profile => profile.Id == mature.Id);
        await harness.ReadyAsync();
        Assert.Equal(general.Id, Assert.Single(harness.Session.Profiles).Id);
        harness.Policy.SetAudience(new AudiencePreferences());
        Assert.False(harness.Session.AccessReady);
        Assert.Empty(harness.Session.Profiles);
        Assert.Null(harness.Session.ActiveProfile);
    }

    [Fact]
    public async Task EditorCreatesOnlyNewIdentityAndAssignsRevisionInsteadOfTrustingDraft()
    {
        await using var harness = await OwnProfileHarness.CreateAsync([]);
        var draft = RichProfile() with { Revision = 999 };
        var created = await harness.Session.SaveEditorAsync(draft, 0, null);
        await harness.ReadyAsync();
        Assert.Equal(1, created.Revision);
        Assert.Equal(1, (await harness.Store.LoadLocalAsync(draft.Id))!.Revision);
        Assert.Null(harness.Session.ActiveProfile);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.SaveEditorAsync(draft, 0, null));
        await harness.ReadyAsync();
        var edited = await harness.Session.SaveEditorAsync(created with { Revision = 700, Title = "Saved" }, 1, ProfileJson.Hash(created));
        await harness.ReadyAsync();
        Assert.Equal(2, edited.Revision);
        Assert.Equal("Saved", edited.Title);
        await Assert.ThrowsAsync<ArgumentException>(() => harness.Session.SaveEditorAsync(edited, 2, "invalid-hash"));
    }

    [Fact]
    public async Task StaleEditorRevisionAndHashCannotOverwriteNewerDiskContent()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        var externallyChanged = profile with { Revision = 2, Title = "External revision" };
        await harness.Store.SaveLocalAsync(externallyChanged);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.SaveEditorAsync(profile with { Title = "Stale editor" }, profile.Revision, ProfileJson.Hash(profile)));
        await harness.ReadyAsync();
        Assert.Equal(ProfileJson.Hash(externallyChanged), ProfileJson.Hash((await harness.Store.LoadLocalAsync(profile.Id))!));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.SaveEditorAsync(externallyChanged, 2, ProfileJson.Hash(externallyChanged with { Title = "Wrong content" })));
    }

    [Fact]
    public async Task ConcurrentEditorSavesSerializeAndRejectTheStaleCompetitor()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        var hash = ProfileJson.Hash(profile);
        var first = Record.ExceptionAsync(async () => await harness.Session.SaveEditorAsync(profile with { Title = "Choice A" }, 1, hash));
        var second = Record.ExceptionAsync(async () => await harness.Session.SaveEditorAsync(profile with { Title = "Choice B" }, 1, hash));
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, result => result is null);
        Assert.IsType<InvalidOperationException>(Assert.Single(results, result => result is not null));
        await harness.ReadyAsync();
        var stored = (await harness.Store.LoadLocalAsync(profile.Id))!;
        Assert.Equal(2, stored.Revision);
        Assert.Contains(stored.Title, new[] { "Choice A", "Choice B" });
    }

    [Fact]
    public async Task StatusAndAvailabilitySaveExactlyOneFieldAndNoopDoesNotRewrite()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        await harness.Session.SelectActiveAsync(profile.Id);
        await harness.ReadyAsync();
        var updated = await harness.Session.UpdateStatusAsync(RpStatus.Ic);
        await harness.ReadyAsync();
        Assert.Equal(ProfileJson.Hash(profile with { Revision = 2, Status = RpStatus.Ic }), ProfileJson.Hash(updated));
        var availability = await harness.Session.UpdateAvailabilityAsync(RpAvailability.Busy);
        await harness.ReadyAsync();
        Assert.Equal(ProfileJson.Hash(updated with { Revision = 3, Availability = RpAvailability.Busy }), ProfileJson.Hash(availability));
        var path = Path.Combine(harness.Directory.Path, "profiles", profile.Id.ToString("N") + ".json");
        var before = File.GetLastWriteTimeUtc(path);
        var noop = await harness.Session.UpdateStatusAsync(RpStatus.Ic);
        Assert.Equal(3, noop.Revision);
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
        Assert.False(harness.Session.IsBusy);
    }

    [Fact]
    public async Task DirtyEditorBlocksBarMutationsButStillAllowsExplicitEditorSave()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        await harness.Session.SelectActiveAsync(profile.Id);
        await harness.ReadyAsync();
        harness.Session.SetEditorBlocked(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.UpdateStatusAsync(RpStatus.Ic));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.UpdateAvailabilityAsync(RpAvailability.Open));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.SelectActiveAsync(profile.Id));
        Assert.Equal(ProfileJson.Hash(profile), ProfileJson.Hash((await harness.Store.LoadLocalAsync(profile.Id))!));
        var saved = await harness.Session.SaveEditorAsync(profile with { Summary = "Explicit editor save" }, 1, ProfileJson.Hash(profile));
        await harness.ReadyAsync();
        Assert.Equal(2, saved.Revision);
        Assert.Equal("Explicit editor save", harness.Session.ActiveProfile!.Summary);
    }

    [Fact]
    public async Task BusyBarRejectsSecondMutationAndSubsequentMutationPreservesFirst()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        await harness.Session.SelectActiveAsync(profile.Id);
        await harness.ReadyAsync();
        var first = harness.Session.UpdateStatusAsync(RpStatus.Ic);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Session.UpdateAvailabilityAsync(RpAvailability.Open));
        await first;
        await harness.ReadyAsync();
        await harness.Session.UpdateAvailabilityAsync(RpAvailability.Open);
        await harness.ReadyAsync();
        var stored = (await harness.Store.LoadLocalAsync(profile.Id))!;
        Assert.Equal(RpStatus.Ic, stored.Status);
        Assert.Equal(RpAvailability.Open, stored.Availability);
        Assert.Equal(3, stored.Revision);
    }

    [Fact]
    public async Task ChangingCharacterOrClosingAccessDuringSelectionCannotActivateWrongContext()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        var selection = harness.Session.SelectActiveAsync(profile.Id);
        harness.Session.ObserveContext(2, true);
        await selection;
        await harness.ReadyAsync();
        Assert.Null(harness.Session.ActiveProfile);
        Assert.False(harness.Session.Settings.ActiveProfiles.ContainsKey(2));
        harness.Session.ObserveContext(1, false);
        Assert.Null(harness.Session.ActiveProfile);
        Assert.Empty(harness.Session.Profiles);
    }

    [Fact]
    public async Task FailedSettingsLoadAndProfileWriteDoNotExposePhantomSelectionAndCanRetry()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        var settingsPath = Path.Combine(harness.Directory.Path, "rp-bar.json");
        await File.WriteAllTextAsync(settingsPath, "broken");
        using var session = new OwnProfileSession(harness.Store, harness.SettingsStore);
        session.ObserveContext(1, true);
        await OwnProfileHarness.PumpUntilAsync(session, () => !session.IsLoading);
        Assert.Null(session.ActiveProfile);
        Assert.False(string.IsNullOrWhiteSpace(session.ErrorMessage));
        await harness.SettingsStore.SaveAsync(new RpBarPreferences());
        session.Retry();
        await OwnProfileHarness.PumpUntilAsync(session, () => !session.IsLoading);
        Assert.Null(session.ActiveProfile);
        Assert.Null(session.ErrorMessage);
        await session.SelectActiveAsync(profile.Id);
        await OwnProfileHarness.PumpUntilAsync(session, () => !session.IsLoading && !session.IsBusy);
        var profilePath = Path.Combine(harness.Directory.Path, "profiles", profile.Id.ToString("N") + ".json");
        using (var fileLock = new FileStream(profilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => session.UpdateStatusAsync(RpStatus.Ic));
            await OwnProfileHarness.PumpUntilAsync(session, () => !session.IsBusy);
            Assert.Equal(1, session.ActiveProfile!.Revision);
        }
        session.Retry();
        await OwnProfileHarness.PumpUntilAsync(session, () => !session.IsLoading);
        Assert.Equal(RpStatus.Ooc, session.ActiveProfile!.Status);
        session.Dispose();
        await session.DisposalCompletion;
    }

    [Fact]
    public async Task CorruptProfileCatalogIsHiddenAndRetryCanRestoreRepairedProfile()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        await harness.Session.SelectActiveAsync(profile.Id);
        await harness.ReadyAsync();
        await File.WriteAllTextAsync(Path.Combine(harness.Directory.Path, "profiles", profile.Id.ToString("N") + ".json"), "broken");
        harness.Session.Reload();
        Assert.Null(harness.Session.ActiveProfile);
        await harness.ReadyAsync();
        Assert.Empty(harness.Session.Profiles);
        Assert.Null(harness.Session.ActiveProfile);
        Assert.False(string.IsNullOrWhiteSpace(harness.Session.ErrorMessage));
        await harness.Store.SaveLocalAsync(profile);
        harness.Session.Retry();
        await harness.ReadyAsync();
        Assert.Equal(profile.Id, harness.Session.ActiveProfile!.Id);
        Assert.Null(harness.Session.ErrorMessage);
    }

    [Fact]
    public async Task LayoutUpdatesCannotEraseExplicitAssociationsAndGlobalHintsPersistWithHiddenBar()
    {
        var profile = RichProfile();
        await using var harness = await OwnProfileHarness.CreateAsync([profile]);
        var staleLayout = harness.Session.Settings;
        await harness.Session.SelectActiveAsync(profile.Id);
        await harness.ReadyAsync();
        harness.Session.SetPreferences(staleLayout with { Visible = false, ShowHints = false, ActiveProfiles = new Dictionary<ulong, Guid>() });
        Assert.False(harness.Session.Settings.ShowHints);
        Assert.Equal(profile.Id, harness.Session.Settings.ActiveProfiles[1]);
        await OwnProfileHarness.PumpUntilAsync(harness.Session, () => File.Exists(Path.Combine(harness.Directory.Path, "rp-bar.json")) &&
            !harness.Session.IsLoading && !harness.Session.IsBusy && !harness.SettingsStore.LoadAsync().GetAwaiter().GetResult().ShowHints);
        var stored = await harness.SettingsStore.LoadAsync();
        Assert.False(stored.Visible);
        Assert.False(stored.ShowHints);
        Assert.Equal(profile.Id, stored.ActiveProfiles[1]);
    }

    [Fact]
    public async Task ExplicitLayoutChoiceDuringSettingsRetryCannotBeReplacedByOlderDiskResult()
    {
        using var directory = new TestDirectory();
        var settingsStore = new RpBarSettingsStore(directory.Path);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "rp-bar.json"), "broken");
        var profileStore = new ProfileStore(directory.Path, new AudiencePolicy(new AudiencePreferences(AudienceSelection.Adult, true)));
        var profile = RichProfile();
        await profileStore.SaveLocalAsync(profile);
        using var session = new OwnProfileSession(profileStore, settingsStore);
        session.ObserveContext(1, true);
        await OwnProfileHarness.PumpUntilAsync(session, () => !session.IsLoading);
        Assert.True(session.SettingsLoaded);
        Assert.False(string.IsNullOrWhiteSpace(session.ErrorMessage));

        await settingsStore.SaveAsync(new RpBarPreferences
        {
            ShowHints = true, Scale = 1f, ActiveProfiles = new Dictionary<ulong, Guid> { [1] = profile.Id },
        });
        session.Retry();
        // Poll owns result publication, so this choice precedes applying the retry result
        // even if the worker has already finished its read. No timing or file-lock trick is needed.
        session.SetPreferences(session.Settings with { ShowHints = false, Scale = 1.4f });
        Assert.False(session.Settings.ShowHints);
        await OwnProfileHarness.PumpUntilAsync(session, () => !session.IsLoading);
        Assert.False(session.Settings.ShowHints);
        Assert.Equal(1.4f, session.Settings.Scale);
        Assert.Equal(profile.Id, session.Settings.ActiveProfiles[1]);
        await OwnProfileHarness.PumpUntilAsync(session, () =>
            !settingsStore.LoadAsync().GetAwaiter().GetResult().ShowHints);
        var persisted = await settingsStore.LoadAsync();
        Assert.False(persisted.ShowHints);
        Assert.Equal(1.4f, persisted.Scale);
        Assert.Equal(profile.Id, persisted.ActiveProfiles[1]);
        session.Dispose();
        await session.DisposalCompletion;
    }

    private static ProfileDocument RichProfile() => ProfileTests.Valid() with
    {
        Summary = "Synthetic summary", Pronouns = "they/them", OocInformation = "Synthetic OOC text",
        Glances = [new("Glance", "Synthetic description", 123)], RpHooks = [new("Hook", "Synthetic RP idea")],
        ExtraFields = new() { ["Custom"] = "Synthetic value" },
    };
}

internal sealed class OwnProfileHarness : IAsyncDisposable
{
    public TestDirectory Directory { get; } = new();
    public AudiencePolicy Policy { get; } = new(new AudiencePreferences(AudienceSelection.Adult, true));
    public ProfileStore Store { get; }
    public RpBarSettingsStore SettingsStore { get; }
    public OwnProfileSession Session { get; private set; } = null!;
    private OwnProfileHarness()
    {
        Store = new ProfileStore(Directory.Path, Policy);
        SettingsStore = new RpBarSettingsStore(Directory.Path);
    }
    public static async Task<OwnProfileHarness> CreateAsync(IReadOnlyList<ProfileDocument> profiles)
    {
        var harness = new OwnProfileHarness();
        foreach (var profile in profiles) await harness.Store.SaveLocalAsync(profile);
        harness.Session = new OwnProfileSession(harness.Store, harness.SettingsStore);
        harness.Session.ObserveContext(1, true);
        await harness.ReadyAsync();
        return harness;
    }
    public Task ReadyAsync() => PumpUntilAsync(Session, () => Session.SettingsLoaded && !Session.IsLoading && !Session.IsBusy);
    public static async Task PumpUntilAsync(OwnProfileSession session, Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            session.Poll();
            if (condition()) return;
            await Task.Delay(10, timeout.Token);
        }
    }
    public async ValueTask DisposeAsync()
    {
        Session.Dispose();
        await Session.DisposalCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        Directory.Dispose();
    }
}
