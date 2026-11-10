using FRP1.Core.Profiles;
using FRP1.Core.Safety;

namespace FRP1.Plugin.Profiles;

public sealed record ProfileField(string Key, string Value);
public sealed record ProfileSection(string Key, IReadOnlyList<RichBlock> Blocks);

public sealed record ProfileReadSnapshot
{
    public required Guid Id { get; init; }
    public required long Revision { get; init; }
    public required ContentRating ContentRating { get; init; }
    public required string DisplayName { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Pronouns { get; init; }
    public required RpStatus Status { get; init; }
    public required RpAvailability Availability { get; init; }
    public required string CurrentLocation { get; init; }
    public required string OocInformation { get; init; }
    public required ProfileAppearance Appearance { get; init; }
    public required IReadOnlyList<ProfileField> Fields { get; init; }
    public required IReadOnlyList<ProfileSection> Sections { get; init; }
    public required IReadOnlyList<GlanceEntry> Glances { get; init; }
    public required IReadOnlyList<RpHook> RpHooks { get; init; }
    public required IReadOnlyList<string> Languages { get; init; }
    public required IReadOnlyList<PersonalityTrait> PersonalityTraits { get; init; }
    public required RpPreferences Preferences { get; init; }
    public required IReadOnlyList<CompanionProfile> Companions { get; init; }
    public PortraitThumbnail? Portrait { get; init; }
}

// Create once per profile revision; every output rechecks the live audience policy.
public sealed class ProfileReader
{
    private readonly AudiencePolicy policy;
    private readonly ProfileDocument owned;
    private readonly ProfileReadSnapshot snapshot;

    public ProfileReader(ProfileDocument profile, AudiencePolicy policy, bool standardPresentation = false)
    {
        ArgumentNullException.ThrowIfNull(policy);
        this.policy = policy;
        EnsureAccess(profile);
        owned = ProfileJson.Import(ProfileJson.Export(profile));
        EnsureAccess(owned);
        var fields = new List<ProfileField>();
        void Field(string key, string value) { if (!string.IsNullOrWhiteSpace(value)) fields.Add(new(key, value)); }
        Field("FirstName", owned.FirstName); Field("LastName", owned.LastName);
        Field("Race", owned.Race); Field("Clan", owned.Clan); Field("Age", owned.Age);
        Field("Gender", owned.Gender); Field("Origin", owned.Origin); Field("Occupation", owned.Occupation);
        Field("EyeColor", owned.EyeColor); Field("Height", owned.Height); Field("Weight", owned.Weight);
        Field("Birthplace", owned.Birthplace); Field("Residence", owned.Residence); Field("FreeCompany", owned.FreeCompany);
        foreach (var extra in owned.ExtraFields) Field(extra.Key, extra.Value);
        var sections = new List<ProfileSection>();
        void Section(string key, string value)
        {
            if (value.Length == 0) return;
            var blocks = SafeRichText.Parse(value);
            if (standardPresentation)
                blocks = Array.AsReadOnly(blocks.Select(block => block with
                { Runs = Array.AsReadOnly(block.Runs.Select(run => run with { Color = "" }).ToArray()) }).ToArray());
            sections.Add(new(key, blocks));
        }
        Section("Appearance", owned.Appearance);
        Section("Personality", owned.Personality);
        Section("Background", owned.Background);
        snapshot = new ProfileReadSnapshot
        {
            Id = owned.Id, Revision = owned.Revision, ContentRating = owned.ContentRating,
            DisplayName = owned.DisplayName, Title = owned.Title, Summary = owned.Summary,
            Pronouns = owned.Pronouns, Status = owned.Status, Availability = owned.Availability,
            CurrentLocation = owned.CurrentLocation, OocInformation = owned.OocInformation,
            Appearance = standardPresentation ? owned.AppearanceStyle with
            { NameColor = "", AccentColor = "", Layout = AboutLayout.Sections } : owned.AppearanceStyle with { },
            Fields = Array.AsReadOnly(fields.ToArray()), Sections = Array.AsReadOnly(sections.ToArray()),
            Glances = Array.AsReadOnly(owned.Glances.Where(entry => entry.Enabled).Select(entry => entry with { }).ToArray()),
            RpHooks = Array.AsReadOnly(owned.RpHooks.Select(entry => entry with { }).ToArray()),
            Languages = Array.AsReadOnly(owned.RpLanguages.ToArray()),
            PersonalityTraits = Array.AsReadOnly(owned.PersonalityTraits.Select(entry => entry with { }).ToArray()),
            Preferences = owned.Preferences with { },
            Companions = Array.AsReadOnly(owned.Companions.Select(entry => entry with { }).ToArray()),
            Portrait = owned.Portrait is null ? null : owned.Portrait with { Data = (byte[])owned.Portrait.Data.Clone() },
        };
        EnsureAccess(owned);
    }

    public ProfileReadSnapshot Read()
    {
        EnsureAccess(owned);
        var result = snapshot with
        {
            Portrait = snapshot.Portrait is null ? null : snapshot.Portrait with
            { Data = (byte[])snapshot.Portrait.Data.Clone() },
        };
        EnsureAccess(owned);
        return result;
    }

    public bool TryRead(out ProfileReadSnapshot? result)
    {
        try { result = Read(); return true; }
        catch (InvalidDataException) { result = null; return false; }
    }

    private void EnsureAccess(ProfileDocument profile)
    {
        if (policy.Snapshot.Selection == AudienceSelection.Unspecified)
            throw new InvalidDataException("An audience selection is required to display profiles.");
        policy.EnsureAllowed(profile);
    }
}
