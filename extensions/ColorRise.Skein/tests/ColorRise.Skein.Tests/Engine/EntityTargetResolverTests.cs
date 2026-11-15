using Stride.Core;
using Stride.Engine;

namespace ColorRise.Skein.Tests.Engine;

/// <summary>
/// Something a script can switch on: <c>&lt;&lt;light Lamp&gt;&gt;</c>.
/// </summary>
[DataContract]
public sealed class LampComponent : EntityComponent
{
    public bool IsLit { get; private set; }

    [YarnCommand("light")]
    public void Light() => IsLit = true;
}

/// <summary>
/// A command finds what it names: actors first, then entities by name, the runner's scene first.
/// </summary>
[Collection(GameCollection.Name)]
public sealed class EntityTargetResolverTests
{
    [Fact]
    public void An_actor_is_found_before_an_entity_of_that_name()
    {
        using var harness = new GameHarness();
        var origin = harness.Add("Runner");
        var named = harness.Add("Odile");
        var actor = harness.Add("NPC_3", new DialogueActorComponent { ActorName = "Odile" });
        var targets = new EntityTargetResolver(origin);

        Assert.Same(actor, targets.Find("Odile", typeof(Entity)));
        Assert.Same(actor.Get<DialogueActorComponent>(), targets.Find("Odile", typeof(DialogueActorComponent)));

        // An actor that left the scene is not found any more: the entity of that name is
        harness.Scene.Entities.Remove(actor);
        Assert.Same(named, targets.Find("Odile", typeof(Entity)));
    }

    [Fact]
    public void An_actor_without_a_name_has_its_entity_s()
    {
        using var harness = new GameHarness();
        var origin = harness.Add("Runner");
        var actor = harness.Add("Kye", new DialogueActorComponent());

        Assert.Equal("Kye", actor.Get<DialogueActorComponent>().EffectiveName);
        Assert.Same(actor, new EntityTargetResolver(origin).Find("Kye", typeof(Entity)));
        Assert.Same(actor.Get<DialogueActorComponent>(), actor.EntityManager.GetProcessor<DialogueActorProcessor>().Find("Kye"));
        Assert.Null(actor.EntityManager.GetProcessor<DialogueActorProcessor>().Find("Odile"));
    }

    [Fact]
    public void A_component_is_found_on_the_first_entity_of_the_name_that_has_one()
    {
        using var harness = new GameHarness();
        var origin = harness.Add("Runner");
        var bare = harness.Add("Lamp");
        var parent = harness.Add("Table");
        var lamp = new Entity("Lamp") { new LampComponent() };
        parent.AddChild(lamp);
        var targets = new EntityTargetResolver(origin);

        Assert.Same(lamp.Get<LampComponent>(), targets.Find("Lamp", typeof(LampComponent)));
        Assert.Same(bare.Transform, targets.Find("Lamp", typeof(TransformComponent)));
        Assert.Same(bare, targets.Find("Lamp", typeof(Entity)));
        Assert.Null(targets.Find("Lamp", typeof(DialogueActorComponent)));
        Assert.Null(targets.Find("Lantern", typeof(Entity)));
    }

    [Fact]
    public void The_scene_of_the_runner_comes_before_the_other_scenes()
    {
        using var harness = new GameHarness();
        var room = new Scene();
        var cellar = new Scene();
        harness.Scene.Children.Add(room);
        harness.Scene.Children.Add(cellar);
        var rootDoor = harness.Add("Door");
        var origin = new Entity("Runner");
        var roomDoor = new Entity("Door");
        var cellarHatch = new Entity("Hatch");
        room.Entities.Add(origin);
        room.Entities.Add(roomDoor);
        cellar.Entities.Add(cellarHatch);
        var targets = new EntityTargetResolver(origin);

        Assert.Same(roomDoor, targets.Find("Door", typeof(Entity)));
        Assert.Same(cellarHatch, targets.Find("Hatch", typeof(Entity)));

        room.Entities.Remove(roomDoor);
        Assert.Same(rootDoor, targets.Find("Door", typeof(Entity)));
    }
}
