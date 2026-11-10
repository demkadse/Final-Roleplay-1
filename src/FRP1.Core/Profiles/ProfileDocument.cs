using System.Text.Json.Serialization;

namespace FRP1.Core.Profiles;

public enum ContentRating { General, Mature18 }
public enum RpStatus { Ic, Ooc, Afk }
public enum RpAvailability { Open, Busy, Unavailable }

public sealed record GlanceEntry(string Label, string Text, uint IconId = 0, bool Enabled = true);
public sealed record RpHook(string Title, string Text);

// Only public, deliberately shared data belongs in this document.
public sealed record ProfileDocument
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public Guid Id { get; init; } = Guid.NewGuid();
    [JsonRequired] public long Revision { get; init; } = 1;
    [JsonRequired] public string DisplayName { get; init; } = "";
    [JsonRequired] public ContentRating ContentRating { get; init; } = ContentRating.General;
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Race { get; init; } = "";
    public string Clan { get; init; } = "";
    public string Age { get; init; } = "";
    public string Gender { get; init; } = "";
    public string Origin { get; init; } = "";
    public string Occupation { get; init; } = "";
    public string Appearance { get; init; } = "";
    public string Personality { get; init; } = "";
    public string Background { get; init; } = "";
    public RpStatus Status { get; init; } = RpStatus.Ooc;
    public RpAvailability Availability { get; init; } = RpAvailability.Unavailable;
    public string Summary { get; init; } = "";
    public string Pronouns { get; init; } = "";
    public string OocInformation { get; init; } = "";
    public string CurrentLocation { get; init; } = "";
    public string FreeCompany { get; init; } = "";
    public string EyeColor { get; init; } = "";
    public string Height { get; init; } = "";
    public string Weight { get; init; } = "";
    public string Birthplace { get; init; } = "";
    public string Residence { get; init; } = "";
    public List<string> RpLanguages { get; init; } = [];
    public List<PersonalityTrait> PersonalityTraits { get; init; } = [];
    public RpPreferences Preferences { get; init; } = new();
    public ProfileAppearance AppearanceStyle { get; init; } = new();
    public List<CompanionProfile> Companions { get; init; } = [];
    public PortraitThumbnail? Portrait { get; init; }
    public List<GlanceEntry> Glances { get; init; } = [];
    public List<RpHook> RpHooks { get; init; } = [];
    public SortedDictionary<string, string> ExtraFields { get; init; } = new(StringComparer.Ordinal);
}

// This association stays local and never enters the wire profile.
public sealed record CharacterBinding(ulong ContentId, uint HomeWorldId, Guid ProfileId);
