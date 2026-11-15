# ColorRise.Skein

Yarn Spinner support for [Stride](https://www.stride3d.net) games, by Color-Rise: write dialogue and story in [Yarn Spinner](https://yarnspinner.dev)'s language, edit it in Game Studio as a graph of nodes with a text pane and the property grid, play it in the editor before the game exists, and run it in a Stride game with a dialogue runner, presenters, variable storage, and commands and functions written in C#.

Skein is built with Yarn Spinner and is compatible with it: it takes the Yarn Spinner compiler and runtime from nuget.org, unchanged, and its `.yarn` files are plain Yarn files that Yarn Spinner's own tools read and write. **Skein is not an official Yarn Spinner product, and it is not affiliated with or endorsed by Yarn Spinner Pty. Ltd. or by the Stride project.**

It lives on this branch of a Stride fork only to build against the engine's development version, whose plugin model it uses: no released Stride has that plugin model yet. It is meant to move to a repository of its own, so it uses nothing of this repository but Stride's packages (see [Tied to the fork](#tied-to-the-fork)).

| Package | What it holds |
|---|---|
| `ColorRise.Skein` | The runtime: compiled Yarn projects, the dialogue runner, variable storage, the line provider, the presenter base, commands and functions. A game references this one. |
| `ColorRise.Skein.UI` | Presenters on Stride's UI: a line presenter with a typewriter, an options presenter, a line advancer. A game references it when it wants them. |
| `ColorRise.Skein.Assets` | The Yarn script and Yarn project assets, the compiler and the templates (loaded by the asset compiler and Game Studio). |
| `ColorRise.Skein.Editor` | Game Studio: the script editor, the project overview, the dialogue player, the commands (any toolkit). |
| `ColorRise.Skein.Editor.Avalonia` | The views of the cross-platform Game Studio. |

The runtime declares the last three as its companions: whoever loads a game that references `ColorRise.Skein` (the asset compiler, Game Studio) loads them too. A game never references them itself.

The design is in [docs/design.md](docs/design.md), the plan of the work in [docs/plan.md](docs/plan.md), and what Skein found in Stride's plugin model in [docs/plugin-findings.md](docs/plugin-findings.md).

## Try it

All paths are from the root of the Stride checkout. You need the .NET 10 SDK.

**1. Build the engine and the editor.** Skein reaches Stride through packages only. A local engine build makes them: it puts `.nupkg` files in `bin/packages` at version `4.5.3-dev`.

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

### What works today

This section follows the work: it says what can be tried at the current commit.

- The five packages build and pack.
- A `.yarn` file of an asset folder is an asset, "Yarn script": the asset view shows it, *Add asset > Dialogue > Yarn script* makes one, and saving, renaming and moving it in Game Studio keep it a plain Yarn file (UTF-8, no byte order mark unless the file came with one).
- A double click on a script opens it in a text pane, the first piece of the script editor. Typing is part of Game Studio's own undo history: the Edit history panel lists "Typing" steps, Ctrl+Z, Ctrl+Y and the Edit menu undo and redo them, and File > Save writes the file. No colours, graph or checks yet.
- A Yarn project is an asset too (`.sdyarn`): *Add asset > Dialogue > Yarn project* makes one, and the property grid edits it: the scripts it includes (glob patterns from its folder, `**/*.yarn` by default), its base language, its saliency strategy, and the severity of the compiler's messages by code. Building the game compiles the project: its data holds the compiled program, the lines of the base language, and the lines of each other language that has a strings file (Yarn Spinner's CSV; a line without a translation shows in the base language, and the build says how many there are). An error in a script fails the build with the script's file, line, column and Yarn Spinner's code (in a new project, a jump to a node that does not exist is an error; Yarn Spinner makes it a warning). A script that no project includes is a warning of the build.
- Not visible in Game Studio yet, covered by tests: the runtime (the dialogue system, the dialogue runner component, presenters, actors, commands and functions in C#, variable storage) and the UI presenters. Yarn Spinner's own 32 test plans pass against Skein's dialogue session.
- The sample game "Brinemoor" is an empty scene with one script and one project, to open in Game Studio:

  ```sh
  cd sources/editor/Stride.GameStudio.Avalonia.Desktop/bin/Debug/net10.0
  dotnet Stride.GameStudio.Avalonia.Desktop.dll <checkout>/extensions/ColorRise.Skein/sample/Brinemoor/Brinemoor.slnx
  ```

  *View > Plugins* lists `ColorRise.Skein.Editor` and `ColorRise.Skein.Editor.Avalonia` under "Plugins of the project". After a rebuild of Skein while Game Studio is open, Game Studio offers to reload.

### Not yet

Everything else of the design: the commands of Game Studio that add line ids, update the strings files and write Yarn Spinner's own project files (the code behind them is there and tested), the graph of the script editor, the project overview, the dialogue player, and the story of the sample game.

## Tied to the fork

What still depends on the checkout Skein sits in, and what removes it:

- `nuget.config` takes the Stride packages from `../../bin/packages`, the output of a local engine build. Once a released Stride has the plugin model, nuget.org serves them and that source goes.
- `Directory.Build.targets` tells the build where the engine's asset compiler is when Skein is built in another configuration than the engine (a Release pack after a Debug engine build). It only acts against a local engine build, and goes with it.

## Credits and licenses

Dialogue powered by Yarn Spinner — https://yarnspinner.dev

Yarn Spinner is © Yarn Spinner Pty. Ltd., Secret Lab Pty. Ltd. and its contributors, under the MIT license. "Yarn Spinner" is a trademark of Yarn Spinner Pty. Ltd.; it is used here only to say what Skein works with. The licenses of Yarn Spinner and of the libraries it brings are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). A game made with Skein ships Yarn Spinner too: keep its license with your game, and credit it as above.
