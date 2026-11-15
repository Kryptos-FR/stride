# ColorRise.Saga

A story system for [Stride](https://www.stride3d.net) games, by Color-Rise: a branching main plot cut into acts, scenarios and quests inside the acts, and dialogues between the player and NPCs (or between NPCs) that become available when the story allows it. Game code talks to it through names the story declares: it raises events, sets variables, provides functions and listens to signals.

Saga is not affiliated with the Stride project. It lives on this branch of a Stride fork only to build against the engine's development version, whose plugin model it uses and tests: no released Stride has that plugin model yet.

| Package | What it holds |
|---|---|
| `ColorRise.Saga` | The runtime: compiled stories, the story runtime, the dialogue sessions, the character component. A game references this one only. |
| `ColorRise.Saga.Assets` | The story and dialogue assets, their compilers and templates (loaded by the asset compiler and Game Studio). |
| `ColorRise.Saga.Editor` | Game Studio: the story and dialogue editors, the story player, the commands (any toolkit). |
| `ColorRise.Saga.Editor.Avalonia` | The views of the cross-platform Game Studio. |

The runtime declares the other three as its companions: whoever loads a game that references `ColorRise.Saga` (the asset compiler, Game Studio) loads them too, either as projects of the game's solution or as packages from a feed. A game never references them itself.

All paths below are from the root of the Stride checkout. You need the .NET 10 SDK.

## 1. Build the engine and the editor

Saga reaches Stride through packages only, as any third party would. A local engine build makes them: it puts `.nupkg` files in `bin/packages` at version `<major>.<minor>.<patch>-dev` (4.5.3-dev today), and those packages point at the engine's build output.

```sh
dotnet build sources/Stride.Editor.Avalonia.slnf
dotnet build sources/assets/Stride.AssetCompiler/Stride.AssetCompiler.csproj
```

Both are Debug builds, which the Game Studio path below assumes. Build the engine again after you pull engine changes.

## 2. Build and test Saga

```sh
dotnet build extensions/ColorRise.Saga/ColorRise.Saga.slnx
dotnet test extensions/ColorRise.Saga/ColorRise.Saga.slnx
```

`extensions/ColorRise.Saga/nuget.config` takes the Stride packages from `bin/packages` and the rest from nuget.org. The Stride version is read from the checkout. To use another one, copy `Directory.Build.local.props.example` to `Directory.Build.local.props` (git-ignored) and set `SagaStrideVersion`.

## 3. Open the sandbox in Game Studio

The sandbox (`extensions/ColorRise.Saga/sandbox/SagaSandbox`) is a small game with a story, two dialogues and a scene of NPCs. Open its solution, `SagaSandbox.slnx`, in the cross-platform Game Studio of the engine build:

```sh
dotnet sources/editor/Stride.GameStudio.Avalonia.Desktop/bin/Debug/net10.0/Stride.GameStudio.Avalonia.Desktop.dll extensions/ColorRise.Saga/sandbox/SagaSandbox/SagaSandbox.slnx
```

(On Windows, `Stride.GameStudio.Avalonia.Desktop.exe` in the same folder, or File > Open in Game Studio.)

The solution holds the four Saga projects next to the game's. Game Studio builds them when it opens the solution, then rebuilds and reloads the editor ones when you change them, so you can work on Saga and see it in the editor without packing anything. The Plugins panel lists `ColorRise.Saga.Editor` and `ColorRise.Saga.Editor.Avalonia` as joined.

The game projects alone are not enough: the game references the runtime project, and Game Studio then looks for its companions in the solution, then as packages in the feeds. With neither, it says `Editor package [ColorRise.Saga.Editor] version [0.1.0], declared by [ColorRise.Saga], was not found` and every Saga asset fails to load.

The WPF Game Studio gets no Saga editors: by design it opens the sandbox and shows Saga's assets in its property grid (not checked yet).

## 4. Pack

```sh
dotnet pack extensions/ColorRise.Saga/ColorRise.Saga.slnx
```

This packs the four packages, at the `Version` set in `Directory.Build.props` (0.1.0), into `extensions/ColorRise.Saga/bin/packages`. Release (the default of `dotnet pack`) works after a Debug engine build: the asset compiler is taken from the engine's configuration (`Directory.Build.targets`).

The packages depend on the Stride packages of the engine build (4.5.3-dev), so they work against that build only, until a released Stride has the plugin model.

## 5. Use the packages in another game

Reference the runtime and nothing else from Saga:

```xml
<PackageReference Include="ColorRise.Saga" Version="0.1.0" />
```

Then give the game a `nuget.config` with both local feeds, so that its restore, the asset compiler and Game Studio find the Saga packages and the Stride packages they depend on:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="stride-dev" value="<stride checkout>/bin/packages" />
    <add key="saga" value="<stride checkout>/extensions/ColorRise.Saga/bin/packages" />
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
    <packageSource key="saga">
      <package pattern="ColorRise.Saga" />
      <package pattern="ColorRise.Saga.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

NuGet keeps a package of a given version once it has it. After packing again at the same version, delete the `colorrise.saga*` folders from the NuGet cache (`~/.nuget/packages`, or `%UserProfile%\.nuget\packages` on Windows) or raise `Version`.

On Linux and macOS, the asset compiler and Game Studio restore the companions themselves, and that restore cannot check the signatures of some nuget.org packages the companions depend on (`System.Threading` 4.3.0 and the like): the build fails with `Could not restore package [ColorRise.Saga.Assets]` followed by signature messages (see [finding 7](docs/plugin-findings.md)). Restoring those packages once through the SDK works around it: a throwaway project next to the game's `nuget.config` that references the three companions with `<RestoreEnablePackagePruning>false</RestoreEnablePackagePruning>`, then `dotnet restore` on it.

## What v1 does and doesn't

It does:

- Stories of acts, scenarios and quests, and dialogues of lines, choices, branches and ends, written as assets and compiled with the game. Conditions and actions are short expressions checked against the story's declarations at build time, with the asset, card and position of each mistake.
- A runtime that settles the story after each event, variable change or dialogue step, saves and restores its state, and plays dialogues with an NPC through a character component.
- In the cross-platform Game Studio: a story editor (plot, act, scenario, with a breadcrumb) and a dialogue editor on node graphs, with links, positions and every change undoable; a story player panel to play a story in the editor; "Play story" on a story in the asset view and "Open dialogue" on an NPC in the scene, also in the command palette.

It doesn't (yet):

- Sub-plots running in parallel with their own timelines, localization and voice-over, barks and ambient lines, typed names or attributes from game code.
- A dialogue UI for the game: the game shows the lines and choices itself.
- Saving in the middle of a dialogue: the saved state holds no dialogue position, and a dialogue played once is used up as soon as it starts. A game saves between dialogues.
- Editors in the WPF Game Studio.
- Published packages: everything builds against a local engine build of this branch.

## Documents

- [Design](docs/design.md) and [implementation plan](docs/plan.md) of the first version.
- [Plugin model findings](docs/plugin-findings.md): what Saga met in Stride's plugin model.
