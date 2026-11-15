using Stride.Core.Diagnostics;

namespace ColorRise.Skein.Tests.Runtime;

/// <summary>
/// Where the variables of the scripts live, and what a game saves and loads.
/// </summary>
public sealed class VariableStorageTests
{
    private const string Purse = """
        title: Start
        ---
        <<declare $gold = 5>>
        <<declare $name = "Odile">>
        <<declare $met_kye = false>>
        <<declare $rich = $gold > 100>>
        ===

        """;

    private static VariableStorage PurseStorage(ILogger? log = null) => new(log) { Program = Scripts.Program(Purse) };

    [Fact]
    public void A_variable_reads_its_default_until_it_is_set()
    {
        var storage = PurseStorage();

        Assert.True(storage.TryGetValue<float>("$gold", out var gold));
        Assert.Equal(5f, gold);
        Assert.True(storage.TryGetValue<string>("$name", out var name));
        Assert.Equal("Odile", name);

        storage.SetValue("$gold", 12f);

        Assert.True(storage.TryGetValue<float>("$gold", out gold));
        Assert.Equal(12f, gold);
        Assert.True(storage.TryGetValue<IConvertible>("$gold", out var any));
        Assert.Equal(12f, any);
        Assert.False(storage.TryGetValue<float>("$unknown", out _));
    }

    [Fact]
    public void A_change_is_told_once_and_only_when_the_value_changes()
    {
        var storage = PurseStorage();
        var changes = new List<(string Name, object? Value)>();
        storage.VariableChanged += (name, value) => changes.Add((name, value));

        storage.SetValue("$gold", 12f);
        storage.SetValue("$gold", 12f);
        storage.SetValue("$name", "Kye");
        storage.SetValue("$met_kye", true);

        Assert.Equal([("$gold", 12f), ("$name", "Kye"), ("$met_kye", true)], changes);
    }

    [Fact]
    public void A_snapshot_loaded_in_a_new_storage_gives_the_same_values()
    {
        var storage = PurseStorage();
        storage.SetValue("$gold", 12f);
        storage.SetValue("$name", "Kye");
        storage.SetValue("$met_kye", true);

        var snapshot = storage.Save();
        var loaded = PurseStorage();
        var changes = new List<string>();
        loaded.VariableChanged += (name, _) => changes.Add(name);
        var dropped = loaded.Load(snapshot);

        Assert.Empty(dropped);
        Assert.True(loaded.TryGetValue<float>("$gold", out var gold));
        Assert.Equal(12f, gold);
        Assert.True(loaded.TryGetValue<string>("$name", out var name));
        Assert.Equal("Kye", name);
        Assert.True(loaded.TryGetValue<bool>("$met_kye", out var met));
        Assert.True(met);
        Assert.Equal(["$gold", "$met_kye", "$name"], changes.Order());
    }

    [Fact]
    public void A_snapshot_is_a_copy()
    {
        var storage = PurseStorage();
        storage.SetValue("$gold", 12f);
        var snapshot = storage.Save();

        storage.SetValue("$gold", 99f);

        Assert.Equal(12f, snapshot.Numbers["$gold"]);
    }

    [Fact]
    public void Loading_puts_back_the_default_of_what_the_snapshot_lacks()
    {
        var storage = PurseStorage();
        var empty = storage.Save();
        storage.SetValue("$gold", 12f);
        var changes = new List<(string Name, object? Value)>();
        storage.VariableChanged += (name, value) => changes.Add((name, value));

        storage.Load(empty);

        Assert.True(storage.TryGetValue<float>("$gold", out var gold));
        Assert.Equal(5f, gold);
        Assert.Equal([("$gold", 5f)], changes);
    }

    [Fact]
    public void What_the_project_no_longer_declares_is_dropped_with_one_warning()
    {
        var log = new LoggerResult();
        var storage = PurseStorage(log);
        var snapshot = new VariableSnapshot();
        snapshot.Numbers["$old"] = 3f;
        snapshot.Strings["$older"] = "x";
        snapshot.Numbers["$gold"] = 7f;

        var dropped = storage.Load(snapshot);

        Assert.Equal(["$old", "$older"], dropped);
        var warning = Assert.Single(log.Messages);
        Assert.Equal(LogMessageType.Warning, warning.Type);
        Assert.Contains("$old", warning.Text);
        Assert.Contains("$older", warning.Text);
        Assert.False(storage.TryGetValue<float>("$old", out _));
        Assert.True(storage.TryGetValue<float>("$gold", out var gold));
        Assert.Equal(7f, gold);
    }

    [Fact]
    public void A_value_of_another_type_than_the_declared_one_is_dropped_and_the_default_kept()
    {
        var storage = PurseStorage(new LoggerResult());
        var snapshot = new VariableSnapshot();
        snapshot.Numbers["$met_kye"] = 1f;

        var dropped = storage.Load(snapshot);

        Assert.Equal(["$met_kye"], dropped);
        Assert.True(storage.TryGetValue<bool>("$met_kye", out var met));
        Assert.False(met);
    }

    [Fact]
    public void A_smart_variable_is_never_in_a_snapshot()
    {
        var storage = PurseStorage(new LoggerResult());
        Assert.Equal(Yarn.VariableKind.Smart, storage.GetVariableKind("$rich"));

        Assert.Throws<ArgumentException>(() => storage.SetValue("$rich", true));
        var stale = new VariableSnapshot();
        stale.Bools["$rich"] = true;
        var dropped = storage.Load(stale);

        Assert.Equal(["$rich"], dropped);
        Assert.Empty(storage.Save().Bools);
    }

    [Fact]
    public void What_the_compiler_keeps_for_itself_survives_a_load_without_a_declaration()
    {
        // How often a saliency strategy has shown a piece of content: Yarn Spinner stores it, no script declares it
        const string ViewCount = "$Yarn.Internal.Content.ViewCount.abc";
        var storage = PurseStorage();
        storage.SetValue(ViewCount, 2f);

        var loaded = PurseStorage();
        var dropped = loaded.Load(storage.Save());

        Assert.Empty(dropped);
        Assert.True(loaded.TryGetValue<float>(ViewCount, out var count));
        Assert.Equal(2f, count);
    }

    [Fact]
    public void A_set_of_another_type_than_the_declared_one_is_refused_by_name()
    {
        var storage = PurseStorage();

        var exception = Assert.Throws<ArgumentException>(() => storage.SetValue("$gold", "many"));

        Assert.Contains("$gold", exception.Message);
        Assert.True(storage.TryGetValue<float>("$gold", out var gold));
        Assert.Equal(5f, gold);
    }

    [Fact]
    public void A_storage_without_a_program_keeps_what_it_is_given()
    {
        var storage = new VariableStorage();
        var snapshot = new VariableSnapshot();
        snapshot.Numbers["$gold"] = 7f;

        var dropped = storage.Load(snapshot);

        Assert.Empty(dropped);
        Assert.True(storage.TryGetValue<float>("$gold", out var gold));
        Assert.Equal(7f, gold);
    }

    [Fact]
    public void A_storage_knows_the_variables_of_every_project_it_serves()
    {
        var storage = PurseStorage();
        storage.Declare(Scripts.Program("title: Other\n---\n<<declare $tide = 2>>\n===\n"));
        var snapshot = new VariableSnapshot();
        snapshot.Numbers["$tide"] = 4f;
        snapshot.Numbers["$gold"] = 7f;

        var dropped = storage.Load(snapshot);

        Assert.Empty(dropped);
        Assert.True(storage.TryGetValue<float>("$tide", out var tide));
        Assert.Equal(4f, tide);
    }

    [Fact]
    public void Clear_puts_every_variable_back_to_its_default()
    {
        var storage = PurseStorage();
        storage.SetValue("$gold", 12f);
        var changes = new List<string>();
        storage.VariableChanged += (name, _) => changes.Add(name);

        storage.Clear();

        Assert.Equal(["$gold"], changes);
        Assert.True(storage.TryGetValue<float>("$gold", out var gold));
        Assert.Equal(5f, gold);
    }

    [Fact]
    public void A_dialogue_of_yarn_spinner_runs_on_the_storage()
    {
        const string Script = """
            title: Start
            ---
            <<declare $gold = 5>>
            <<declare $rich = $gold > 100>>
            <<set $gold = $gold + 200>>
            <<if $rich>>
            Odile: Rich.
            <<endif>>
            ===

            """;
        var compilation = Scripts.Compile(Script);
        var storage = new VariableStorage();
        var lines = new List<string>();
        var dialogue = new Yarn.Dialogue(storage)
        {
            LineHandler = line => lines.Add(compilation.Lines[line.ID].Text),
            CommandHandler = _ => { },
            OptionsHandler = _ => { },
            NodeStartHandler = _ => { },
            NodeCompleteHandler = _ => { },
            DialogueCompleteHandler = () => { },
        };
        dialogue.SetProgram(compilation.Program!);
        dialogue.SetNode("Start");
        for (var steps = 0; steps < 10 && (steps == 0 || dialogue.IsActive); steps++)
        {
            dialogue.Continue();
        }

        Assert.Equal(["Odile: Rich."], lines);
        Assert.Equal(205f, storage.Save().Numbers["$gold"]);
        Assert.DoesNotContain("$rich", storage.Save().Bools.Keys);
    }
}
