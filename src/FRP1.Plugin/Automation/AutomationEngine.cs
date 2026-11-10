namespace FRP1.Plugin.Automation;

public sealed record AutomationContext(ulong ContentId = 0, bool AccessReady = false, bool LoggedIn = false,
    bool InCombat = false, bool InDuty = false, uint TerritoryId = 0);

public sealed record AutomationIntent(long ContextVersion, long RulesVersion, ulong ContentId, AutomationRule Rule);

public sealed class AutomationEngine
{
    private readonly Queue<AutomationIntent> queue = new();
    private AutomationContext context = new();
    private AutomationPreferences preferences = new();
    private long contextVersion;
    private long rulesVersion;

    public bool OverlaysSuppressed => context.AccessReady && context.LoggedIn && context.ContentId != 0 &&
        ((preferences.HideOverlaysInCombat && context.InCombat) || (preferences.HideOverlaysInDuty && context.InDuty));
    public long Generation { get; private set; }
    public int PendingCount => queue.Count;

    public void SetPreferences(AutomationPreferences value)
    {
        preferences = AutomationPreferences.Snapshot(value);
        rulesVersion++;
        Generation++;
        queue.Clear();
    }

    public void ObserveContext(AutomationContext value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (context == value) return;
        var before = context;
        context = value;
        contextVersion++;
        Generation++;
        queue.Clear();
        if (!preferences.Enabled || !value.AccessReady || !value.LoggedIn || value.ContentId == 0) return;
        var triggers = new HashSet<AutomationTrigger>();
        var sameCharacter = before.ContentId == value.ContentId && before.LoggedIn && before.AccessReady;
        if (!sameCharacter) triggers.Add(AutomationTrigger.Login);
        else
        {
            if (before.InCombat != value.InCombat) triggers.Add(value.InCombat ? AutomationTrigger.CombatEntered : AutomationTrigger.CombatLeft);
            if (before.InDuty != value.InDuty) triggers.Add(value.InDuty ? AutomationTrigger.DutyEntered : AutomationTrigger.DutyLeft);
            if (before.TerritoryId != value.TerritoryId) triggers.Add(AutomationTrigger.TerritoryChanged);
        }
        foreach (var rule in preferences.Rules.Where(rule => rule.Enabled && triggers.Contains(rule.Trigger) &&
                     (!rule.TerritoryId.HasValue || rule.TerritoryId == value.TerritoryId)))
            queue.Enqueue(new AutomationIntent(contextVersion, rulesVersion, value.ContentId, rule));
    }

    public bool IsCurrent(AutomationIntent? value) => value is not null && value.Rule is not null && preferences.Enabled && context.AccessReady && context.LoggedIn && context.ContentId != 0 &&
        value.ContextVersion == contextVersion && value.RulesVersion == rulesVersion && value.ContentId == context.ContentId &&
        preferences.Rules.Any(rule => rule.Id == value.Rule.Id && rule == value.Rule && rule.Enabled);

    public bool TryDequeue(out AutomationIntent? value)
    {
        while (queue.TryDequeue(out var next))
            if (IsCurrent(next)) { value = next; return true; }
        value = null;
        return false;
    }
}
