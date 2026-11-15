using System.Reflection;
using ColorRise.Saga.Content;
using Stride.Core;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Engine;
using Xunit;

namespace ColorRise.Saga.Tests.Runtime;

public sealed class SagaSystemTests
{
    [Fact]
    public void LoadReplacesRuntime()
    {
        var system = new SagaSystem(new ServiceRegistry());
        Assert.Null(system.Runtime);

        var first = system.Load(Story("act_1"));
        Assert.Same(first, system.Runtime);
        Assert.Equal("act_1", first.State.CurrentActId);

        var saved = first.CaptureState();
        var second = system.Load(Story("act_1"), saved);
        Assert.Same(second, system.Runtime);
        Assert.NotSame(first, second);
        Assert.Same(saved, second.State);
        Assert.Equal("act_1", second.State.CurrentActId);
    }

    [Fact]
    public void CharacterWithoutSystemHasNothingToSay()
    {
        var character = new SagaCharacterComponent { CharacterId = "mara" };
        _ = new Entity { character };

        Assert.Null(character.Talk());
    }

    [Fact]
    public void CharacterComponentHasAnIcon()
    {
        var assembly = typeof(ColorRise.Saga.Assets.EditorImages).Assembly;

        var image = Assert.Single(assembly.GetCustomAttributes<TypeImageAttribute>(), x => x.Type == typeof(SagaCharacterComponent));
        Assert.Contains(assembly.GetManifestResourceNames(), x => x.EndsWith(image.ResourceName, StringComparison.Ordinal));
    }

    private static StoryDefinition Story(string firstAct) => new StoryBuilder()
        .Character("mara")
        .Act(new ActDefinition { Id = firstAct })
        .Build();
}
