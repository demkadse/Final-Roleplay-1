using FRP1.Plugin.Contacts;
using FRP1.Plugin.Presentation;

namespace FRP1.Plugin.RpBar;

// The own HUD follows the local character. Foreign targets belong to the social target card.
public sealed record RpBarSceneSnapshot(bool SceneValid, bool TargetPresent, ActorSnapshot? Target, ActorSnapshot? LocalActor);
public enum RpBarContextKind { Neutral, Own, Foreign }
public sealed record RpBarDisplayContext(RpBarContextKind Kind, ActorSnapshot? Actor = null, ProfileKey? Profile = null);

public static class RpBarContextResolver
{
    public static RpBarDisplayContext Resolve(RpBarSceneSnapshot scene, bool accessReady, PresentationResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(scene); ArgumentNullException.ThrowIfNull(resolver);
        if (!accessReady || !scene.SceneValid || scene.LocalActor is not { } local || !Valid(local)) return new(RpBarContextKind.Neutral);
        return new(RpBarContextKind.Own, local);
    }

    private static bool Valid(ActorSnapshot actor) => !string.IsNullOrWhiteSpace(actor.Name) && actor.Name.Length <= 80 &&
        actor.HomeWorldId is > 0 and <= 65535 && !actor.Name.Any(char.IsControl);
}
