using System.Text.Json.Nodes;
using FRP1.Core.Profiles;
using FRP1.Plugin.Automation;
using Xunit;

namespace FRP1.Core.Tests;

public sealed class AutomationTests
{
    [Fact]
    public void DisabledEngineAndDisabledRulesNeverQueueMutations()
    {
        var engine = new AutomationEngine();
        engine.SetPreferences(new() { Rules = [Rule(AutomationTrigger.Login)] });
        engine.ObserveContext(Ready());
        Assert.False(engine.TryDequeue(out _));
        engine.SetPreferences(new() { Enabled = true, Rules = [Rule(AutomationTrigger.CombatEntered) with { Enabled = false }] });
        engine.ObserveContext(Ready() with { InCombat = true });
        Assert.Equal(0, engine.PendingCount);
    }

    [Fact]
    public void LoginTriggersOnceAndCharacterChangeInvalidatesPreviouslyDequeuedIntent()
    {
        var engine = new AutomationEngine();
        engine.SetPreferences(new() { Enabled = true, Rules = [Rule(AutomationTrigger.Login)] });
        engine.ObserveContext(Ready());
        Assert.True(engine.TryDequeue(out var old));
        Assert.True(engine.IsCurrent(old));
        engine.ObserveContext(Ready());
        Assert.False(engine.TryDequeue(out _));
        engine.ObserveContext(Ready() with { ContentId = 2 });
        Assert.False(engine.IsCurrent(old));
        Assert.True(engine.TryDequeue(out var next));
        Assert.Equal(2ul, next!.ContentId);
        Assert.True(engine.IsCurrent(next));
    }

    [Theory]
    [InlineData("access")]
    [InlineData("logout")]
    [InlineData("zero-character")]
    [InlineData("rules-disabled")]
    [InlineData("rule-edited")]
    public void RevocationOrNewRuleGenerationInvalidatesCapturedJobs(string reason)
    {
        var engine = new AutomationEngine();
        var rule = Rule(AutomationTrigger.Login);
        var preferences = new AutomationPreferences { Enabled = true, Rules = [rule] };
        engine.SetPreferences(preferences);
        engine.ObserveContext(Ready());
        Assert.True(engine.TryDequeue(out var captured));
        switch (reason)
        {
            case "access": engine.ObserveContext(Ready() with { AccessReady = false }); break;
            case "logout": engine.ObserveContext(Ready() with { LoggedIn = false }); break;
            case "zero-character": engine.ObserveContext(Ready() with { ContentId = 0 }); break;
            case "rules-disabled": engine.SetPreferences(preferences with { Enabled = false }); break;
            case "rule-edited": engine.SetPreferences(preferences with { Rules = [rule with { Status = RpStatus.Afk }] }); break;
        }
        Assert.False(engine.IsCurrent(captured));
        Assert.Equal(0, engine.PendingCount);
    }

    [Fact]
    public void CombatDutyAndTerritoryTransitionsRespectFiltersAndRemainBounded()
    {
        var engine = new AutomationEngine();
        var rules = new[] { Rule(AutomationTrigger.CombatEntered), Rule(AutomationTrigger.CombatLeft),
            Rule(AutomationTrigger.DutyEntered), Rule(AutomationTrigger.DutyLeft), Rule(AutomationTrigger.TerritoryChanged) with { TerritoryId = 5 } };
        engine.SetPreferences(new() { Enabled = true, Rules = rules });
        engine.ObserveContext(Ready());
        Assert.Equal(0, engine.PendingCount);
        var context = Ready() with { InCombat = true, InDuty = true };
        engine.ObserveContext(context);
        var triggers = new List<AutomationTrigger>();
        while (engine.TryDequeue(out var intent)) triggers.Add(intent!.Rule.Trigger);
        Assert.Equal(new[] { AutomationTrigger.CombatEntered, AutomationTrigger.DutyEntered }, triggers);
        engine.ObserveContext(context with { TerritoryId = 4 });
        Assert.Equal(0, engine.PendingCount);
        engine.ObserveContext(context with { TerritoryId = 5 });
        Assert.True(engine.TryDequeue(out var territory));
        Assert.Equal(AutomationTrigger.TerritoryChanged, territory!.Rule.Trigger);
        Assert.False(engine.TryDequeue(out _));
    }

    [Fact]
    public void TemporaryOverlaySuppressionDoesNotGrantAccessOrChangeSavedVisibility()
    {
        var engine = new AutomationEngine();
        engine.SetPreferences(new() { HideOverlaysInCombat = true, HideOverlaysInDuty = true });
        engine.ObserveContext(Ready() with { InCombat = true });
        Assert.True(engine.OverlaysSuppressed);
        engine.ObserveContext(Ready());
        Assert.False(engine.OverlaysSuppressed);
        engine.ObserveContext(Ready() with { InDuty = true, AccessReady = false });
        Assert.False(engine.OverlaysSuppressed);
        Assert.Equal(0, engine.PendingCount);
    }

    [Fact]
    public async Task AutomationSettingsRoundtripDetachedRulesAndStartDisabled()
    {
        using var directory = new TestDirectory();
        var store = new AutomationSettingsStore(directory.Path);
        Assert.False((await store.LoadAsync()).Enabled);
        var rules = new List<AutomationRule> { Rule(AutomationTrigger.Login) with { Action = AutomationActionKind.SelectProfile, ProfileId = Guid.NewGuid() } };
        var preferences = new AutomationPreferences { Enabled = true, Rules = rules };
        var saving = store.SaveAsync(preferences);
        rules.Clear();
        await saving;
        var restored = await new AutomationSettingsStore(directory.Path).LoadAsync();
        Assert.True(restored.Enabled);
        Assert.Equal(AutomationActionKind.SelectProfile, Assert.Single(restored.Rules).Action);
        Assert.True(Assert.IsAssignableFrom<ICollection<AutomationRule>>(restored.Rules).IsReadOnly);
    }

    [Fact]
    public void InvalidOrUnboundedRulesAreRejectedBeforeTheyCanQueue()
    {
        var rule = Rule(AutomationTrigger.Login);
        var invalid = new[] { rule with { Id = Guid.Empty }, rule with { Name = "  " }, rule with { TerritoryId = 0 },
            rule with { Trigger = (AutomationTrigger)999 }, rule with { Action = (AutomationActionKind)999 },
            rule with { Action = AutomationActionKind.SelectProfile, ProfileId = null }, rule with { ProfileId = Guid.NewGuid() } };
        foreach (var entry in invalid)
            Assert.Throws<InvalidDataException>(() => AutomationPreferences.Snapshot(new() { Rules = [entry] }));
        Assert.Throws<InvalidDataException>(() => AutomationPreferences.Snapshot(new() { Rules = [rule, rule] }));
        Assert.Throws<InvalidDataException>(() => AutomationPreferences.Snapshot(new() { Rules = Enumerable.Range(0, 17).Select(_ => Rule(AutomationTrigger.Login)).ToArray() }));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1,\"preferences\":null}")]
    [InlineData("{\"schemaVersion\":1,\"preferences\":{\"enabled\":false,\"hideOverlaysInCombat\":false,\"hideOverlaysInDuty\":false,\"rules\":[]},\"unexpected\":true}")]
    public async Task CorruptAutomationSettingsFailClosed(string json)
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "automation.json"), json);
        await Assert.ThrowsAsync<InvalidDataException>(() => new AutomationSettingsStore(directory.Path).LoadAsync());
    }

    private static AutomationRule Rule(AutomationTrigger trigger) => new() { Name = "Synthetic rule", Trigger = trigger };
    private static AutomationContext Ready() => new(1, true, true, TerritoryId: 1);
}
