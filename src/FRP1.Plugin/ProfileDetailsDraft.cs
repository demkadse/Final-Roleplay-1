using FRP1.Core.Profiles;

namespace FRP1.Plugin;

// Mutable UI buffers own their collections; neither edits nor later snapshots
// change the stored profile or previously captured asynchronous save results.
internal sealed class ProfileDetailsDraft(ProfileDocument profile)
{
    public RpStatus Status = profile.Status;
    public RpAvailability Availability = profile.Availability;
    public string Summary = profile.Summary;
    public string Pronouns = profile.Pronouns;
    public string OocInformation = profile.OocInformation;
    public string CurrentLocation = profile.CurrentLocation;
    public string FreeCompany = profile.FreeCompany;
    public List<GlanceEntry> Glances { get; } = profile.Glances.Select(entry => entry with { }).ToList();
    public List<RpHook> RpHooks { get; } = profile.RpHooks.Select(entry => entry with { }).ToList();

    public ProfileDocument ApplyTo(ProfileDocument target) => target with
    {
        Status = Status,
        Availability = Availability,
        Summary = Summary,
        Pronouns = Pronouns,
        OocInformation = OocInformation,
        CurrentLocation = CurrentLocation,
        FreeCompany = FreeCompany,
        Glances = Glances.Select(entry => entry with { }).ToList(),
        RpHooks = RpHooks.Select(entry => entry with { }).ToList(),
    };
}
