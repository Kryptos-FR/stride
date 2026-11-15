using ColorRise.Saga.Assets;
using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Assets;
using Stride.Core.Mathematics;
using Stride.Core.Reflection;
using Xunit;

namespace ColorRise.Saga.Tests.Assets;

public sealed class AssetYamlTests
{
    static AssetYamlTests()
    {
        // What the asset compiler and Game Studio do when they load the Assets companion
        AssemblyRegistry.Register(typeof(SagaSystem).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(StoryAsset).Assembly, AssemblyCommonCategories.Assets);
    }

    [Fact]
    public void StoryAssetRoundTripsThroughYaml()
    {
        var story = new StoryAssetFactory().New();
        story.Declarations.Events.Add(new EventDeclaration { Name = "dawn" });
        story.Declarations.Functions.Add(new FunctionDeclaration { Name = "has_item", Parameters = { SagaTypeKind.String } });
        story.Acts[0].Position = new Vector2(10, 20);
        story.Acts[0].Scenarios.Add(new ScenarioCard { Id = "siege", StartWhen = "raised(\"dawn\")", Outcomes = { new ScenarioOutcomeCard { Name = "won" } } });
        story.Acts[0].Quests.Add(new QuestCard { Id = "dig", ScenarioId = "siege", Stages = { new QuestStageCard { Id = "first", DoneWhen = "day > 1" } } });

        var copy = RoundTrip(story, out var yaml);

        Assert.Contains("!SagaStoryAsset", yaml);
        Assert.Equal("act_1", copy.FirstActId);
        Assert.Equal(new Vector2(10, 20), copy.Acts[0].Position);
        Assert.Equal("raised(\"dawn\")", copy.Acts[0].Scenarios[0].StartWhen);
        Assert.Equal("won", copy.Acts[0].Scenarios[0].Outcomes[0].Name);
        Assert.Equal("first", copy.Acts[0].Quests[0].Stages[0].Id);
        Assert.Equal([SagaTypeKind.String], copy.Declarations.Functions[0].Parameters);
        Assert.Equal("\"Hero\"", copy.Declarations.Variables[0].Default);
    }

    [Fact]
    public void DialogueAssetRoundTripsThroughYaml()
    {
        var dialogue = new DialogueAsset
        {
            Story = new AssetReference(AssetId.New(), "Story/Main"),
            CharacterId = "mara",
            EntryNodeId = "hello",
            Nodes =
            {
                new LineNodeCard { Id = "hello", SpeakerId = "mara", Text = "Hi {player_name}.", NextId = "ask", Position = new Vector2(1, 2) },
                new ChoicesNodeCard { Id = "ask", Choices = { new DialogueChoiceCard { Text = "Bye", HideWhenFalse = false, TargetId = "end" } } },
                new BranchNodeCard { Id = "check", Links = { new DialogueLinkCard { When = "true", TargetId = "end" } } },
                new EndNodeCard { Id = "end" },
            },
        };

        var copy = RoundTrip(dialogue, out var yaml);

        Assert.Contains("!SagaDialogueAsset", yaml);
        Assert.Equal("Story/Main", copy.Story?.Location.ToString());
        Assert.Equal("Hi {player_name}.", Assert.IsType<LineNodeCard>(copy.Nodes[0]).Text);
        Assert.Equal(new Vector2(1, 2), copy.Nodes[0].Position);
        Assert.False(Assert.IsType<ChoicesNodeCard>(copy.Nodes[1]).Choices[0].HideWhenFalse);
        Assert.Equal("true", Assert.IsType<BranchNodeCard>(copy.Nodes[2]).Links[0].When);
        Assert.IsType<EndNodeCard>(copy.Nodes[3]);
    }

    private static T RoundTrip<T>(T asset, out string yaml) where T : Asset
    {
        using var stream = new MemoryStream();
        AssetFileSerializer.Save(stream, asset, null);
        yaml = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        stream.Position = 0;
        return AssetFileSerializer.Load<T>(stream, "test" + AssetRegistry.GetDefaultExtension(typeof(T))).Asset;
    }
}
