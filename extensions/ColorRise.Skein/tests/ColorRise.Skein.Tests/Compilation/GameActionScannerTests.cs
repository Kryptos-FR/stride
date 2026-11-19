using ColorRise.Skein.Assets.Compilation;

namespace ColorRise.Skein.Tests.Compilation;

/// <summary>
/// The build and Game Studio learn the commands and the functions of a game from its assemblies, without running it.
/// </summary>
public sealed class GameActionScannerTests
{
    [Fact]
    public void An_assembly_gives_its_commands_and_its_functions_with_their_parameters()
    {
        var actions = GameActionScanner.Scan([typeof(GameActionScannerTests).Assembly, typeof(GameActionScannerTests).Assembly, typeof(string).Assembly]);

        Assert.Empty(actions.Problems);

        var open = Assert.Single(actions.Actions, a => a.Name == "open");
        Assert.Equal(YarnActionKind.Command, open.Kind);
        Assert.Equal("Door", open.TargetType);
        Assert.Equal("Opens the door.", open.Documentation);

        var hasItem = Assert.Single(actions.Actions, a => a.Name == "has_item");
        Assert.Equal(YarnActionKind.Function, hasItem.Kind);
        Assert.Equal("bool", hasItem.ReturnType);
        var item = Assert.Single(hasItem.Parameters);
        Assert.Equal(("item", "string"), (item.Name, item.Type));

        var declared = Assert.Single(actions.Functions, f => f.Name == "has_item");
        Assert.Equal([YarnFunctionDeclaration.String], declared.ParameterTypes);
        Assert.Equal(YarnFunctionDeclaration.Bool, declared.ReturnType);
        Assert.Equal(actions.Actions.Count(a => a.Kind == YarnActionKind.Function), actions.Functions.Count);
    }

    [Fact]
    public void A_method_that_cannot_be_a_function_is_a_problem_that_names_it_and_the_others_are_found()
    {
        using var game = new GeneratedGame(
            "BrokenGame",
            ("Where", typeof(Guid), typeof(YarnFunctionAttribute), "where", []),
            ("Go", typeof(int), typeof(YarnCommandAttribute), "go", []),
            ("Count", typeof(int), typeof(YarnFunctionAttribute), "place_count", [("region", typeof(string))]));

        var actions = GameActionScanner.Scan([game.Assembly]);

        Assert.Equal(
            [
                "The command 'go' (Places.Go) returns a Int32: a command returns nothing, or a task the dialogue waits for.",
                "The function 'where' (Places.Where) returns a Guid: a function returns a boolean, a number or a string, or a task of one.",
            ],
            actions.Problems.Order(StringComparer.Ordinal));
        var count = Assert.Single(actions.Actions);
        Assert.Equal("place_count", count.Name);
        Assert.Equal("region", Assert.Single(count.Parameters).Name);
        Assert.Equal("number", Assert.Single(actions.Functions).ReturnType);
    }

    [Fact]
    public void An_assembly_that_does_not_reference_Skein_is_not_read()
    {
        Assert.Empty(GameActionScanner.Scan([]).Actions);
        Assert.Empty(GameActionScanner.Scan([typeof(string).Assembly, typeof(FactAttribute).Assembly]).Actions);
    }
}
