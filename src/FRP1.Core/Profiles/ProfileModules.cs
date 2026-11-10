using System.Text.Json.Serialization;

namespace FRP1.Core.Profiles;

public enum RpExperience { Unspecified, Newcomer, Experienced, Guide }
public enum PreferenceAnswer { Unspecified, AskFirst, Yes, No }
public enum AboutLayout { Sections, Chronicle, Compact }
public enum CompanionKind { Pet, Mount, Other }

public sealed record PersonalityTrait(string LeftLabel, string RightLabel, int Value = 50);

// These are conversation preferences, never consent or age verification.
public sealed record RpPreferences
{
    public RpExperience Experience { get; init; }
    public string PreferredStyle { get; init; } = "";
    public PreferenceAnswer Injury { get; init; }
    public PreferenceAnswer CharacterDeath { get; init; }
    public PreferenceAnswer Romance { get; init; }
    public PreferenceAnswer Crime { get; init; }
    public PreferenceAnswer LossOfControl { get; init; }
}

// Public profile styling is separate from the recipient's private UI theme.
public sealed record ProfileAppearance
{
    public uint IconId { get; init; }
    public string NameColor { get; init; } = "";
    public string AccentColor { get; init; } = "";
    public AboutLayout Layout { get; init; }
}

public sealed record CompanionProfile
{
    [JsonRequired] public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public CompanionKind Kind { get; init; }
    public string Species { get; init; } = "";
    public string Description { get; init; } = "";
    public uint IconId { get; init; }
}

// Only normalized, metadata-free PNG thumbnails are accepted on the wire.
public sealed record PortraitThumbnail(string MediaType, byte[] Data);
