namespace ColorRise.Skein.Tests.Engine;

/// <summary>
/// The dialogue system is the game's own: its commands, its variables, its language.
/// </summary>
[Collection(GameCollection.Name)]
public sealed class DialogueSystemTests
{
    [Fact]
    public void A_game_makes_its_dialogue_system_with_the_commands_and_functions_of_its_assemblies()
    {
        using var harness = new GameHarness();

        // Declared with [GameSystem]: the game made it, the runners did not have to
        var system = harness.Game.Services.GetService<DialogueSystem>();
        Assert.NotNull(system);

        // The assembly of these tests is one of the game's: Stride registered it as it loaded
        var actions = system.Actions.Describe();
        Assert.Contains(actions, a => a.Name == "has_item" && a.Kind == YarnActionKind.Function);
        Assert.Contains(actions, a => a.Name == "open" && a.Kind == YarnActionKind.Command);
        Assert.Empty(harness.Logged(Stride.Core.Diagnostics.LogMessageType.Error));
    }

    [Fact]
    public void The_lines_of_a_project_follow_the_language_of_the_game()
    {
        using var harness = new GameHarness();
        var project = new YarnProject { BaseLanguage = "en" };
        var changes = new List<string>();
        harness.Dialogues.LanguageChanged += changes.Add;

        var lines = harness.Dialogues.GetLineProvider(project);
        Assert.Equal("en", lines.Language);

        harness.Dialogues.Language = "fr";
        harness.Dialogues.Language = "fr";
        Assert.Equal("fr", lines.Language);
        Assert.Same(lines, harness.Dialogues.GetLineProvider(project));
        Assert.Equal("fr", harness.Dialogues.GetLineProvider(new YarnProject { BaseLanguage = "de" }).Language);

        harness.Dialogues.Language = "";
        Assert.Equal("en", lines.Language);
        Assert.Equal(["fr", ""], changes);
    }
}
