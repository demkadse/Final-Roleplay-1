using FRP1.Core.Profiles;

namespace FRP1.Core.Safety;

public enum AudienceSelection { Unspecified, Under18, Adult }

public sealed record AudiencePreferences(
    AudienceSelection Selection = AudienceSelection.Unspecified,
    bool MatureContentOptIn = false);

public sealed class AudiencePolicy
{
    private readonly object gate = new();
    private AudiencePreferences preferences;

    public AudiencePolicy(AudiencePreferences? initial = null) => preferences = Normalize(initial ?? new());

    public event EventHandler? Changed;

    public AudiencePreferences Snapshot { get { lock (gate) return preferences; } }

    public bool AcceptsMatureContent
    {
        get
        {
            var snapshot = Snapshot;
            return snapshot.Selection == AudienceSelection.Adult && snapshot.MatureContentOptIn;
        }
    }

    public void SetAudience(AudiencePreferences value)
    {
        var normalized = Normalize(value);
        lock (gate)
        {
            if (preferences == normalized) return;
            preferences = normalized;
            // Serialize notifications with the change, so a rapid later opt-in cannot
            // hide a withdrawal from active connection subscribers.
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void EnsureAllowed(ProfileDocument profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Enum.IsDefined(profile.ContentRating) ||
            (profile.ContentRating == ContentRating.Mature18 && !AcceptsMatureContent))
            throw new InvalidDataException("This profile is unavailable for the current audience settings.");
    }

    private static AudiencePreferences Normalize(AudiencePreferences value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Enum.IsDefined(value.Selection)) throw new ArgumentOutOfRangeException(nameof(value));
        return value.Selection == AudienceSelection.Adult ? value : value with { MatureContentOptIn = false };
    }
}
