using Stride.Engine;

namespace ColorRise.Skein;

/// <summary>
/// Knows the actors of the running scenes, so that a command finds its target by actor name.
/// </summary>
public sealed class DialogueActorProcessor : EntityProcessor<DialogueActorComponent>
{
    private readonly List<DialogueActorComponent> actors = [];

    /// <summary>
    /// The actors, in the order they came in.
    /// </summary>
    public IReadOnlyList<DialogueActorComponent> Actors => actors;

    /// <summary>
    /// Finds the first actor of a name.
    /// </summary>
    /// <returns>The actor, or null when no actor has the name.</returns>
    public DialogueActorComponent? Find(string actorName)
    {
        foreach (var actor in actors)
        {
            if (actor.EffectiveName == actorName)
                return actor;
        }

        return null;
    }

    /// <inheritdoc />
    protected override void OnEntityComponentAdding(Entity entity, DialogueActorComponent component, DialogueActorComponent data) => actors.Add(component);

    /// <inheritdoc />
    protected override void OnEntityComponentRemoved(Entity entity, DialogueActorComponent component, DialogueActorComponent data) => actors.Remove(component);
}
