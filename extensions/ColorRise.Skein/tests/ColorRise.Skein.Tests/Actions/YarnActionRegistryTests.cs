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

    private sealed record Tool(string Name);

    private static class Tools
    {
        [YarnConverter(typeof(Tool))]
        public static bool TryTool(string text, out Tool tool)
        {
            tool = new Tool(text.ToUpperInvariant());
            return text != "spoon";
        }

        [YarnCommand("dig")]
        public static void Dig(Tool tool, params Tool[] spares) => Calls.Add($"dig {tool.Name} {string.Join('|', spares.Select(s => s.Name))}");
    }

    // Generic, so that registering the whole assembly skips it
    private static class BadConverters<TUnused>
    {
        [YarnConverter(typeof(Tool))]
        public static Tool NoTry(string text) => new(text);
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
    public async Task A_run_can_find_its_targets_its_own_way()
    {
        var cellar = new Door();
        var attic = new Door();
        var registry = Registry(new Doors(("Door", cellar)));

        Assert.True(await registry.RunCommandAsync("open Door", new Doors(("Door", attic)), CancellationToken.None));
        Assert.True(attic.IsOpen);
        Assert.False(cellar.IsOpen);

        // Without its own way, the registry's
        Assert.True(await registry.RunCommandAsync("open Door", targets: null, CancellationToken.None));
        Assert.True(cellar.IsOpen);
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
    [InlineData("walk Odile 1", "walk Odile Fast False")]
    [InlineData("walk Odile 0 running", "walk Odile Slow True")]
    public async Task An_enum_takes_the_number_of_one_of_its_cases(string command, string call)
    {
        // What <<walk Odile {$pace}>> gives when $pace is of a Yarn enum
        await Run(Registry(), command);

        Assert.Equal([call], Calls);
    }

    [Theory]
    [InlineData("give oil many", "count")]
    [InlineData("give", "item")]
    [InlineData("give oil 2 extra", "extra")]
    [InlineData("walk Odile sideways", "sideways")]
    [InlineData("walk Odile 2", "Slow, Fast")]
    [InlineData("walk Odile 0.5", "Slow, Fast")]
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
    public async Task A_function_is_called_with_the_values_of_a_script()
    {
        var registry = Registry();
        registry.AddFunction("tide", () => 3f);
        registry.AddFunction("sum", (int first, params float[] rest) => first + rest.Sum());

        Assert.True(registry.HasFunction("tide"));
        Assert.False(registry.HasFunction("ebb"));
        Assert.Equal(true, await registry.InvokeFunctionAsync("has_item", [(Yarn.Value)"oil"]));
        // A number of any type comes back as a float
        Assert.Equal(4f, await registry.InvokeFunctionAsync("double_of", [(Yarn.Value)2f]));
        Assert.Equal(3f, await registry.InvokeFunctionAsync("tide", []));
        Assert.Equal(6f, await registry.InvokeFunctionAsync("sum", [(Yarn.Value)1f, (Yarn.Value)2f, (Yarn.Value)3f]));

        Assert.Contains("ebb", (await Assert.ThrowsAsync<ArgumentException>(() => registry.InvokeFunctionAsync("ebb", []).AsTask())).Message);
        Assert.Contains("takes 0 parameters", (await Assert.ThrowsAsync<ArgumentException>(() => registry.InvokeFunctionAsync("tide", [(Yarn.Value)1f]).AsTask())).Message);
    }

    private static class Weather
    {
        [YarnFunction("tide_at")]
        public static async Task<int> TideAt(string place, CancellationToken cancellation)
        {
            await Task.Delay(1, cancellation);
            return place.Length;
        }

        [YarnFunction("wind")]
        public static ValueTask<float> Wind() => new(3f);
    }

    [Fact]
    public async Task A_function_may_return_a_task_and_take_the_cancellation_of_the_dialogue()
    {
        var registry = new YarnActionRegistry();
        registry.RegisterType(typeof(Weather));

        // Scripts see the result of the task, and no parameter for the cancellation
        var tide = Assert.Single(registry.Describe(), d => d.Name == "tide_at");
        Assert.Equal(("number", true), (tide.ReturnType, tide.IsAsync));
        Assert.Equal(["place"], tide.Parameters.Select(p => p.Name));
        Assert.Equal(new YarnFunctionDeclaration { Name = "tide_at", ParameterTypes = ["string"], ReturnType = "number" }.ParameterTypes, YarnFunctionDeclaration.From(tide).ParameterTypes);

        Assert.Equal(4f, await registry.InvokeFunctionAsync("tide_at", [(Yarn.Value)"pier"]));
        // A task that is done answers at once
        var wind = registry.InvokeFunctionAsync("wind", []);
        Assert.True(wind.IsCompletedSuccessfully);
        Assert.Equal(3f, wind.Result);

        using var stopped = new CancellationTokenSource();
        await stopped.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.InvokeFunctionAsync("tide_at", [(Yarn.Value)"pier"], stopped.Token).AsTask());

        // A task of something scripts do not have is refused by name
        Assert.Contains("lamp", Assert.Throws<ArgumentException>(() => registry.AddFunction("lamp", () => Task.FromResult(DateTime.Now))).Message);
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
    public async Task A_converter_makes_a_parameter_of_its_type_from_a_word()
    {
        var registry = new YarnActionRegistry();
        registry.RegisterType(typeof(Tools));

        Assert.True(await Run(registry, "dig shovel pick rake"));
        Assert.Equal(["dig SHOVEL PICK|RAKE"], Calls);

        var refused = await Assert.ThrowsAsync<YarnCommandException>(() => Run(registry, "dig spoon"));
        Assert.Contains("'spoon' is not a Tool for the parameter 'tool'", refused.Message);

        // Scripts write a word: the tools of Yarn Spinner see a string, the editors the type
        var tool = registry.Describe().Single(d => d.Name == "dig").Parameters[0];
        Assert.Equal(("string", "Tool"), (tool.Type, tool.TypeName));
    }

    [Fact]
    public async Task A_converter_added_by_hand_wins_over_the_reading_of_an_enum()
    {
        var registry = Registry();
        registry.AddConverter<Pace>((string text, out Pace pace) =>
        {
            pace = text == "briskly" ? Pace.Fast : Pace.Slow;
            return text is "briskly" or "lazily";
        });

        Assert.True(await Run(registry, "walk Odile briskly"));
        Assert.Equal(["walk Odile Fast False"], Calls);
        await Assert.ThrowsAsync<YarnCommandException>(() => Run(registry, "walk Odile fast"));

        // One converter for a type
        var twice = Assert.Throws<InvalidOperationException>(() => registry.AddConverter<Pace>((string text, out Pace pace) => Enum.TryParse(text, out pace)));
        Assert.Contains("Pace", twice.Message);
        Assert.True(registry.RemoveConverter(typeof(Pace)));
        Assert.True(await Run(registry, "walk Odile fast"));
    }

    [Fact]
    public void A_converter_that_does_not_try_is_refused_by_name()
    {
        var result = Assert.Throws<ArgumentException>(() => new YarnActionRegistry().RegisterType(typeof(BadConverters<int>)));
        Assert.Contains("NoTry", result.Message);
        Assert.Contains("out", result.Message);
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
        var recorder = new LineRecorder(registry);
        var dialogue = new Yarn.Dialogue(new VariableStorage()) { Responder = recorder, Program = compilation.Program };
        await dialogue.StartDialogue("Start");

        Assert.Equal(["Odile: Light it."], recorder.LineIds.Select(id => compilation.Lines[id].Text));
    }
}
