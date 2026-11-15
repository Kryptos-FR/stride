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

- The five packages build and pack. They hold nothing to try yet.

### Not yet

Everything else of the design: the Yarn assets, the runtime, the editor, the dialogue player, the sample game "Brinemoor".

## Tied to the fork

What still depends on the checkout Skein sits in, and what removes it:

- `nuget.config` takes the Stride packages from `../../bin/packages`, the output of a local engine build. Once a released Stride has the plugin model, nuget.org serves them and that source goes.
- `Directory.Build.targets` tells the build where the engine's asset compiler is when Skein is built in another configuration than the engine (a Release pack after a Debug engine build). It only acts against a local engine build, and goes with it.

## Credits and licenses

Dialogue powered by Yarn Spinner — https://yarnspinner.dev

Yarn Spinner is © Yarn Spinner Pty. Ltd., Secret Lab Pty. Ltd. and its contributors, under the MIT license. "Yarn Spinner" is a trademark of Yarn Spinner Pty. Ltd.; it is used here only to say what Skein works with. The licenses of Yarn Spinner and of the libraries it brings are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). A game made with Skein ships Yarn Spinner too: keep its license with your game, and credit it as above.
