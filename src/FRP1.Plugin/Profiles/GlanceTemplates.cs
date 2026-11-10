using FRP1.Core.Profiles;
using FRP1.Core.Safety;

namespace FRP1.Plugin.Profiles;

public sealed record GlanceTemplateSet(ContentRating ContentRating, IReadOnlyList<GlanceEntry> Entries);

public static class GlanceTemplates
{
    // Local stock templates contain no player information and no network references.
    public static IReadOnlyList<GlanceEntry> Defaults { get; } = Array.AsReadOnly(new[]
    {
        new GlanceEntry("Auftreten", ""), new GlanceEntry("Stimme", ""),
        new GlanceEntry("Kleidung", ""), new GlanceEntry("Besonderheit", ""),
        new GlanceEntry("Begleiter", ""),
    });

    public static GlanceTemplateSet Copy(ProfileDocument source, AudiencePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        EnsureAccess(source, policy);
        var owned = ProfileJson.Import(ProfileJson.Export(source));
        EnsureAccess(owned, policy);
        return new(owned.ContentRating, Array.AsReadOnly(owned.Glances.Select(entry => entry with { }).ToArray()));
    }

    public static ProfileDocument Apply(ProfileDocument target, GlanceTemplateSet template, AudiencePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(policy);
        EnsureAccess(target, policy);
        if (!Enum.IsDefined(template.ContentRating) || template.Entries is null ||
            (template.ContentRating == ContentRating.Mature18 && target.ContentRating != ContentRating.Mature18))
            throw new InvalidDataException("The glance template requires the matching profile content rating.");
        var selected = template.Entries.Take(6).Select(entry => entry is null
            ? throw new InvalidDataException("Invalid glance template.") : entry with { }).ToList();
        var result = ProfileJson.Import(ProfileJson.Export(target with { Glances = selected }));
        EnsureAccess(result, policy);
        return result;
    }

    private static void EnsureAccess(ProfileDocument profile, AudiencePolicy policy)
    {
        if (policy.Snapshot.Selection == AudienceSelection.Unspecified)
            throw new InvalidDataException("An audience selection is required to use profile templates.");
        policy.EnsureAllowed(profile);
    }
}
