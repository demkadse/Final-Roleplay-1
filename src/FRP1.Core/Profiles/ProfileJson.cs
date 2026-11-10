using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FRP1.Core.Safety;

namespace FRP1.Core.Profiles;

public static class ProfileJson
{
    public const int MaximumBytes = 48 * 1024;
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 12,
    };

    public static void Validate(ProfileDocument? profile)
    {
        if (profile is null || profile.SchemaVersion != 1 || profile.Id == Guid.Empty || profile.Revision < 1 ||
            !Enum.IsDefined(profile.ContentRating) || !Enum.IsDefined(profile.Status) || !Enum.IsDefined(profile.Availability))
            throw new InvalidDataException("Unsupported or invalid profile metadata.");
        Check(profile.DisplayName, 160, required: true);
        foreach (var field in new[] { profile.FirstName, profile.LastName, profile.Title, profile.Race,
                     profile.Clan, profile.Age, profile.Gender, profile.Origin, profile.Occupation })
            Check(field, 256);
        foreach (var field in new[] { profile.Appearance, profile.Personality, profile.Background })
            Check(field, 8192);
        Check(profile.Summary, 512);
        Check(profile.Pronouns, 80);
        Check(profile.OocInformation, 4096);
        Check(profile.CurrentLocation, 256);
        Check(profile.FreeCompany, 160);
        foreach (var field in new[] { profile.EyeColor, profile.Height, profile.Weight }) Check(field, 80);
        Check(profile.Birthplace, 160);
        Check(profile.Residence, 160);
        if (profile.RpLanguages is null || profile.RpLanguages.Count > 12)
            throw new InvalidDataException("Too many roleplay languages.");
        foreach (var language in profile.RpLanguages) Check(language, 80, required: true);
        if (profile.PersonalityTraits is null || profile.PersonalityTraits.Count > 8)
            throw new InvalidDataException("Too many personality traits.");
        foreach (var trait in profile.PersonalityTraits)
        {
            if (trait is null || trait.Value is < 0 or > 100)
                throw new InvalidDataException("Invalid personality trait.");
            Check(trait.LeftLabel, 80, required: true);
            Check(trait.RightLabel, 80, required: true);
        }
        var preferences = profile.Preferences;
        if (preferences is null || !Enum.IsDefined(preferences.Experience) ||
            new[] { preferences.Injury, preferences.CharacterDeath, preferences.Romance,
                preferences.Crime, preferences.LossOfControl }.Any(value => !Enum.IsDefined(value)))
            throw new InvalidDataException("Invalid roleplay preferences.");
        Check(preferences.PreferredStyle, 512);
        var appearance = profile.AppearanceStyle;
        if (appearance is null || appearance.IconId > 1_000_000 || !Enum.IsDefined(appearance.Layout) ||
            !IsColor(appearance.NameColor) || !IsColor(appearance.AccentColor))
            throw new InvalidDataException("Invalid profile appearance.");
        if (profile.Companions is null || profile.Companions.Count > 8)
            throw new InvalidDataException("Too many companions.");
        var companionIds = new HashSet<Guid>();
        foreach (var companion in profile.Companions)
        {
            if (companion is null || companion.Id == Guid.Empty || !companionIds.Add(companion.Id) ||
                !Enum.IsDefined(companion.Kind) || companion.IconId > 1_000_000)
                throw new InvalidDataException("Invalid companion profile.");
            Check(companion.Name, 160, required: true);
            Check(companion.Species, 160);
            Check(companion.Description, 2048);
        }
        if (profile.Portrait is not null) PortraitValidator.Validate(profile.Portrait);
        if (profile.Glances is null || profile.Glances.Count > 5)
            throw new InvalidDataException("Too many glance entries.");
        foreach (var glance in profile.Glances)
        {
            if (glance is null || glance.IconId > 1_000_000)
                throw new InvalidDataException("Invalid glance entry.");
            Check(glance.Label, 80, required: true);
            Check(glance.Text, 1024);
        }
        if (profile.RpHooks is null || profile.RpHooks.Count > 10)
            throw new InvalidDataException("Too many roleplay hooks.");
        foreach (var hook in profile.RpHooks)
        {
            if (hook is null) throw new InvalidDataException("Invalid roleplay hook.");
            Check(hook.Title, 80, required: true);
            Check(hook.Text, 1024);
        }
        if (profile.ExtraFields is null || profile.ExtraFields.Count > 24)
            throw new InvalidDataException("Too many additional fields.");
        foreach (var pair in profile.ExtraFields)
        {
            Check(pair.Key, 80, required: true);
            Check(pair.Value, 2048);
        }
        if (JsonSerializer.SerializeToUtf8Bytes(profile, Options).Length > MaximumBytes)
            throw new InvalidDataException("Profile exceeds the size limit.");
    }

    public static byte[] Export(ProfileDocument profile)
    {
        Validate(profile);
        var canonical = profile with { ExtraFields = new SortedDictionary<string, string>(profile.ExtraFields, StringComparer.Ordinal) };
        return JsonSerializer.SerializeToUtf8Bytes(canonical, Options);
    }

    public static ProfileDocument Import(ReadOnlySpan<byte> json)
    {
        if (json.Length == 0 || json.Length > MaximumBytes)
            throw new InvalidDataException("Profile exceeds the size limit.");
        try
        {
            var profile = JsonSerializer.Deserialize<ProfileDocument>(json, Options);
            Validate(profile);
            return profile!;
        }
        catch (JsonException ex) { throw new InvalidDataException("Invalid profile JSON.", ex); }
    }

    public static string Hash(ProfileDocument profile) => Convert.ToHexString(SHA256.HashData(Export(profile)));

    public static bool IsColor(string? color) => color is not null &&
        (color.Length == 0 || (color.Length == 7 && color[0] == '#' && color.AsSpan(1).ToString().All(Uri.IsHexDigit)));

    public static ProfileDocument Import(ReadOnlySpan<byte> json, AudiencePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var profile = Import(json);
        policy.EnsureAllowed(profile);
        return profile;
    }

    private static void Check(string? text, int limit, bool required = false)
    {
        if (text is null || text.Length > limit || (required && string.IsNullOrWhiteSpace(text)) ||
            text.Any(c => char.IsControl(c) && c is not '\n' and not '\r' and not '\t'))
            throw new InvalidDataException("Invalid profile text.");
    }
}
