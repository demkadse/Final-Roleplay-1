namespace FRP1.Plugin.Theming;

public enum ThemeSelectionMode { Automatic, Manual }

public sealed record ThemePreferences(
    ThemeSelectionMode Mode = ThemeSelectionMode.Automatic,
    string ManualThemeId = "obsidian",
    bool ReduceDecoration = false);
