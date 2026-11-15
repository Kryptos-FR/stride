using Stride.Engine;

namespace ColorRise.Skein;

/// <summary>
/// Finds what a command names among the entities of a dialogue runner's scenes: the actors first
/// (<see cref="DialogueActorComponent"/>), then the entities by their own name, in the runner's scene before the others.
/// </summary>
public sealed class EntityTargetResolver : IYarnTargetResolver
{
    private readonly Entity origin;

    /// <summary>
    /// Makes a resolver.
    /// </summary>
    /// <param name="origin">The entity the search starts from: the runner's.</param>
    public EntityTargetResolver(Entity origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        this.origin = origin;
    }

    /// <summary>
    /// Finds an entity by name, or a component of the type on an entity of that name. Among several entities of one name,
    /// the first that has what the type asks wins.
    /// </summary>
    public object? Find(string name, Type type)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(type);

        foreach (var entity in Named(name))
        {
            if (type.IsInstanceOfType(entity))
                return entity;

            foreach (var component in entity.Components)
            {
                if (type.IsInstanceOfType(component))
                    return component;
            }
        }

        return null;
    }

    private IEnumerable<Entity> Named(string name)
    {
        if (origin.EntityManager?.GetProcessor<DialogueActorProcessor>() is { } actors)
        {
            foreach (var actor in actors.Actors)
            {
                if (actor.EffectiveName == name)
                    yield return actor.Entity;
            }
        }

        var scene = origin.Scene;
        if (scene is null)
            yield break;

        foreach (var entity in EntitiesOf(scene.Entities))
        {
            if (entity.Name == name)
                yield return entity;
        }

        var root = scene;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        foreach (var other in ScenesOf(root))
        {
            if (other == scene)
                continue;

            foreach (var entity in EntitiesOf(other.Entities))
            {
                if (entity.Name == name)
                    yield return entity;
            }
        }
    }

    private static IEnumerable<Scene> ScenesOf(Scene scene)
    {
        yield return scene;
        foreach (var child in scene.Children)
        {
            foreach (var nested in ScenesOf(child))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<Entity> EntitiesOf(IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            yield return entity;
            foreach (var child in EntitiesOf(entity.Transform.Children.Select(t => t.Entity)))
            {
                yield return child;
            }
        }
    }
}
