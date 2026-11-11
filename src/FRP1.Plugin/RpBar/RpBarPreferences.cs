using System.Collections.ObjectModel;

namespace FRP1.Plugin.RpBar;

public sealed record RpBarPreferences
{
    public bool Visible { get; init; } = true;
    public bool Locked { get; init; }
    public bool ShowHints { get; init; } = true;
    public bool Collapsed { get; init; }
    public float Scale { get; init; } = 1f;
    public float PositionX { get; init; } = 24f;
    public float PositionY { get; init; } = 100f;
    public IReadOnlyDictionary<ulong, Guid> ActiveProfiles { get; init; } =
        new ReadOnlyDictionary<ulong, Guid>(new Dictionary<ulong, Guid>());

    public static RpBarPreferences Snapshot(RpBarPreferences value)
    {
        RpBarSettingsStore.Validate(value);
        return value with { ActiveProfiles = new ReadOnlyDictionary<ulong, Guid>(new Dictionary<ulong, Guid>(value.ActiveProfiles)) };
    }
}
