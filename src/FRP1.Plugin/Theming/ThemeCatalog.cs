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
    // Original FRP1 palettes. Race IDs are local game-sheet row IDs, not profile text.
    private static readonly ReadOnlyCollection<ThemeDefinition> Themes = Array.AsReadOnly(new[]
    {
        Dark("hyur", "Hyur · Schiefer und Bronze", 1, 0x141C27, 0x1E2938, 0xF4F1E9, 0xC2C9D2, 0xD5AF79, 0x6C829A),
        Dark("elezen", "Elezen · Blaugrau und Silber", 2, 0x151D28, 0x233043, 0xF0F3F8, 0xC5D0DF, 0xC9D7EE, 0x6D86A7, 4f),
        Dark("lalafell", "Lalafell · Honig und Creme", 3, 0x251B15, 0x35281E, 0xFFF4DD, 0xD9CAB4, 0xEFCA79, 0xA5875F, 10f),
        Dark("miqote", "Miqo’te · Pflaume und Roségold", 4, 0x231725, 0x352338, 0xFBEDF4, 0xD7BFD0, 0xE4B2AA, 0x9B738E, 9f),
        Dark("roegadyn", "Roegadyn · Petrol und Kupfer", 5, 0x101E23, 0x193239, 0xEEF6F2, 0xBDD2D0, 0xE0AE8B, 0x688E91, 3f),
        Dark("aura", "Au Ra · Tinte und Jade", 6, 0x101B2B, 0x1C2B40, 0xF8F1DD, 0xC7D1CF, 0x9CD9C1, 0x688C9A, 5f),
        Dark("hrothgar", "Hrothgar · Anthrazit und Bernstein", 7, 0x1A1A1D, 0x2B2A2D, 0xF8F1E5, 0xCFC6B9, 0xF1C16B, 0x96846B, 3f),
        Dark("viera", "Viera · Moos und Violett", 8, 0x172018, 0x263329, 0xF4F2DF, 0xC9D4C0, 0xD1BFEA, 0x789879, 9f),
        Dark("obsidian", "Obsidian", null, 0x17191E, 0x252931, 0xF1F3F7, 0xC5CBD5, 0xCCD4E2, 0x7C879B, 6f),
        new ThemeDefinition("pergament", "Pergament", null, new ThemePalette(
            Color(0xF2E7D1), Color(0xFFF5E2), Color(0x302A24), Color(0x534536),
            Color(0x704427), Color(0x9B8266), Color(0x744D2B), Color(0xFBF1DC),
            Color(0xE4D3B5), Color(0xD7C39F), Color(0xCCB58D)), 5f),
        Dark("mondlicht", "Mondlicht", null, 0x111A2D, 0x1C2A44, 0xEDF3FF, 0xBCCBE5, 0xB6D0FC, 0x748CB7, 8f),
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

    private static ThemeDefinition Dark(string id, string name, uint? raceId, uint background, uint panel,
        uint text, uint muted, uint accent, uint border, float rounding = 7f)
    {
        var panelColor = Color(panel);
        return new ThemeDefinition(id, name, raceId, new ThemePalette(
            Color(background), panelColor, Color(text), Color(muted), Color(accent), Color(border),
            Color(accent), Color(background), panelColor,
            Blend(panelColor, Color(accent), 0.12f), Blend(panelColor, Color(accent), 0.20f)), rounding);
    }

    private static Vector4 Color(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

    private static Vector4 Blend(Vector4 first, Vector4 second, float amount) =>
        new(Vector3.Lerp(new Vector3(first.X, first.Y, first.Z), new Vector3(second.X, second.Y, second.Z), amount), 1f);
}
