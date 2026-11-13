using FRP1.Core.Profiles;

namespace FRP1.Plugin.Integrations;

// Only these detached fields leave the native IPC reader. No source identity is exported into profiles.
public sealed record MoodlesStatus(Guid StatusId, string Label, string Description, uint IconId, string ContentHash);
public sealed record MoodlesBinding(Guid StatusId, string ContentHash, ContentRating? Rating = null);
public sealed record MoodlesOptions(bool Enabled = false, IReadOnlyList<MoodlesBinding>? Bindings = null);
public sealed record MoodlesReadResult(IntegrationStatus Status, IReadOnlyList<MoodlesStatus>? Statuses = null);
public sealed record MoodlesContext(IntegrationContext Integration, ContentRating TargetRating, int GlanceCount);

public interface IMoodlesReader : IDisposable
{
    long Version { get; }
    MoodlesReadResult ReadLocal();
}
