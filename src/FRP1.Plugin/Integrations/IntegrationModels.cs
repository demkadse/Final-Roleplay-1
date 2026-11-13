using System.Text.Json.Serialization;
using FRP1.Core.Safety;

namespace FRP1.Plugin.Integrations;

public sealed record IntegrationSettings
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public bool HonorificEnabled { get; init; }
    public bool IpcReadEnabled { get; init; }
    public bool IpcControlEnabled { get; init; }
    public bool IpcSetStatusEnabled { get; init; }
    public bool IpcSetAvailabilityEnabled { get; init; }
    public bool IpcOpenOwnProfileEnabled { get; init; }
    public bool MoodlesEnabled { get; init; }
    public IReadOnlyList<MoodlesBinding> MoodlesBindings { get; init; } = Array.Empty<MoodlesBinding>();
}

public sealed record IntegrationIpcOptions(bool ReadEnabled = false, bool ControlEnabled = false,
    bool SetStatusEnabled = false, bool SetAvailabilityEnabled = false, bool OpenOwnProfileEnabled = false);

public enum IntegrationStatus { Disabled, Missing, Incompatible, Ready, Busy, Error, Blocked }
public enum IntegrationActionResult { None, Queued, Applied, Disabled, NotReady, Blocked, Stale, RateLimited, AlreadyPending }

// Detached local identity. Address plus content ID prevents reuse of a native object ID.
public readonly record struct IntegrationActor(ulong ContentId, ulong ObjectId, long Address, string Name,
    uint HomeWorldId, uint TerritoryId, uint MapId)
{
    public bool IsValid => ContentId != 0 && ObjectId != 0 && Address != 0 && !string.IsNullOrWhiteSpace(Name) &&
        HomeWorldId != 0 && TerritoryId != 0 && MapId != 0;
}

public sealed record IntegrationDraftToken(Guid ProfileId, long Revision, string Hash, long EditorGeneration);
public sealed record IntegrationContext(IntegrationActor Actor, bool AccessReady, AudiencePreferences Audience,
    long PolicyGeneration, IntegrationDraftToken? Draft, bool Dirty, bool Busy)
{
    public bool CanRead => AccessReady && Actor.IsValid && Audience.Selection != AudienceSelection.Unspecified;
    public bool CanImport => CanRead && !Dirty && !Busy && Draft is { ProfileId: var id, Revision: > 0 } && id != Guid.Empty;
}

public sealed record HonorificReadResult(IntegrationStatus Status, string? Json = null);

// Read() is called only by Tick on the UI/frame thread. Events only advance Version.
public interface IHonorificReader : IDisposable
{
    long Version { get; }
    HonorificReadResult Read();
}
