using FRP1.Core.Profiles;
using FRP1.Core.Safety;
using FRP1.Plugin.Integrations;

namespace FRP1.Plugin.Ipc;

public static class FrpIpcRules
{
    public const int ReadCapability = 1;
    public const int SetStatusCapability = 2;
    public const int SetAvailabilityCapability = 4;
    public const int OpenOwnProfileCapability = 8;
    public const int ActionIntervalMilliseconds = 500;
    public const int CompletionLifetimeMilliseconds = 30_000;

    public static int Capabilities(IntegrationIpcOptions options) => (options.ReadEnabled ? ReadCapability : 0) |
        (options.ControlEnabled && options.SetStatusEnabled ? SetStatusCapability : 0) |
        (options.ControlEnabled && options.SetAvailabilityEnabled ? SetAvailabilityCapability : 0) |
        (options.ControlEnabled && options.OpenOwnProfileEnabled ? OpenOwnProfileCapability : 0);

    public static bool ValidAction(FrpIpcAction action) => action.Kind switch
    {
        FrpIpcActionKind.SetStatus => action.Value is >= 0 and <= 2,
        FrpIpcActionKind.SetAvailability => action.Value is >= 0 and <= 2,
        FrpIpcActionKind.OpenOwnProfile => action.Value == 0,
        _ => false,
    };

    public static bool ActionEnabled(IntegrationIpcOptions options, FrpIpcAction action) => options.ControlEnabled && (action.Kind switch
    {
        FrpIpcActionKind.SetStatus => options.SetStatusEnabled,
        FrpIpcActionKind.SetAvailability => options.SetAvailabilityEnabled,
        FrpIpcActionKind.OpenOwnProfile => options.OpenOwnProfileEnabled,
        _ => false,
    });

    public static bool Allowed(FrpIpcContext context) => context.AccessReady && context.Actor.IsValid &&
        context.Audience.Selection != AudienceSelection.Unspecified && context.Active is { Id: var id, Revision: > 0 } active &&
        id != Guid.Empty && Enum.IsDefined(active.Rating) && Enum.IsDefined(active.Status) && Enum.IsDefined(active.Availability) &&
        (active.Rating == ContentRating.General || context.Audience.Selection == AudienceSelection.Adult && context.Audience.MatureContentOptIn);

    public static FrpIpcReadiness Readiness(FrpIpcContext context) => !Allowed(context) ? FrpIpcReadiness.Blocked :
        context.Dirty || context.Busy ? FrpIpcReadiness.Busy : FrpIpcReadiness.Ready;

    public static bool SameScope(FrpIpcContext left, FrpIpcContext right) => left.Actor == right.Actor &&
        left.AccessReady == right.AccessReady && left.Audience == right.Audience && left.PolicyGeneration == right.PolicyGeneration &&
        left.Active?.Id == right.Active?.Id;

    public static bool AlreadySatisfied(FrpIpcAction action, FrpIpcProfile active) => action.Kind switch
    {
        FrpIpcActionKind.SetStatus => (int)active.Status == action.Value,
        FrpIpcActionKind.SetAvailability => (int)active.Availability == action.Value,
        _ => false,
    };
}
