using System.Collections.ObjectModel;
using System.Globalization;
using FRP1.Core.Profiles;

namespace FRP1.Plugin.UiProfiles;

public enum ProfileArea { Overview, Character, Ooc }

public static class ProfileAreas
{
    public static IReadOnlyList<ProfileArea> All { get; } = Array.AsReadOnly(new[]
    {
        ProfileArea.Overview, ProfileArea.Character, ProfileArea.Ooc,
    });

    public static string Title(ProfileArea area) => area switch
    {
        ProfileArea.Overview => "Überblick",
        ProfileArea.Character => "Charakter",
        ProfileArea.Ooc => "OOC",
        _ => throw new ArgumentOutOfRangeException(nameof(area)),
    };
}

public enum AdditionalContentKind { RpHook, ExtraField, PersonalityTrait, Companion }
public sealed record AdditionalContentValue(string Label, string Value);
public sealed record AdditionalContentEntry(Guid ProfileId, AdditionalContentKind Kind, int Index, string Key, string Title,
    IReadOnlyList<AdditionalContentValue> Values, uint IconId = 0);

// Existing extended contents remain discoverable and can only be removed deliberately.
// These helpers neither mutate the source nor drop unrelated profile fields.
public static class ExistingAdditionalContent
{
    public static IReadOnlyList<AdditionalContentEntry> Read(ProfileDocument profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var result = new List<AdditionalContentEntry>();
        for (var index = 0; index < profile.RpHooks.Count; index++)
        {
            var hook = profile.RpHooks[index];
            result.Add(new(profile.Id, AdditionalContentKind.RpHook, index, index.ToString(CultureInfo.InvariantCulture), hook.Title,
                Values(new AdditionalContentValue("Beschreibung", hook.Text))));
        }
        var extraIndex = 0;
        foreach (var pair in profile.ExtraFields)
            result.Add(new(profile.Id, AdditionalContentKind.ExtraField, extraIndex++, pair.Key, pair.Key, Values(new AdditionalContentValue("Inhalt", pair.Value))));
        for (var index = 0; index < profile.PersonalityTraits.Count; index++)
        {
            var trait = profile.PersonalityTraits[index];
            result.Add(new(profile.Id, AdditionalContentKind.PersonalityTrait, index, index.ToString(CultureInfo.InvariantCulture), "Persönlichkeitsmerkmal",
                Values(new("Linker Pol", trait.LeftLabel), new("Rechter Pol", trait.RightLabel),
                    new("Gewichtung", trait.Value.ToString(CultureInfo.InvariantCulture) + "%"))));
        }
        for (var index = 0; index < profile.Companions.Count; index++)
        {
            var companion = profile.Companions[index];
            result.Add(new(profile.Id, AdditionalContentKind.Companion, index, companion.Id.ToString("N"), companion.Name,
                Values(new("Begleitertyp", companion.Kind.ToString()), new("Art / Volk", companion.Species),
                    new("Beschreibung", companion.Description)), companion.IconId));
        }
        return result.AsReadOnly();
    }

    public static ProfileDocument Remove(ProfileDocument profile, AdditionalContentEntry entry)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.ProfileId != profile.Id || !Read(profile).Any(current => current.Kind == entry.Kind && current.Index == entry.Index &&
            current.Key == entry.Key && current.Title == entry.Title && current.IconId == entry.IconId && current.Values.SequenceEqual(entry.Values)))
            throw new InvalidOperationException("This additional entry changed; select its current version.");
        profile = Copy(profile);
        return entry.Kind switch
        {
            AdditionalContentKind.RpHook => profile with
            { RpHooks = profile.RpHooks.Where((_, index) => index != entry.Index).Select(item => item with { }).ToList() },
            AdditionalContentKind.ExtraField => profile with
            { ExtraFields = new SortedDictionary<string, string>(profile.ExtraFields.Where(pair => pair.Key != entry.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal) },
            AdditionalContentKind.PersonalityTrait => profile with
            { PersonalityTraits = profile.PersonalityTraits.Where((_, index) => index != entry.Index).Select(item => item with { }).ToList() },
            AdditionalContentKind.Companion => profile with
            { Companions = profile.Companions.Where((_, index) => index != entry.Index).Select(item => item with { }).ToList() },
            _ => throw new ArgumentOutOfRangeException(nameof(entry)),
        };
    }

    private static ReadOnlyCollection<AdditionalContentValue> Values(params AdditionalContentValue[] values) => Array.AsReadOnly(values);

    private static ProfileDocument Copy(ProfileDocument source) => source with
    {
        Glances = source.Glances.Select(entry => entry with { }).ToList(),
        RpHooks = source.RpHooks.Select(entry => entry with { }).ToList(),
        ExtraFields = new SortedDictionary<string, string>(source.ExtraFields, StringComparer.Ordinal),
        RpLanguages = source.RpLanguages.ToList(),
        PersonalityTraits = source.PersonalityTraits.Select(entry => entry with { }).ToList(),
        Preferences = source.Preferences with { }, AppearanceStyle = source.AppearanceStyle with { },
        Companions = source.Companions.Select(entry => entry with { }).ToList(),
        Portrait = source.Portrait is null ? null : source.Portrait with { Data = (byte[])source.Portrait.Data.Clone() },
    };
}
