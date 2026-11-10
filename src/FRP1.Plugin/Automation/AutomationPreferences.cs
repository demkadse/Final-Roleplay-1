using System.Text.Json.Serialization;
using FRP1.Core.Profiles;

namespace FRP1.Plugin.Automation;

public enum AutomationTrigger { Login, CombatEntered, CombatLeft, DutyEntered, DutyLeft, TerritoryChanged }
public enum AutomationActionKind { SetStatus, SetAvailability, SelectProfile }

public sealed record AutomationRule
{
    [JsonRequired] public Guid Id { get; init; } = Guid.NewGuid();
    [JsonRequired] public string Name { get; init; } = "Regel";
    [JsonRequired] public bool Enabled { get; init; } = true;
    [JsonRequired] public AutomationTrigger Trigger { get; init; } = AutomationTrigger.CombatEntered;
    [JsonRequired] public AutomationActionKind Action { get; init; } = AutomationActionKind.SetStatus;
    [JsonRequired] public RpStatus Status { get; init; } = RpStatus.Ooc;
    [JsonRequired] public RpAvailability Availability { get; init; } = RpAvailability.Unavailable;
    [JsonRequired] public Guid? ProfileId { get; init; }
    [JsonRequired] public uint? TerritoryId { get; init; }
}

public sealed record AutomationPreferences
{
    [JsonRequired] public bool Enabled { get; init; }
    [JsonRequired] public bool HideOverlaysInCombat { get; init; }
    [JsonRequired] public bool HideOverlaysInDuty { get; init; }
    [JsonRequired] public IReadOnlyList<AutomationRule> Rules { get; init; } = Array.AsReadOnly(Array.Empty<AutomationRule>());

    public static AutomationPreferences Snapshot(AutomationPreferences value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Rules is null || value.Rules.Count > 16) throw new InvalidDataException("Too many automation rules.");
        var ids = new HashSet<Guid>();
        var rules = value.Rules.ToArray();
        foreach (var rule in rules)
        {
            if (rule is null || rule.Id == Guid.Empty || !ids.Add(rule.Id) || rule.Name is null ||
                string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Length > 80 || rule.Name.Any(char.IsControl) ||
                !Enum.IsDefined(rule.Trigger) || !Enum.IsDefined(rule.Action) || !Enum.IsDefined(rule.Status) || !Enum.IsDefined(rule.Availability) ||
                rule.TerritoryId == 0 || (rule.Action == AutomationActionKind.SelectProfile ? rule.ProfileId is null || rule.ProfileId == Guid.Empty : rule.ProfileId is not null))
                throw new InvalidDataException("Invalid automation rule.");
        }
        return value with { Rules = Array.AsReadOnly(rules) };
    }
}
