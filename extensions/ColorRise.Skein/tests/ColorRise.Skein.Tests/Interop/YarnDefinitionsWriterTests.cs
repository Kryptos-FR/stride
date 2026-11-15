using System.Text.Json.Nodes;
using ColorRise.Skein.Assets.Interop;

namespace ColorRise.Skein.Tests.Interop;

/// <summary>
/// The definitions file tells Yarn Spinner's tools the commands and functions of the game.
/// </summary>
public sealed class YarnDefinitionsWriterTests
{
    private static readonly YarnActionDescription[] Actions =
    [
        new()
        {
            Name = "open",
            Kind = YarnActionKind.Command,
            TargetType = "Door",
            IsAsync = true,
            Documentation = "Opens a door.",
            Method = "Door.Open",
            Parameters =
            [
                new() { Name = "speed", Type = "string", Choices = ["Slow", "Fast"], IsOptional = true, DefaultValue = "Slow" },
                new() { Name = "sounds", Type = "string", IsParams = true },
            ],
        },
        new()
        {
            Name = "has_item",
            Kind = YarnActionKind.Function,
            ReturnType = "bool",
            Method = "Inventory.HasItem",
            Parameters = [new() { Name = "item", Type = "string" }],
        },
        new() { Name = "give", Kind = YarnActionKind.Command, Method = "Inventory.Give", Parameters = [new() { Name = "who", Type = "any", IsOptional = true }] },
    ];

    [Fact]
    public void A_function_is_listed_with_its_parameters_and_what_it_returns()
    {
        var root = JsonNode.Parse(YarnDefinitionsWriter.Write(Actions))!;

        Assert.Equal(2, root["version"]!.GetValue<int>());
        var function = Assert.Single(root["functions"]!.AsArray())!;
        Assert.Equal(
            """{"yarnName":"has_item","definitionName":"Inventory.HasItem","language":"csharp","async":false,"parameters":[{"name":"item","type":"string"}],"return":{"type":"bool"}}""",
            function.ToJsonString());
    }

    [Fact]
    public void Commands_come_by_name_with_the_object_they_run_on_as_their_first_parameter()
    {
        var commands = JsonNode.Parse(YarnDefinitionsWriter.Write(Actions))!["commands"]!.AsArray();

        Assert.Equal(["give", "open"], commands.Select(c => c!["yarnName"]!.GetValue<string>()));
        Assert.Equal("""[{"name":"who","type":"any","defaultValue":""}]""", commands[0]!["parameters"]!.ToJsonString());

        var open = commands[1]!;
        Assert.True(open["async"]!.GetValue<bool>());
        Assert.Equal("Opens a door.", open["documentation"]!.GetValue<string>());
        Assert.Equal(
            """[{"name":"target","type":"instance","subtype":"Door","documentation":"The name of the Door the command runs on."},{"name":"speed","type":"string","documentation":"One of: Slow, Fast.","defaultValue":"Slow"},{"name":"sounds","type":"string","isParamsArray":true}]""",
            open["parameters"]!.ToJsonString());
    }

    [Fact]
    public void A_game_without_actions_gives_a_file_with_empty_lists()
    {
        Assert.Equal(
            """
            {
                "version": 2,
                "commands": [],
                "functions": []
            }

            """.ReplaceLineEndings("\n"),
            YarnDefinitionsWriter.Write([]).ReplaceLineEndings("\n"));
    }
}
