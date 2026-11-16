# Skein for programmers

How to put Yarn Spinner dialogue in a Stride game with Skein: the packages, the dialogue runner, the presenters, commands and functions in C#, variables and saves, languages, and the limits. The writers' side (the script editor, the dialogue player, line ids and translations) is in [guide-writers.md](guide-writers.md).

The sample game `sample/Brinemoor` uses everything this guide describes. The C# of this guide is compiled with Skein's tests (`tests/ColorRise.Skein.Tests/Guide/DeveloperGuideSamples.cs`), and its Yarn is compiled too.

## 1. Add Skein to a game

A game references two packages, in the project that holds its assets (the one beside the `.sdpkg`):

```xml
<ItemGroup>
  <PackageReference Include="ColorRise.Skein" Version="0.1.0" />
  <PackageReference Include="ColorRise.Skein.UI" Version="0.1.0" />
</ItemGroup>
```

- `ColorRise.Skein` is the runtime. Every game that plays Yarn dialogue needs it.
- `ColorRise.Skein.UI` has presenters made with Stride's UI: a line presenter with a typewriter, an options presenter, a line advancer. Leave it out when the game shows its dialogue its own way.

A game never references `ColorRise.Skein.Assets`, `ColorRise.Skein.Editor` or `ColorRise.Skein.Editor.Avalonia`. The runtime declares them as its companions: the asset compiler loads the first one when it builds the game, and Game Studio loads all of them when it opens the game. Both restore them by themselves, from the package sources of the game's `nuget.config`.

No released Stride has the plugin model Skein uses yet. Until one does, a game needs the packages of a local engine build and of a local `dotnet pack` of Skein, so its `nuget.config` names the two folders:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="stride-dev" value="<stride checkout>/bin/packages" />
    <add key="skein" value="<stride checkout>/extensions/ColorRise.Skein/bin/packages" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="stride-dev">
      <package pattern="Stride" />
      <package pattern="Stride.*" />
    </packageSource>
    <packageSource key="skein">
      <package pattern="ColorRise.Skein" />
      <package pattern="ColorRise.Skein.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

`dotnet pack extensions/ColorRise.Skein/ColorRise.Skein.slnx` makes Skein's packages. A game in a folder outside the Stride checkout, with only these two references, restores, builds its Yarn project and plays it.

## 2. Scripts and the Yarn project

- A `.yarn` file in an asset folder of the game is a **Yarn script** asset. It is a plain Yarn Spinner file.
- A **Yarn project** asset (`.sdyarn`) says which scripts make one story (glob patterns from its own folder, `**/*.yarn` by default), the language they are written in, and the other languages. *Add asset > Dialogue > Yarn project* makes one with a first script.
- Building the game compiles the project. An error in a script fails the build with the file, the line and Yarn Spinner's code. The compiled project is the content a dialogue runner plays (`YarnProject`), with one string table per language.
- A project is built when something of the game references it: a dialogue runner of a scene, or the list of root assets of the package.

The build knows the functions of the game (section 5): a script that calls one with the wrong types fails the build at its line.

## 3. The dialogue runner

`DialogueRunnerComponent` plays the nodes of a project. *Add entity > Dialogue > Dialogue runner* in the scene editor makes an entity with a runner and, when the game references `ColorRise.Skein.UI`, the three presenters.

| Property | What it does |
|---|---|
| Project | The Yarn project whose nodes the runner plays. |
| Start Node, Auto Start | The node (or node group) to play when the scene starts. |
| Presenters | Who shows the dialogue. A line goes to all of them at once, and the dialogue goes on when all are done. |
| Saliency | How to pick among the nodes of a node group and the lines of a line group. Empty: what the project says. |
| Options Fall Through | When no option of a list can be chosen, skip the list and go on after it. Off: the presenters get the list all the same, and the dialogue waits for one of them to answer. |

A script starts a dialogue with `StartDialogue`, which takes a node or a node group:

```csharp
public sealed class Innkeeper : SyncScript
{
    public DialogueRunnerComponent? Runner { get; set; }

    public override void Update()
    {
        // The task of StartDialogue completes when the dialogue is over
        if (Runner is { IsRunning: false } && Input.IsKeyPressed(Keys.T))
            _ = Runner.StartDialogue("Tam");
    }
}
```

A runner plays one dialogue at a time: `StartDialogue` throws while `IsRunning` is true. `Stop()` ends the dialogue at once. The runner tells what happens with events (`DialogueStarted`, `DialogueCompleted`, `NodeStarted`, `NodeCompleted`, `UnhandledCommand`), and `CurrentNode` is the node it is in.

```csharp
public sealed class Opening : AsyncScript
{
    public DialogueRunnerComponent? Runner { get; set; }

    public override async Task Execute()
    {
        if (Runner is null)
            return;

        Runner.NodeStarted += node => Log.Info($"The story enters {node}");
        Runner.UnhandledCommand += command => Log.Warning($"The story asks for <<{command}>>");

        await Runner.StartDialogue("Opening");
        Log.Info("The opening is over");
    }
}
```

A dialogue runs on the game's thread, as a micro-thread of the script system: presenters and commands are ordinary `async` code, and can `await Script.NextFrame()`.

**When something fails.** An exception of a presenter, of a command (other than `YarnCommandException`, section 5) or of the story ends the dialogue: the presenters are told the end, `DialogueCompleted` is raised, the log has the error, and the task of `StartDialogue` fails with it. The game goes on, and the runner can start another dialogue.

**Several runners** can play at once. Brinemoor has two: the story, and the fishers who talk among themselves on the pier (`Chatter`), whose lines are captions that never stop the player. Runners share the variables of the game unless one is given its own (`VariablesOverride`).

**Node groups from code.** `HasSalientContent(group)` tells whether a node group has a node whose conditions are all true now, and `NodeExists(name)` whether the project has a node. Brinemoor's story moves on with a node group named `Events`: each event is a node with a `when:` header, and the game plays the group whenever it has something to say.

```csharp
public sealed class Events : SyncScript
{
    public DialogueRunnerComponent? Runner { get; set; }

    public override void Update()
    {
        // The node group "Events" plays whenever one of its nodes has all its conditions true
        if (Runner is { IsRunning: false } && Runner.HasSalientContent("Events"))
            _ = Runner.StartDialogue("Events");
    }
}
```

```yarn
title: Events
when: once
when: $lamp_lit
---
Far out, a ship turns toward the light.
===
```

An event must make its own condition false, or be `when: once`: the game above would play it again each frame.

## 4. Presenters

A presenter shows the dialogue. A runner can have several, each doing one thing.

### The presenters of `ColorRise.Skein.UI`

| Component | What it does | Main settings |
|---|---|---|
| Dialogue line presenter (`LinePresenter`) | The speaker's name and the text, typed little by little, with a mark when the line waits for the player. `[pause=500/]` in a line pauses the typing. | Typewriter (Instant, Letter, Word), Speed (units per second), Auto Advance and Hold Time, Font |
| Dialogue options presenter (`OptionsPresenter`) | A column of buttons. The player clicks, or moves with the keys or the gamepad. | Unavailable (Hidden or Disabled), the keys, Number Keys ("1." "2." and the digit keys), Font |
| Dialogue line advancer (`LineAdvancer`) | The player's "next": a first press finishes the typing, the next one leaves the line. | Advance Key, Advance Button, Advance With Pointer |

With no UI page, they build a default look: a text box at the bottom of the screen and the options at its centre. For the game's own look, give them a UI page (`Page`) that has elements with these names (`DefaultDialogueLook`):

| Name | Element |
|---|---|
| `SkeinText` | The text block of the line. Required. |
| `SkeinSpeaker` | The text block of the speaker's name. Without it, the name stays in the text. |
| `SkeinLine` | What is shown while a line is, and hidden when the dialogue is over. |
| `SkeinContinue` | What is shown when the line is all there and waits. |
| `SkeinOptions` | The panel of the options: its buttons, each with a text block inside, take the options in order. |

The default compositor of a new Stride game draws the UI before the post effects, which wash it out. Brinemoor's compositor has a stage for the UI drawn after them; copy it for a readable dialogue box.

A line can carry events of the game: the line presenter raises `MarkerReached` when the typing passes a marker it does not know.

```csharp
public sealed class Shaker : StartupScript
{
    public LinePresenter? Lines { get; set; }

    public override void Start()
    {
        if (Lines is null)
            return;

        // "The ground moves. [shake/] Did you feel that?"
        Lines.MarkerReached += marker =>
        {
            if (marker.Name == "shake")
                Log.Info("The camera shakes");
        };
    }
}
```

### A presenter of the game

Derive from `DialoguePresenter` and override what the presenter does. Every method is asynchronous: the dialogue goes on when the tasks of all the presenters complete.

| Method | When |
|---|---|
| `RunLineAsync(line, cancellation)` | A line to show. Return when the presenter is done with it. |
| `RunOptionsAsync(options, cancellation)` | Options to choose from. Return the `Index` of the chosen option, or `null` to let another presenter answer. |
| `OnDialogueStartedAsync()`, `OnDialogueCompletedAsync()` | A dialogue starts; it is over, or was stopped. |
| `OnNodeEnterAsync(node)`, `OnNodeExitAsync(node)` | The dialogue enters or leaves a node. |
| `PrepareForLinesAsync(lineIds)` | The lines a node may show: the time to load voice-over or portraits. |

A `LocalizedLine` has the text in the current language (`Text`, and `TextWithoutCharacterName` when the name is shown apart), the speaker (`CharacterName`), the line's id and its tags (`Metadata`: `#mood:angry` in a script gives `mood:angry`). The markup of the text is read: `Text.Attributes` has each attribute with its place.

`LineCancellation` carries the two requests of a player, as in Yarn Spinner for Unity: `HurryUp` (show the rest of the line now) and `NextLine` (leave the line). Both are cancelled when the dialogue stops.

```csharp
public sealed class CaptionPresenter : DialoguePresenter
{
    public float SecondsPerLetter { get; set; } = 0.05f;

    public override async Task RunLineAsync(LocalizedLine line, LineCancellation cancellation)
    {
        var text = line.TextWithoutCharacterName.Text;
        Show(line.CharacterName, text);

        // The line stays for a time that fits its length, unless the player asks for the next one
        var left = 1.5f + text.Length * SecondsPerLetter;
        while (left > 0 && !cancellation.NextLine.IsCancellationRequested)
        {
            left -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
            await Script.NextFrame();
        }
    }

    public override Task OnDialogueCompletedAsync()
    {
        Hide();
        return Task.CompletedTask;
    }

    private void Show(string? speaker, string text) => Log.Info(speaker is null ? text : $"{speaker}: {text}");

    private void Hide() => Log.Info("");
}
```

A presenter of options returns the choice. This one takes the first option that can be chosen, for a demo that plays by itself:

```csharp
public sealed class FirstOptionPresenter : DialoguePresenter
{
    public override Task<int?> RunOptionsAsync(IReadOnlyList<DialogueOption> options, CancellationToken cancellation)
        => Task.FromResult(options.FirstOrDefault(option => option.IsAvailable)?.Index);
}
```

A presenter is a script component: add it to an entity, and add it to the runner's `Presenters`. Brinemoor's `MoodPresenter` colours the speaker's name by a tag of the line, beside the line presenter.

## 5. Commands and functions

A **command** is something the story tells the game to do: `<<pay 3>>`. A **function** is something the story asks the game: `<<if coins() >= 3>>`. Both are C# methods with an attribute.

```csharp
public static class Purse
{
    public static int Coins { get; set; } = 10;

    [YarnFunction("coins", Description = "How many coins the player has: coins().")]
    public static int CoinCount() => Coins;

    [YarnCommand("pay", Description = "Takes coins from the player: <<pay 3>>.")]
    public static void Pay(int amount)
    {
        if (amount > Coins)
            throw new YarnCommandException($"pay {amount}", $"the player has {Coins} coins.");
        Coins -= amount;
    }
}
```

```yarn
Tam: A bed is three coins.
-> Pay. <<if coins() >= 3>>
    <<pay 3>>
    Tam: Up the stairs, second door.
-> Not tonight.
```

- Skein finds the methods by their attributes in the assemblies of the game when the game starts. Nothing is registered by hand.
- The `Description` is for the writers: Game Studio and Yarn Spinner's Visual Studio Code extension show it.
- A **function** is static, takes and returns booleans, numbers and strings, and changes nothing in the game: Yarn Spinner calls it when it evaluates a condition, as often as it likes.
- A **command** returns nothing, or a task the dialogue waits for. Its parameters are the words of the command, converted: strings, booleans, numbers, enums, and entities or components found by name. Optional parameters, a `params` array and a `CancellationToken` work.
- A command that cannot be done throws `YarnCommandException`: the log has the error, and the dialogue goes on.

**A command on an object.** A command can be an instance method of a component. The first word after the command then names the entity:

```csharp
public sealed class Gate : StartupScript
{
    public bool IsOpen { get; private set; }

    // <<lower Portcullis>> runs on the entity named Portcullis, at once
    [YarnCommand("lower", Description = "Closes a gate: <<lower Portcullis>>.")]
    public void Lower() => IsOpen = false;

    // <<raise Portcullis 2>>: the dialogue waits until the gate is up
    [YarnCommand("raise", Description = "Opens a gate in so many seconds, and waits: <<raise Portcullis 2>>.")]
    public async Task Raise(float seconds = 1, CancellationToken cancellation = default)
    {
        while (seconds > 0 && !cancellation.IsCancellationRequested)
        {
            seconds -= (float)Game.UpdateTime.Elapsed.TotalSeconds;
            await Script.NextFrame();
        }

        IsOpen = true;
    }
}
```

The name is looked for among the entities that have a **dialogue actor** component (`DialogueActorComponent`, whose Actor Name is the name the scripts use; empty, the entity's own name), then among the entities by name, in the runner's scene first. Give an actor component to what the story names: it keeps working when the entity is renamed or spawned from a prefab. The token of a command is cancelled when the dialogue stops.

**By hand.** A command or a function can also be a delegate, added and removed from code:

```csharp
public sealed class Fanfare : StartupScript
{
    public override void Start()
    {
        var dialogues = Services.GetSafeServiceAs<DialogueSystem>();
        dialogues.Actions.AddCommandHandler("fanfare", (int trumpets) => Log.Info($"{trumpets} trumpets play"));
        dialogues.Actions.AddFunction("difficulty", () => 2);
    }

    public override void Cancel()
    {
        var dialogues = Services.GetSafeServiceAs<DialogueSystem>();
        dialogues.Actions.RemoveCommandHandler("fanfare");
        dialogues.Actions.RemoveFunction("difficulty");
    }
}
```

The build and Game Studio only know the methods with attributes: a function added by hand is unknown to the compiler, which then guesses its type from how the script uses it.

**Built in.** `<<wait 2>>` waits two seconds of game time, `<<stop>>` ends the dialogue, and Yarn Spinner's standard functions (`visited`, `visited_count`, `dice`, `random`, `round`, `format` and the others) are there.

**A command nobody handles** raises the runner's `UnhandledCommand` with its text. Without a listener, the log has one warning per command, and the dialogue goes on. A game can use the event as its own small command interpreter.

## 6. Variables

`DialogueSystem` is the game system Skein adds to a game. It has the commands and the functions (`Actions`), the variables (`Variables`) and the language (`Language`). A script finds it in the services.

The variables of the story live in a `VariableStorage`. The game reads them, sets them and listens to them:

```csharp
public sealed class LampLight : SyncScript
{
    private VariableStorage? variables;

    public YarnProject? Story { get; set; }

    public override void Start()
    {
        variables = Services.GetSafeServiceAs<DialogueSystem>().Variables;
        variables.VariableChanged += OnVariableChanged;

        // The storage learns a story when a runner of it starts: a script that may start first tells it
        if (Story is not null)
            variables.Declare(Story);

        // A variable that was never set reads the default its script declares
        if (variables.TryGetValue<bool>("$lamp_lit", out var lit))
            Entity.EnableAll(lit, applyOnChildren: true);
    }

    public override void Update()
    {
        if (variables is not null && Input.IsKeyPressed(Keys.F1))
            variables.SetValue("$lamp_lit", true);
    }

    public override void Cancel()
    {
        if (variables is not null)
            variables.VariableChanged -= OnVariableChanged;
    }

    private void OnVariableChanged(string name, object? value)
    {
        if (name == "$lamp_lit" && value is bool lit)
            Entity.EnableAll(lit, applyOnChildren: true);
    }
}
```

- Numbers are `float`, and an enum of a script is a number (its case, counted from zero) unless its cases have values.
- A smart variable (`<<declare $can_pay = coins() >= 3>>`) is computed each time it is read, and cannot be set.
- Setting a variable no script declares, or with another type than its declaration, throws `ArgumentException` with the reason.
- The storage knows the variables of a project from the start of a scene that has a runner of it. Code that reads a variable or loads a save before that (a script that starts before the runner, a menu without a runner) calls `Variables.Declare(project)` first.
- Where to keep the state of a game is a choice. Brinemoor keeps what the story decides in Yarn variables (`$act`, `$quest_lamp`), and what the world owns in the game (the inventory, the clock), which the story reads with functions and changes with commands. Nothing is in both.

## 7. Saving

`Variables.Save()` gives a `VariableSnapshot`: three dictionaries (numbers, strings, booleans) that any serializer writes. `Variables.Load(snapshot)` puts them back.

```csharp
public sealed class SaveFile
{
    public Dictionary<string, float> Numbers { get; set; } = [];

    public Dictionary<string, string> Strings { get; set; } = [];

    public Dictionary<string, bool> Bools { get; set; } = [];

    public static SaveFile Capture(DialogueSystem dialogues)
    {
        var snapshot = dialogues.Variables.Save();
        return new SaveFile { Numbers = snapshot.Numbers, Strings = snapshot.Strings, Bools = snapshot.Bools };
    }

    public IReadOnlyList<string> Restore(DialogueSystem dialogues)
    {
        var snapshot = new VariableSnapshot();
        foreach (var (name, value) in Numbers)
            snapshot.Numbers[name] = value;
        foreach (var (name, value) in Strings)
            snapshot.Strings[name] = value;
        foreach (var (name, value) in Bools)
            snapshot.Bools[name] = value;

        // What the story no longer declares is dropped, and returned
        return dialogues.Variables.Load(snapshot);
    }
}
```

- **Save between two dialogues.** A dialogue that is running is not in the snapshot: where it is in its node cannot be saved. A game refuses to save while a runner `IsRunning`, or saves when the dialogue is over.
- How often each node was visited, and what each `once` remembers, are variables of the storage (their names start with `$Yarn.Internal.`): they are saved with the rest.
- Load a save once the storage knows the story (section 6): a storage that knows no project keeps everything it is given, and one that knows another project drops what that project does not declare.
- A save made with an older story loads: what no script declares any more, or declares with another type, is dropped, with one warning that lists it.

## 8. Languages

A project is written in its base language, and has a strings file for each other language (the writers' guide says how they are made). The game changes the language with one property:

```csharp
public sealed class LanguageKey : SyncScript
{
    public DialogueRunnerComponent? Runner { get; set; }

    public override void Update()
    {
        if (Runner?.Project is not { } project || !Input.IsKeyPressed(Keys.L))
            return;

        // The base language first, then the languages that have a strings file
        var dialogues = Services.GetSafeServiceAs<DialogueSystem>();
        var current = dialogues.Language.Length > 0 ? dialogues.Language : project.BaseLanguage;
        var next = project.Languages[(project.Languages.IndexOf(current) + 1) % project.Languages.Count];
        dialogues.Language = next;
    }
}
```

- The lines that follow are in the new language; the line on the screen stays as it is. `LanguageChanged` tells the game, for its own texts.
- A line that has no translation shows in the base language, with one warning in the log per line. The build says how many lines lack a translation.
- **The texts of the game's own screen** can be lines too. Brinemoor has a node that nothing plays, with one line per word of its screen and an id written by hand (`Carrying #line:ui_carrying`). The game reads them with `GetLineProvider(project).GetLine("line:ui_carrying", [])`, so they are translated in the same file as the story.
- Markup: `select`, `plural` and `ordinal` work in every language, with the rules of the language of the line. A `plural` or an `ordinal` shows its `other` text for a case it does not give. A game adds its own replacement markers on `GetLineProvider(project).Markup`, which is Yarn Spinner's `LineParser`.
- For voice-over, or lines that come from elsewhere, give a runner its own `ILineProvider` (`LinesOverride`).

## 9. Testing a story without a game

`DialogueSession` plays a compiled project with no game and no window: it is what the runner, Game Studio's dialogue player and Skein's tests all use. A test compiles the scripts, gives the session the game's commands and functions, answers the options, and reads what was said.

Brinemoor's tests do this (`sample/Brinemoor/Brinemoor.Tests/TestStory.cs`): they play the story to each of its four endings by the text of the options, in two languages, in a few seconds. The test project references `ColorRise.Skein.Assets` for the compiler. A game's commands that are static methods need the tests of a project to run one after the other.

## 10. A trimmed build

Skein finds commands and functions by reflection. A trimmed build must keep the methods that carry the attributes, in one of two ways:

- keep the game's assembly whole (`<TrimmerRootAssembly Include="MyGame" />` in the project that publishes), or
- register the types from code, which also tells the trimmer to keep their methods:

```csharp
public static class TrimmedGame
{
    public static void Register(DialogueSystem dialogues)
    {
        dialogues.Actions.RegisterType(typeof(Purse));
        dialogues.Actions.RegisterType(typeof(Gate));
    }
}
```

Registering a type twice changes nothing, so this is safe beside the search by attributes. A trimmed or ahead-of-time build has not been tried with Skein yet: a source generator that replaces the reflection is planned.

## 11. Limits

- A dialogue cannot be saved while it runs (section 7).
- One Yarn Spinner version: 3.2.2, taken from nuget.org unchanged. Scripts use the language of Yarn Spinner 3.
- The build and the editor know the commands and functions with attributes only (section 5).
- No voice-over presenter and no audio: a game writes its own presenter and line provider for them.
- The UI presenters use Stride's UI. Their look is a UI page, or the default one; the UI editor of the cross-platform Game Studio is not there yet, so a page is made in code or in the Windows Game Studio.
- Trimmed and ahead-of-time builds are not tried (section 10).
- Skein is built against a development version of Stride (section 1).
