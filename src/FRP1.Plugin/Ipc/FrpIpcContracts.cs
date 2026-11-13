using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Integrations;

namespace FRP1.Plugin.Ipc;

public enum FrpIpcReadiness { Disabled, Blocked, Busy, Ready }
public enum FrpIpcResult
{
    Unknown = 0, Queued = 1, Running = 2, Persisted = 3, Opened = 4, AlreadySatisfied = 5,
    Disabled = -1, InvalidArgument = -2, Blocked = -3, Busy = -4, Stale = -5, RateLimited = -6, Error = -7,
}
public enum FrpIpcActionKind { SetStatus, SetAvailability, OpenOwnProfile }
public readonly record struct FrpIpcAction(FrpIpcActionKind Kind, int Value = 0);

// Private detached state; only primitive status/availability values reach the IPC provider.
public sealed record FrpIpcProfile(Guid Id, long Revision, string Hash, ContentRating Rating,
    RpStatus Status, RpAvailability Availability)
{
    public static FrpIpcProfile From(ProfileDocument profile) =>
        new(profile.Id, profile.Revision, ProfileJson.Hash(profile), profile.ContentRating, profile.Status, profile.Availability);
}

public sealed record FrpIpcContext(IntegrationActor Actor, bool AccessReady, AudiencePreferences Audience,
    long PolicyGeneration, long CatalogVersion, long EditorGeneration, bool Dirty, bool Busy, FrpIpcProfile? Active);
