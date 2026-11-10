using FRP1.Core.Profiles;
using FRP1.Plugin.Localization;

namespace FRP1.Plugin.UiSocial;

public sealed partial class SocialUiModule
{
    private static string T(string german, string english) => UiText.CurrentLanguage == UiLanguage.English ? english : german;
    private static string StatusLabel(RpStatus status) => status switch
    {
        RpStatus.Ic => T("IC – im Charakter", "IC – in character"),
        RpStatus.Ooc => T("OOC – außerhalb des Charakters", "OOC – out of character"),
        _ => T("AFK – abwesend", "AFK – away"),
    };
    private static string AvailabilityLabel(RpAvailability availability) => availability switch
    {
        RpAvailability.Open => T("Offen für RP", "Open for RP"),
        RpAvailability.Busy => T("Beschäftigt", "Busy"),
        _ => T("Kein RP", "No RP"),
    };
}
