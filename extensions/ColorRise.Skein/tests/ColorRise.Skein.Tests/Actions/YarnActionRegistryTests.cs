using ColorRise.Skein.Assets.Compilation;

namespace ColorRise.Skein.Tests.Actions;

/// <summary>
/// The commands and the functions a game gives to its scripts.
/// </summary>
public sealed class YarnActionRegistryTests
{
    private static readonly List<string> Calls = [];

    public YarnActionRegistryTests()
    {
        Calls.Clear();
    }

    private enum Pace
    {
        Slow,
        Fast,
    }

    private sealed class Door
    {
        public bool IsOpen { get; private set; }

        [YarnCommand("open", Description = "Opens the door.")]
        public void Open() => IsOpen = true;

        [YarnCommand("knock")]
        public void Knock(int times, Door? other = null) => Calls.Add($"knock {times} {(other is null ? "alone" : "with another")}");
    }

    private static class Game
    {
        [YarnCommand("give")]
        public static void Give(string item, int count = 1) => Calls.Add($"give {item} {count}");

        [YarnCommand("walk")]
        public static void Walk(string who, Pace pace, bool running = false) => Calls.Add($"walk {who} {pace} {running}");

        [YarnCommand("say_all")]
        public static void SayAll(string who, params string[] words) => Calls.Add($"{who}: {string.Join('|', words)}");

        [YarnCommand("fade")]
        public static async Task Fade(float seconds, CancellationToken cancellation)
        {
            Calls.Add($"fade {seconds} starts");
            await Gate.Task.WaitAsync(cancellation);
            Calls.Add("fade ends");
        }

        [YarnFunction("has_item")]
        public static bool HasItem(string item) => item == "oil";

        [YarnFunction("double_of")]
        public static int DoubleOf(int value) => value * 2;

        public static TaskCompletionSource Gate { get; set; } = new();
    }

    private sealed class Doors(params (string Name, Door Door)[] doors) : IYarnTargetResolver
    {
        public object? Find(string name, Type type)
            => type == typeof(Door) ? doors.FirstOrDefault(d => d.Name == name).Door : null;
    }

    private static YarnActionRegistry Registry(IYarnTargetResolver? targets = null)
    {
        var registry = new YarnActionRegistry { Targets = targets };
        registry.RegisterType(typeof(Game));
        registry.RegisterType(typeof(Door));
        return registry;
    }

    private static async Task<bool> Run(YarnActionRegistry registry, string command)
        => await registry.RunCommandAsync(command, CancellationToken.None);

    [Fact]
    public async Task A_static_command_gets_its_parameters_converted_and_its_defaults()
    {
        var registry = Registry();

        Assert.True(await Run(registry, "give oil 2"));
        Assert.True(await Run(registry, "give oil"));

        Assert.Equal(["give oil 2", "give oil 1"], Calls);
    }

    [Fact]
    public async Task A_command_nothing_handles_is_not_run()
    {
        Assert.False(await Run(Registry(), "dance wildly"));
        Assert.False(await Run(Registry(), "   "));
    }

    [Fact]
    public async Task An_instance_command_runs_on_the_target_its_first_word_names()
    {
        var cellar = new Door();
        var attic = new Door();
        var registry = Registry(new Doors(("CellarDoor", cellar), ("AtticDoor", attic)));

        Assert.True(await Run(registry, "open CellarDoor"));

        Assert.True(cellar.IsOpen);
        Assert.False(attic.IsOpen);
    }

    [Fact]
    public async Task A_parameter_of_a_type_text_does_not_convert_to_is_found_as_a_target_is()
    {
        var registry = Registry(new Doors(("CellarDoor", new Door()), ("AtticDoor", new Door())));

        await Run(registry, "knock CellarDoor 3 AtticDoor");
        await Run(registry, "knock CellarDoor 1");

        Assert.Equal(["knock 3 with another", "knock 1 alone"], Calls);
    }

    [Fact]
    public async Task A_command_that_returns_a_task_is_awaited()
    {
        Game.Gate = new TaskCompletionSource();
        var registry = Registry();

        var running = registry.RunCommandAsync("fade 1.5", CancellationToken.None).AsTask();
        await Task.Yield();

        Assert.False(running.IsCompleted);
        Assert.Equal(["fade 1.5 starts"], Calls);

        Game.Gate.SetResult();
        Assert.True(await running);
        Assert.Equal(["fade 1.5 starts", "fade ends"], Calls);
    }

    [Fact]
    public async Task A_command_gets_the_cancellation_of_the_run()
    {
        Game.Gate = new TaskCompletionSource();
        using var cancellation = new CancellationTokenSource();
        var running = Registry().RunCommandAsync("fade 2", cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(["fade 2 starts"], Calls);
    }

    [Theory]
    [InlineData("say \"two words\"", new[] { "say", "two words" })]
    [InlineData("  walk   Odile\tpier  ", new[] { "walk", "Odile", "pier" })]
    [InlineData("say \"She said \\\"no\\\".\" loud", new[] { "say", "She said \"no\".", "loud" })]
    [InlineData("say \"\" end", new[] { "say", "", "end" })]
    [InlineData("say pre\"fix ed\"post", new[] { "say", "pre", "fix ed", "post" })]
    [InlineData("say \"never closed", new[] { "say", "never closed" })]
    [InlineData("", new string[0])]
    public void A_command_text_splits_into_words_and_quotes_group_words(string text, string[] words)
    {
        Assert.Equal(words, YarnCommandText.Split(text));
    }

    [Fact]
    public async Task A_params_array_takes_the_rest()
    {
        var registry = Registry();

        await Run(registry, "say_all Odile one \"two words\" three");
        await Run(registry, "say_all Kye");

        Assert.Equal(["Odile: one|two words|three", "Kye: "], Calls);
    }

    [Fact]
    public async Task An_enum_converts_whatever_its_case_and_a_bool_from_its_own_name()
    {
        var registry = Registry();

        await Run(registry, "walk Odile fast");
        await Run(registry, "walk Odile SLOW true");
        await Run(registry, "walk Odile Slow running");

        Assert.Equal(["walk Odile Fast False", "walk Odile Slow True", "walk Odile Slow True"], Calls);
    }

    [Theory]
    [InlineData("give oil many", "count")]
    [InlineData("give", "item")]
    [InlineData("give oil 2 extra", "extra")]
    [InlineData("walk Odile sideways", "sideways")]
    [InlineData("open", "target")]
    [InlineData("open GardenDoor", "GardenDoor")]
    [InlineData("knock CellarDoor 2 GardenDoor", "GardenDoor")]
    public async Task A_command_that_cannot_be_called_says_which_and_why(string command, string reason)
    {
        var registry = Registry(new Doors(("CellarDoor", new Door())));

        var exception = await Assert.ThrowsAsync<YarnCommandException>(() => Run(registry, command));

        Assert.Contains($"<<{command}>>", exception.Message);
        Assert.Contains(reason, exception.Message);
        Assert.Equal(command, exception.CommandText);
        Assert.Empty(Calls);
    }

    [Fact]
    public async Task An_instance_command_without_a_resolver_says_so()
    {
        var exception = await Assert.ThrowsAsync<YarnCommandException>(() => Run(Registry(), "open CellarDoor"));

        Assert.Contains("CellarDoor", exception.Message);
    }

    [Fact]
    public async Task What_a_command_throws_comes_out_as_thrown()
    {
        var registry = new YarnActionRegistry();
        registry.AddCommandHandler("fail", (Action)(() => throw new InvalidOperationException("The lamp is out.")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(registry, "fail"));

        Assert.Equal("The lamp is out.", exception.Message);
    }

    [Fact]
    public async Task A_handler_added_by_hand_runs_and_can_be_removed()
    {
        var registry = new YarnActionRegistry();
        var door = new Door();
        registry.AddCommandHandler("unlock", (string key, int turns) => Calls.Add($"unlock {key} {turns}"));
        registry.AddCommandHandler("open_cellar", door.Open);
        registry.AddCommandHandler("pause", async (float seconds) => { await Task.Yield(); Calls.Add($"pause {seconds}"); });

        Assert.True(await Run(registry, "unlock brass 2"));
        Assert.True(await Run(registry, "open_cellar"));
        Assert.True(await Run(registry, "pause 0.5"));
        Assert.True(registry.RemoveCommandHandler("unlock"));
        Assert.False(await Run(registry, "unlock brass 2"));

        Assert.Equal(["unlock brass 2", "pause 0.5"], Calls);
        Assert.True(door.IsOpen);
    }

    private static class Twice
    {
        public static void GiveAgain(string item)
        {
        }
    }

    [Fact]
    public void Two_handlers_of_one_name_are_refused_naming_both()
    {
        var registry = Registry();

        var exception = Assert.Throws<InvalidOperationException>(() => registry.AddCommandHandler("give", Twice.GiveAgain));

        Assert.Contains("give", exception.Message);
        Assert.Contains("Game.Give", exception.Message);
        Assert.Contains("Twice.GiveAgain", exception.Message);
    }

    [Fact]
    public void An_assembly_gives_its_attributed_methods_and_registering_them_again_changes_nothing()
    {
        var registry = new YarnActionRegistry();

        registry.RegisterAssembly(typeof(YarnActionRegistryTests).Assembly);
        var count = registry.Describe().Count;
        registry.RegisterAssembly(typeof(YarnActionRegistryTests).Assembly);
        registry.RegisterType(typeof(Game));

        Assert.Equal(count, registry.Describe().Count);
        Assert.Contains(registry.Describe(), d => d.Name == "give");
        Assert.Contains(registry.Describe(), d => d.Name == "open");
        Assert.Contains(registry.Describe(), d => d.Name == "has_item");
    }

    [Fact]
    public void A_function_describes_its_name_its_parameters_and_its_result()
    {
        var hasItem = Assert.Single(Registry().Describe(), d => d.Name == "has_item");

        Assert.Equal(YarnActionKind.Function, hasItem.Kind);
        var parameter = Assert.Single(hasItem.Parameters);
        Assert.Equal(("item", "string"), (parameter.Name, parameter.Type));
        Assert.False(parameter.IsOptional);
        Assert.Equal("bool", hasItem.ReturnType);
        Assert.Contains("Game.HasItem", hasItem.Method);
    }

    [Fact]
    public void A_command_describes_its_target_its_defaults_and_whether_the_dialogue_waits()
    {
        var descriptions = Registry().Describe();

        var open = Assert.Single(descriptions, d => d.Name == "open");
        Assert.Equal(YarnActionKind.Command, open.Kind);
        Assert.Equal("Door", open.TargetType);
        Assert.Equal("Opens the door.", open.Documentation);
        Assert.Null(open.ReturnType);

        var give = Assert.Single(descriptions, d => d.Name == "give");
        Assert.Null(give.TargetType);
        Assert.Equal([("item", "string", false, (string?)null), ("count", "number", true, "1")], give.Parameters.Select(p => (p.Name, p.Type, p.IsOptional, p.DefaultValue)));
        Assert.False(give.IsAsync);

        var fade = Assert.Single(descriptions, d => d.Name == "fade");
        Assert.True(fade.IsAsync);
        // The cancellation is not a word of the command
        Assert.Equal(["seconds"], fade.Parameters.Select(p => p.Name));

        var sayAll = Assert.Single(descriptions, d => d.Name == "say_all");
        Assert.True(sayAll.Parameters[1].IsParams);

        var walk = Assert.Single(descriptions, d => d.Name == "walk");
        Assert.Equal(["Slow", "Fast"], walk.Parameters[1].Choices);
        Assert.Equal("false", walk.Parameters[2].DefaultValue);
    }

    [Fact]
    public void Functions_go_to_the_library_of_a_dialogue()
    {
        var library = new Yarn.Library();
        var registry = Registry();
        registry.AddFunction("tide", () => 3f);

        registry.CopyFunctionsTo(library);
        registry.CopyFunctionsTo(library);

        Assert.Equal(true, library.GetFunction("has_item").DynamicInvoke("oil"));
        Assert.Equal(4, library.GetFunction("double_of").DynamicInvoke(2));
        Assert.Equal(3f, library.GetFunction("tide").DynamicInvoke());
    }

    private static class Bad
    {
        public static void NotStatic()
        {
        }
    }

    [Fact]
    public void A_function_that_scripts_cannot_call_is_refused_by_name()
    {
        var registry = new YarnActionRegistry();

        var result = Assert.Throws<ArgumentException>(() => registry.AddFunction("when", () => DateTime.Now));
        Assert.Contains("when", result.Message);
        Assert.Contains("DateTime", result.Message);

        var parameter = Assert.Throws<ArgumentException>(() => registry.AddFunction("near", (Door door) => true));
        Assert.Contains("near", parameter.Message);
        Assert.Contains("door", parameter.Message);

        var nothing = Assert.Throws<ArgumentException>(() => registry.AddFunction("nothing", Bad.NotStatic));
        Assert.Contains("nothing", nothing.Message);

        var command = Assert.Throws<ArgumentException>(() => registry.AddCommandHandler("count", () => 3));
        Assert.Contains("count", command.Message);

        Assert.Throws<ArgumentException>(() => registry.AddCommandHandler("two words", () => { }));
    }

    [Fact]
    public async Task A_function_runs_in_a_script()
    {
        const string Script = """
            title: Start
            ---
            <<if has_item("oil") and double_of(2) == 4>>
            Odile: Light it.
            <<endif>>
            ===

            """;
        var registry = Registry();
        var compilation = Scripts.Compile(Script, [.. registry.Describe().Where(d => d.Kind == YarnActionKind.Function).Select(YarnFunctionDeclaration.From)]);
        var lines = new List<string>();
        var dialogue = new Yarn.Dialogue(new VariableStorage())
        {
            LineHandler = line => lines.Add(compilation.Lines[line.ID].Text),
            CommandHandler = _ => { },
            OptionsHandler = _ => { },
            NodeStartHandler = _ => { },
            NodeCompleteHandler = _ => { },
            DialogueCompleteHandler = () => { },
        };
        registry.CopyFunctionsTo(dialogue.Library);
        dialogue.SetProgram(compilation.Program!);
        dialogue.SetNode("Start");
        for (var steps = 0; steps < 10 && (steps == 0 || dialogue.IsActive); steps++)
        {
            dialogue.Continue();
        }

        await Task.CompletedTask;
        Assert.Equal(["Odile: Light it."], lines);
    }
}
