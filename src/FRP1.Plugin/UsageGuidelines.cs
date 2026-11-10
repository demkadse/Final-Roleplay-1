namespace FRP1.Plugin;

internal static class UsageGuidelines
{
    public const int CurrentVersion = 1;

    public static IReadOnlyList<string> Notices { get; } = Array.AsReadOnly(new[]
    {
        "Als allgemein/jugendfrei markierte Profile dürfen keine sexuellen oder sonstigen expliziten Inhalte ab 18 enthalten. Das gilt für Namen, Titel, Glances, Freitexte, Bilder und alle weiteren Profilfelder.",
        "Kennzeichne nicht jugendfreie Profile korrekt als „ab 18“. Die Kennzeichnung gilt für das gesamte Profil, nicht nur für einzelne Abschnitte.",
        "Gib dein Alter wahrheitsgemäß an. Die Altersauswahl bezieht sich auf dich als reale Person, nicht auf das Alter deines RP-Charakters. Sie ist eine Selbstauskunft und wird nicht automatisch überprüft.",
        "Sexualisierte Darstellungen Minderjähriger sind grundsätzlich nicht erlaubt, auch nicht in Profilen ab 18.",
        "Veröffentliche keine privaten Daten anderer Personen ohne deren Erlaubnis und keine belästigenden Inhalte.",
        "Verwende nur eigene Bilder und Inhalte oder solche, für deren Nutzung du die nötige Erlaubnis hast.",
        "Diese Hinweisbestätigung bleibt lokal. Sie erstellt kein Konto, wird nicht an andere Clients übertragen und erteilt keine Altersfreigabe.",
    });
}
