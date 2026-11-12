using System.Collections.ObjectModel;
using System.Numerics;

namespace FRP1.Plugin.Theming;

public readonly record struct ThemePalette(
    Vector4 Background,
    Vector4 Panel,
    Vector4 Text,
    Vector4 MutedText,
    Vector4 Accent,
    Vector4 Border,
    Vector4 Focus,
    Vector4 Input,
    Vector4 Button,
    Vector4 Hover,
    Vector4 Selected);

public readonly record struct ThemeDefinition(
    string Id,
    string Name,
    uint? RaceId,
    ThemePalette Palette,
    float Rounding = 7f);

public static class ThemeCatalog
{
    // Legacy IDs stay readable; their automatic variants share the quiet default palette.
    private static readonly ReadOnlyCollection<ThemeDefinition> Themes = Array.AsReadOnly(new[]
    {
        Legacy("hyur", "Hyur", 1), Legacy("elezen", "Elezen", 2),
        Legacy("lalafell", "Lalafell", 3), Legacy("miqote", "Miqo’te", 4),
        Legacy("roegadyn", "Roegadyn", 5), Legacy("aura", "Au Ra", 6),
        Legacy("hrothgar", "Hrothgar", 7), Legacy("viera", "Viera", 8),
        Dark("obsidian", "Obsidian", null, 0x15171B, 0x1D2026, 0xEDF0F4, 0xB7BEC9, 0xA5B8D3, 0x3C424C, 4f),
        new ThemeDefinition("pergament", "Pergament", null, new ThemePalette(
            Color(0xF2E7D1), Color(0xFFF5E2), Color(0x302A24), Color(0x534536),
            Color(0x704427), Color(0xC8B79C), Color(0x744D2B), Color(0xFBF1DC),
            Color(0xE4D3B5), Color(0xE2D4BB), Color(0xD5C3A5)), 4f),
        Dark("mondlicht", "Nachtblau", null, 0x101823, 0x172332, 0xEDF3FF, 0xB8C9E0, 0x91B7D8, 0x34465A, 4f),
    });

    public static IReadOnlyList<ThemeDefinition> All => Themes;

    public static ThemeDefinition Get(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        foreach (var theme in Themes)
            if (string.Equals(theme.Id, id, StringComparison.Ordinal)) return theme;
        throw new ArgumentException("Unknown FRP1 theme.", nameof(id));
    }

    public static ThemeDefinition ForRace(uint? raceId)
    {
        if (raceId.HasValue)
            foreach (var theme in Themes)
                if (theme.RaceId == raceId) return theme;
        return Get("obsidian");
    }

    public static ThemeDefinition Resolve(ThemePreferences preferences, uint? raceId)
    {
        ValidatePreferences(preferences);
        return preferences.Mode == ThemeSelectionMode.Manual ? Get(preferences.ManualThemeId) : ForRace(raceId);
    }

    public static void ValidatePreferences(ThemePreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (!Enum.IsDefined(preferences.Mode)) throw new ArgumentOutOfRangeException(nameof(preferences));
        if (preferences.ManualThemeId is not { Length: > 0 and <= 32 })
            throw new ArgumentException("Invalid FRP1 theme selection.", nameof(preferences));
        _ = Get(preferences.ManualThemeId);
    }

    private static ThemeDefinition Legacy(string id, string name, uint raceId) =>
        Dark(id, name, raceId, 0x15171B, 0x1D2026, 0xEDF0F4, 0xB7BEC9, 0xA5B8D3, 0x3C424C, 4f);

    private static ThemeDefinition Dark(string id, string name, uint? raceId, uint background, uint panel,
        uint text, uint muted, uint accent, uint border, float rounding = 7f)
    {
        var panelColor = Color(panel);
        return new ThemeDefinition(id, name, raceId, new ThemePalette(
            Color(background), panelColor, Color(text), Color(muted), Color(accent), Color(border),
            Color(accent), Color(background), panelColor,
            Blend(panelColor, Color(accent), 0.10f), Blend(panelColor, Color(accent), 0.18f)), rounding);
    }

    private static Vector4 Color(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

    private static Vector4 Blend(Vector4 first, Vector4 second, float amount) =>
        new(Vector3.Lerp(new Vector3(first.X, first.Y, first.Z), new Vector3(second.X, second.Y, second.Z), amount), 1f);
}
