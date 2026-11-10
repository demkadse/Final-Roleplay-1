using FRP1.Core.Profiles;

namespace FRP1.Plugin.UiProfiles;

// This draft owns its editable collections and never writes profiles itself.
public sealed class ExtendedProfileDraft
{
    private PortraitThumbnail? portrait;
    public ContentRating ContentRating = ContentRating.General;
    public string EyeColor = "";
    public string Height = "";
    public string Weight = "";
    public string Birthplace = "";
    public string Residence = "";
    public string LanguagesText = "";
    public List<PersonalityTrait> PersonalityTraits { get; } = [];
    public RpPreferences Preferences = new();
    public ProfileAppearance Appearance = new();
    public List<CompanionProfile> Companions { get; } = [];
    public PortraitThumbnail? Portrait
    {
        get => Copy(portrait);
        set { if (value is not null) PortraitValidator.Validate(value); portrait = Copy(value); }
    }

    public static ExtendedProfileDraft From(ProfileDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new ExtendedProfileDraft
        {
            EyeColor = source.EyeColor, Height = source.Height, Weight = source.Weight,
            ContentRating = source.ContentRating,
            Birthplace = source.Birthplace, Residence = source.Residence,
            LanguagesText = string.Join('\n', source.RpLanguages),
            Preferences = source.Preferences with { }, Appearance = source.AppearanceStyle with { },
            Portrait = source.Portrait,
        };
        result.PersonalityTraits.AddRange(source.PersonalityTraits.Select(trait => trait with { }));
        result.Companions.AddRange(source.Companions.Select(companion => companion with { }));
        return result;
    }

    public ProfileDocument ApplyTo(ProfileDocument target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target with
        {
            EyeColor = EyeColor, Height = Height, Weight = Weight, Birthplace = Birthplace, Residence = Residence,
            RpLanguages = LanguagesText.Split('\n').Select(value => value.Trim()).Where(value => value.Length != 0).ToList(),
            PersonalityTraits = PersonalityTraits.Select(trait => trait with { }).ToList(),
            Preferences = Preferences with { }, AppearanceStyle = Appearance with { },
            Companions = Companions.Select(companion => companion with { }).ToList(), Portrait = Copy(portrait),
        };
    }

    private static PortraitThumbnail? Copy(PortraitThumbnail? source) =>
        source is null ? null : source with { Data = (byte[])source.Data.Clone() };
}
