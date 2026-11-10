using FRP1.Core.Networking;
using FRP1.Core.Profiles;

namespace FRP1.Plugin.UiSocial;

public enum MouseoverDetail { Compact, Detailed }
public sealed record EnvironmentFilter(RpStatus? Status = null, RpAvailability? Availability = null, bool HideEmpty = false);

public static class SocialUiRules
{
    public static void ValidateListenPort(int port)
    {
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Invalid local listen port.");
    }

    public static PeerInvitation CreateAdvertisedInvitation(PeerInvitation original, string numericAddress, int port)
    {
        ArgumentNullException.ThrowIfNull(original);
        original.Validate();
        var advertised = original with { Address = numericAddress, Port = port };
        advertised.Validate();
        return advertised;
    }

    public static bool MatchesEnvironment(ProfileDocument? profile, EnvironmentFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if ((filter.Status.HasValue && !Enum.IsDefined(filter.Status.Value)) ||
            (filter.Availability.HasValue && !Enum.IsDefined(filter.Availability.Value)))
            throw new ArgumentOutOfRangeException(nameof(filter));
        if (profile is null) return !filter.Status.HasValue && !filter.Availability.HasValue && !filter.HideEmpty;
        return (!filter.Status.HasValue || profile.Status == filter.Status.Value) &&
            (!filter.Availability.HasValue || profile.Availability == filter.Availability.Value) &&
            (!filter.HideEmpty || !IsEmptyProfile(profile));
    }

    // A name, portrait, decoration or status alone does not provide written RP information.
    public static bool IsEmptyProfile(ProfileDocument profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new[] { profile.FirstName, profile.LastName, profile.Title, profile.Race, profile.Clan, profile.Age,
            profile.Gender, profile.Origin, profile.Occupation, profile.Appearance, profile.Personality, profile.Background,
            profile.Summary, profile.Pronouns, profile.OocInformation, profile.CurrentLocation, profile.FreeCompany,
            profile.EyeColor, profile.Height, profile.Weight, profile.Birthplace, profile.Residence, profile.Preferences.PreferredStyle }
            .All(string.IsNullOrWhiteSpace) && profile.RpLanguages.All(string.IsNullOrWhiteSpace) &&
            profile.PersonalityTraits.Count == 0 && profile.Companions.Count == 0 &&
            profile.Preferences with { PreferredStyle = "" } == new RpPreferences() &&
            !profile.Glances.Any(glance => glance.Enabled && (!string.IsNullOrWhiteSpace(glance.Label) || !string.IsNullOrWhiteSpace(glance.Text))) &&
            !profile.RpHooks.Any(hook => !string.IsNullOrWhiteSpace(hook.Title) || !string.IsNullOrWhiteSpace(hook.Text)) &&
            profile.ExtraFields.Values.All(string.IsNullOrWhiteSpace);
    }
}
