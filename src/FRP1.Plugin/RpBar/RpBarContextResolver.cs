using FRP1.Plugin.Contacts;
using FRP1.Plugin.Presentation;

namespace FRP1.Plugin.RpBar;

// Raw game target only. A separately pinned reader target must never replace this snapshot.
public sealed record RpBarSceneSnapshot(bool SceneValid, bool TargetPresent, ActorSnapshot? Target, ActorSnapshot? LocalActor);
public enum RpBarContextKind { Neutral, Own, Foreign }
public sealed record RpBarDisplayContext(RpBarContextKind Kind, ActorSnapshot? Actor = null, ProfileKey? Profile = null);

public static class RpBarContextResolver
{
    public static RpBarDisplayContext Resolve(RpBarSceneSnapshot scene, bool accessReady, PresentationResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(scene); ArgumentNullException.ThrowIfNull(resolver);
        if (!accessReady || !scene.SceneValid || scene.LocalActor is not { } local || !Valid(local)) return new(RpBarContextKind.Neutral);
        if (!scene.TargetPresent) return scene.Target is null ? new(RpBarContextKind.Own, local) : new(RpBarContextKind.Neutral);
        if (scene.Target is not { } target || !Valid(target)) return new(RpBarContextKind.Neutral);
        if (SameCharacter(local, target)) return new(RpBarContextKind.Own, local);
        var view = resolver.Resolve(target);
        return view.IsReadable && view.Key is { } key
            ? new(RpBarContextKind.Foreign, target, key)
            : new(RpBarContextKind.Neutral, target);
    }

    private static bool SameCharacter(ActorSnapshot left, ActorSnapshot right) => left.HomeWorldId == right.HomeWorldId &&
        string.Equals(left.Name.Trim(), right.Name.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool Valid(ActorSnapshot actor) => !string.IsNullOrWhiteSpace(actor.Name) && actor.Name.Length <= 80 &&
        actor.HomeWorldId is > 0 and <= 65535 && !actor.Name.Any(char.IsControl);
}
