# ColorRise.Skein

Yarn Spinner support for [Stride](https://www.stride3d.net) games, by Color-Rise: write dialogue and story in [Yarn Spinner](https://yarnspinner.dev)'s language, edit it in Game Studio as a graph of nodes with a text pane and the property grid, play it in the editor before the game exists, and run it in a Stride game with a dialogue runner, presenters, variable storage, and commands and functions written in C#.

Skein is built with Yarn Spinner and is compatible with it: it takes the Yarn Spinner compiler and runtime from nuget.org, unchanged, and its `.yarn` files are plain Yarn files that Yarn Spinner's own tools read and write. **Skein is not an official Yarn Spinner product, and it is not affiliated with or endorsed by Yarn Spinner Pty. Ltd. or by the Stride project.**

It lives on this branch of a Stride fork only to build against the engine's development version, whose plugin model it uses: no released Stride has that plugin model yet. It is meant to move to a repository of its own, so it uses nothing of this repository but Stride's packages (see [Tied to the fork](#tied-to-the-fork)).

| Package | What it holds |
|---|---|
| `ColorRise.Skein` | The runtime: compiled Yarn projects, the dialogue runner, variable storage, the line provider, the presenter base, commands and functions. A game references this one. |
| `ColorRise.Skein.UI` | Presenters on Stride's UI: a line presenter with a typewriter, an options presenter, a line advancer. A game references it when it wants them. |
| `ColorRise.Skein.Audio` | The voice-over presenter, on Stride's audio. A game references it when its lines are voiced. |
| `ColorRise.Skein.Assets` | The Yarn script and Yarn project assets, the compiler and the templates (loaded by the asset compiler and Game Studio). |
| `ColorRise.Skein.Editor` | Game Studio: the script editor, the project overview, the dialogue player, the commands (any toolkit). |
| `ColorRise.Skein.Editor.Avalonia` | The views of the cross-platform Game Studio. |

The runtime declares the last three as its companions: whoever loads a game that references `ColorRise.Skein` (the asset compiler, Game Studio) loads them too. A game never references them itself.

Two guides say how to use it: [docs/guide-writers.md](docs/guide-writers.md) for who writes the story (the script editor, the dialogue player, line ids and translations, voice-over, working with Visual Studio Code on the same files), and [docs/guide-developers.md](docs/guide-developers.md) for who programs the game (the packages, the dialogue runner, presenters (voice-over included), commands and functions, variables and saves, languages).

The design is in [docs/design.md](docs/design.md), the plan of the work in [docs/plan.md](docs/plan.md), and what Skein found in Stride's plugin model in [docs/plugin-findings.md](docs/plugin-findings.md).

## Try it

All paths are from the root of the Stride checkout. You need the .NET 10 SDK.

**1. Build the engine and the editor.** Skein reaches Stride through packages only. A local engine build makes them: it puts `.nupkg` files in `bin/packages` at version `4.5.4-dev`.

```sh
dotnet build sources/Stride.Editor.Avalonia.slnf
dotnet build sources/assets/Stride.AssetCompiler/Stride.AssetCompiler.csproj
```

Both are Debug builds. Build the engine again after you pull engine changes.

**2. Build and test Skein.**

```sh
dotnet build extensions/ColorRise.Skein/ColorRise.Skein.slnx
dotnet test extensions/ColorRise.Skein/ColorRise.Skein.slnx
```

To build against another Stride version, copy `Directory.Build.local.props.example` to `Directory.Build.local.props` (git-ignored) and set `SkeinStrideVersion`.

**3. Use it in a game of your own.** `dotnet pack extensions/ColorRise.Skein/ColorRise.Skein.slnx` puts the six packages in `extensions/ColorRise.Skein/bin/packages`. The first section of the [developers' guide](docs/guide-developers.md) has the two references and the `nuget.config` a game needs.

### What works today

This section follows the work: it says what can be tried at the current commit.

- The six packages build and pack, with the documentation of their public types. A small game in a folder outside the Stride checkout, with only the two package references of the developers' guide, restores, builds its Yarn project and plays it (on Linux here, the asset compiler first failed to restore the companion packages: finding 21 of [docs/plugin-findings.md](docs/plugin-findings.md)).
- The C# and the Yarn of the two guides are compiled by the tests.
- A `.yarn` file of an asset folder is an asset, "Yarn script": the asset view shows it, *Add asset > Dialogue > Yarn script* makes one, and saving, renaming and moving it in Game Studio keep it a plain Yarn file (UTF-8, no byte order mark unless the file came with one).
- A double click on a script opens the script editor: the graph of its nodes on the left, its text on the right, the compiler's messages below.
  - Graph: a card for each node, with a row for each line, option, block and command of its body (a double click on a line or an option edits it in a box on the card, keeping its line id; *+ line* adds a line with the speaker of the last one; *Fold cards* folds them to their first lines; a click on the condition chip of a line or an option, or on its "+ if", opens the condition builder, rows of variables, functions and values offered from the project, joined by `and` or `or`, with *once* and an *Edit as text* for the conditions rows cannot hold; a double click on a `<<set>>` opens the set builder), a link for each jump, leaving from the option or the branch of a block it is under (a detour dashed and teal, a jump to an expression dotted and blue-grey, a jump to a node that does not exist red; a long link, or one whose card is out of view, shows the option at its middle), a stub for a node of another script (a double click opens it), a red stub for a node that does not exist, a blue-grey stub for a jump to an expression, and an "end" mark on the cards where the dialogue stops. Drag a card to place it (`position:` in the text), drag from a card's "jump" or "detour" connector, or from the connector of an option, to another card to add the jump there, double click or F2 to rename (the jumps of every script of the project follow), right click for the menus (add a node or a note, duplicate, delete, colour, add to a node group, layout: the cards in rows that follow the jumps, with the nodes of a node group side by side, and one block for each frame of *Group by* or each cluster). Clusters, node groups and the values of the "Group by" header are frames; the search dims the cards that do not match.
  - Text: the lines of the selected nodes, or the whole file. Yarn's colours, the compiler's messages underlined as you type, Ctrl+click on a node name to go to it. A double click on a message in the list shows its place.
  - *Export graph…* writes the graph for a design document: a Mermaid file (`.mmd`, or fenced in a `.md`) or a Graphviz file (`.dot`, `.gv`), by the extension of the name. A node group is one node, the notes stay out, the jumps are solid and the detours dashed, labelled with the option they leave from.
  - Each character has a colour, on the cards, in the text and in the dialogue player: one made from its name, or the one chosen in the project's *Character colours*.
  - Everything is a step of Game Studio's own undo history: the Edit history panel lists the steps ("Typing", "Move node", "Rename node 'A' to 'B'"), Ctrl+Z and Ctrl+Y undo and redo them, and File > Save writes the file.
  - The property grid shows the headers of the nodes selected on the graph: title, tags, colour, cluster, the `when:` conditions (each with a "…" button that opens the condition builder, *always* and *once* included) and the other headers. A change there is a change of the text (a new title renames the node in the whole project), and one step of the history.
  - A script another program changed while Game Studio is open (Yarn Spinner's Visual Studio Code extension, a text editor, a pull from version control) is reloaded: at once when it has no unsaved change here, where one undo gives the text of before back, and after a question when it has.
- A Yarn project is an asset too (`.sdyarn`): *Add asset > Dialogue > Yarn project* makes one, and the property grid edits it: the scripts it includes (glob patterns from its folder, `**/*.yarn` by default), its base language, its saliency strategy, the severity of the compiler's messages by code, and the colours of its characters in Game Studio. Building the game compiles the project: its data holds the compiled program, the lines of the base language, and the lines of each other language that has a strings file (Yarn Spinner's CSV; a line without a translation shows in the base language, and the build says how many there are). An error in a script fails the build with the script's file, line, column and Yarn Spinner's code (in a new project, a jump to a node that does not exist is an error; Yarn Spinner makes it a warning). A script that no project includes is a warning of the build. The build knows the functions of the game's code (`[YarnFunction("has_item")]` on a static method): a call with the wrong types is an error at its line. A command can take parameters of the game's own types through a converter (`[YarnConverter(typeof(Seed))]` on `static bool TrySeed(string text, out Seed seed)`).
- A string property that names a node (`[YarnNode(nameof(Project))]` on a member, as the runner's "Start Node" has) is picked in the property grid among the nodes of the project its object references: a box whose list filters as you type. `Filter`, `FilterType` and `FilterHeader` narrow the list to the nodes whose header passes, as in Yarn Spinner for Unity. Game Studio's cross-platform version cannot add a component to an entity yet, so a runner has to be in the scene file to see it.
- A double click on a Yarn project opens its overview: its scripts with their nodes and errors, its variables (a double click shows the declaration), the commands and functions of the game's code (how a script writes each, its description and its method), what the compiler says of the whole project, and its languages with their counts (lines, translated, to update, missing, voiced). Its buttons:
  - *Add line ids* gives a `#line:` id to each line that has none, in one step of the history. A line needs an id to be translated.
  - *Export strings* writes the strings file of a language (Yarn Spinner's CSV: the lines in the base language, to translate in place) as `Strings/<project>.<language>.csv` in the folder of the project, and sets it in the project's localisations. It never writes over the file of a translation.
  - *Update strings files* brings each file up to date with the scripts: new lines are added without a text, lines that are gone are removed, and the translation of a line that changed is marked `(NEEDS UPDATE)`.
  - *Export recording script* writes, for each language, the script the actors record from (`Recording/<project>.<language>.csv`): the file of each clip, the character, the text, the node, the line before it and the line's hashtags, and whether the clip is there already.
  - Saving a project writes a `.yarnproject` file beside it (and a `.ysls.json` file of the game's commands and functions once the game is built), so that Yarn Spinner's Visual Studio Code extension and command line see the same project. What another tool keeps in the file stays.
- Voice-over: the project's *Voice-over folder* (`Voice/{language}` by default) holds a Sound asset per line and language, named after the line's id (`033f504` for `#line:033f504`). The build puts the clips in the game with the project and says, per language, how many lines have none; a sound whose name is no line's id is a warning. In the game, the voice-over presenter of `ColorRise.Skein.Audio` plays each line's clip, from the speaker's entity when it has an audio emitter, and goes to the next line when the clip ends. A line has no clip of another language. Game Studio's dialogue player marks the lines that have a clip, without playing them.
- The dialogue player plays a node of a Yarn project from its scripts as they are in memory: nothing needs saving or building. "▶ Play" in the toolbar of the script editor, or "Play from here" in the menu of a card, opens the *Dialogue player* panel (also in the *View* menu) and starts.
  - The panel shows what the dialogue says, waits after each line (*Continue*) and at the options (those whose condition fails are greyed). Commands go on at once, and show in the transcript. When the scripts call a function of the game (`has_item("oil")`), the panel asks for the answer and remembers it for that call. The variables can be set by hand (a variable of an enum shows its case, and takes the name of another), and an answer changed. When a node group or a line group picks, the transcript tells why: each candidate with its score, or the condition that ruled it out.
  - The script editor follows the run: the card of the node the run is in has a blue border, the nodes it went through a paler one, and the text lights the line.
  - *Timeline* opens the *Dialogue timeline* panel: a column per step, and lanes for the nodes, the lines, the choices, what the game did and the variables. A click on a step (or the arrows) moves the run back and forth; the player, the variables and the script editor show that step. Going on from an earlier step with another choice drops what followed.
  - After an edit of the scripts, *Replay* plays the same inputs on the new scripts: a dot marks the steps that are new or read differently, and the replay stops with the reason before a choice that no longer exists.
  - With several languages, the panel plays in the one chosen (from the strings files as they are on the disk).
- Commands, in the command palette (Ctrl+Shift+P, category *Dialogue*) and in the menus:
  - *Play dialogue*, in the menu of the asset view on a Yarn project or on a script of a project: opens the dialogue player and starts, at the node `Start` of the project (else its first node by name), or at the first node of the script.
  - *Play from here* (Ctrl+F5 in a script editor, and in the menu of a card): plays the node of the card, else the selected node, else the first node of the script.
  - *Add line ids*, *Export strings* (the strings file of each language that has none), *Update strings files* and *Export recording script*, in the menu of the asset view on a Yarn project: they open the overview of the project, which shows what was done.
  - *Open dialogue player*.
- *Add asset > Dialogue > Yarn project*, in a folder where the project would have no script, also makes a first script `Start` with a node `Start`: both in one step of the undo history, and *Play dialogue* on the new project plays at once.
- *Add entity > Dialogue > Dialogue runner* in the scene editor makes an entity with a dialogue runner that starts at the node `Start`, and with a line presenter, an options presenter and a line advancer when the game references `ColorRise.Skein.UI`, and a voice-over presenter when it references `ColorRise.Skein.Audio`. In a game with one Yarn project, the runner plays it.
- The Yarn script and the Yarn project have their thumbnail in the asset view and their icon in *Add asset*; the dialogue runner and the presenters have an icon in the property grid.
- Covered by tests only: the runtime (the dialogue system, the dialogue runner component, presenters, actors, commands and functions in C#, variable storage) and the UI presenters. Yarn Spinner's own 32 test plans pass against Skein's dialogue session.
- The sample game "Brinemoor" (`sample/Brinemoor`): a courier on a small island, with people to talk to and things to use. Its story has three acts of one day each, eight quests and four endings, in English and in French: five scripts and 70 nodes under `Brinemoor.Game/Assets/Dialogue`, written to use every feature of Yarn Spinner at least once.
  - Build and run it on Linux (on Windows, `Brinemoor.Windows`):

    ```sh
    cd extensions/ColorRise.Skein/sample/Brinemoor
    dotnet build Brinemoor.Linux/Brinemoor.Linux.csproj
    dotnet Bin/Linux/Debug/Brinemoor.Linux.dll
    ```

  - Keys: WASD or the arrows move, T talks to the person nearby, E uses the thing nearby, Space goes to the next line, 1 to 9 choose an option, N lets time pass, L switches the language, F5 saves, F9 loads, Esc quits.
  - What it shows of Skein in a game: a dialogue runner with the line presenter, the options presenter (numbered) and the line advancer; a presenter of the game's own (`MoodPresenter`); a second runner whose lines are captions (`Chatter`); commands and functions in C# (`StoryCommands`, and `walk` on a `Talkable`); a node group `Events` that the game plays whenever one of its nodes has something to say (`StoryDirector`); the screen's own words taken from line ids, so that they are translated with the story; a save of the variables with the game's state (`SaveSlot`); the voice-over presenter, with placeholder clips (a blip per word) for the three lines of narration that open the story in English.
  - The story in short: light the lamp of the lighthouse before the first night, or a ship strikes the rocks; take a side at the town meeting and follow the trail of the missing keeper; on the third day the island votes who keeps the light, and each voter listens to the courier only if a deed of the first two days moved them.
  - Its tests (`Brinemoor.Tests`) need no running game: they play the story to each of its four endings, in English and in French, and check that the scripts use each feature of Yarn Spinner and that every line is translated.
  - Yarn Spinner's own tools read the story as it is: `Brinemoor.yarnproject` and `Brinemoor.ysls.json` beside the Yarn project are the files Skein writes, and a test compiles the scripts with Yarn Spinner's compiler from that project file and the definitions file it names, as Yarn Spinner's command line does. The command line itself (`ysc compile Brinemoor.yarnproject`) compiled the story without a message in version 3.2.2; version 3.3 was not run.
  - Open it in Game Studio:

    ```sh
    cd sources/editor/Stride.GameStudio.Avalonia.Desktop/bin/Debug/net10.0
    dotnet Stride.GameStudio.Avalonia.Desktop.dll <checkout>/extensions/ColorRise.Skein/sample/Brinemoor/Brinemoor.slnx
    ```

    *View > Plugins* lists `ColorRise.Skein.Editor` and `ColorRise.Skein.Editor.Avalonia` under "Plugins of the project". After a rebuild of Skein while Game Studio is open, Game Studio offers to reload.

### Not yet

- A check on Windows: everything here was built and run on Linux. The WPF Game Studio gets none of Skein's views, and was not opened with a game that uses Skein.
- Yarn Spinner's Visual Studio Code extension, and its command line in version 3.3, were not run on the files Skein writes: its 3.3 compiler was, from the project file as they read it.
- A trimmed or ahead-of-time build of a game (the developers' guide says what it needs). Skein finds commands and functions by reflection; a source generator in its place is a later step.

## Tied to the fork

What still depends on the checkout Skein sits in, and what removes it:

- `nuget.config` takes the Stride packages from `../../bin/packages`, the output of a local engine build. Once a released Stride has the plugin model, nuget.org serves them and that source goes.
- `Directory.Build.targets` tells the build where the engine's asset compiler is when Skein is built in another configuration than the engine (a Release pack after a Debug engine build). It only acts against a local engine build, and goes with it.
- The packages have a prerelease version, 0.1.0-dev, because they depend on a development version of Stride. They get a stable version with a released Stride.

## Credits and licenses

Skein is under the MIT license: see [LICENSE](LICENSE).

Dialogue powered by Yarn Spinner — https://yarnspinner.dev

Yarn Spinner is © Yarn Spinner Pty. Ltd., Secret Lab Pty. Ltd. and its contributors, under the MIT license. "Yarn Spinner" is a trademark of Yarn Spinner Pty. Ltd.; it is used here only to say what Skein works with. The licenses of Yarn Spinner and of the libraries it brings are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). A game made with Skein ships Yarn Spinner too: keep its license with your game, and credit it as above.
