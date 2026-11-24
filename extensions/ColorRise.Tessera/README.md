# ColorRise.Tessera

Tile sets, tile maps and flipbooks for [Stride](https://www.stride3d.net) games, by Color-Rise: cut images into tiles, paint maps of several layers in Game Studio, animate characters and objects from the same tiles, and draw it all in a Stride game, as the 2D role-playing games of the 16-bit consoles did. A tessera is one tile of a mosaic.

**Tessera is not affiliated with or endorsed by the Stride project.**

It lives on this branch of a Stride fork only to build against the engine's development version, whose plugin model it uses: no released Stride has that plugin model yet. It is meant to move to a repository of its own, so it uses nothing of this repository but Stride's packages (see [Tied to the fork](#tied-to-the-fork)).

| Package | What it holds |
|---|---|
| `ColorRise.Tessera` | The runtime: tile sets, tile maps, their components and their drawing. A game references this one. |
| `ColorRise.Tessera.Assets` | The tile set and tile map assets, their compilers and templates (loaded by the asset compiler and Game Studio). |
| `ColorRise.Tessera.Editor` | Game Studio: the editors of tile sets, tile maps and flipbooks (any toolkit). |
| `ColorRise.Tessera.Editor.Avalonia` | The views of the cross-platform Game Studio. |

The runtime declares the last three as its companions: whoever loads a game that references `ColorRise.Tessera` (the asset compiler, Game Studio) loads them too. A game never references them itself.

The design is in [docs/design.md](docs/design.md), the plan of the work in [docs/plan.md](docs/plan.md), and what Tessera found in Stride's plugin model in [docs/plugin-findings.md](docs/plugin-findings.md).

## Try it

All paths are from the root of the Stride checkout. You need the .NET 10 SDK.

**1. Build the engine and the editor.** Tessera reaches Stride through packages only. A local engine build makes them: it puts `.nupkg` files in `bin/packages` at version `4.5.4-dev`.

```sh
dotnet build sources/Stride.Editor.Avalonia.slnf
dotnet build sources/assets/Stride.AssetCompiler/Stride.AssetCompiler.csproj
```

Both are Debug builds. Build the engine again after you pull engine changes.

**2. Build and test Tessera.**

```sh
dotnet build extensions/ColorRise.Tessera/ColorRise.Tessera.slnx
dotnet test extensions/ColorRise.Tessera/ColorRise.Tessera.slnx
```

To build against another Stride version, copy `Directory.Build.local.props.example` to `Directory.Build.local.props` (git-ignored) and set `TesseraStrideVersion`.

**3. Use it in a game of your own.** `dotnet pack extensions/ColorRise.Tessera/ColorRise.Tessera.slnx` puts the packages in `extensions/ColorRise.Tessera/bin/packages`.

### What works today

This section follows the work: it says what can be tried at the current commit.

- The four packages build and pack. Game Studio's test session loads the editor plugin. Nothing else yet.

### Not yet

- Everything of the design: tile sets, tile maps, their drawing, the editors, flipbooks and the sample game.
- A check on Windows: everything here is built and run on Linux. The WPF Game Studio gets none of Tessera's views.

## Tied to the fork

What still depends on the checkout Tessera sits in, and what removes it:

- `nuget.config` takes the Stride packages from `../../bin/packages`, the output of a local engine build. Once a released Stride has the plugin model, nuget.org serves them and that source goes.
- `Directory.Build.targets` tells the build where the engine's asset compiler is when Tessera is built in another configuration than the engine (a Release pack after a Debug engine build). It only acts against a local engine build, and goes with it.
- The packages have a prerelease version, 0.1.0-dev, because they depend on a development version of Stride. They get a stable version with a released Stride.

## Credits and licenses

Tessera is under the MIT license: see [LICENSE](LICENSE). The licenses of what it ships from others are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
