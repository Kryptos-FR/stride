# ColorRise.Saga

A story system for [Stride](https://www.stride3d.net) games, by Color-Rise: a branching main plot cut into acts, scenarios and quests inside the acts, and dialogues between the player and NPCs (or between NPCs) that become available when the story allows it. Game code talks to it through names the story declares: it raises events, sets variables, provides functions and listens to signals.

Saga is not affiliated with the Stride project. It lives on this branch of a Stride fork only to build against the engine's development version, whose plugin model it uses and tests.

| Package | What it holds |
|---|---|
| `ColorRise.Saga` | The runtime: compiled stories, the story runtime, the dialogue sessions, the character component. A game references this one. |
| `ColorRise.Saga.Assets` | The story and dialogue assets, their compilers and templates (loaded by the asset compiler and Game Studio). |
| `ColorRise.Saga.Editor` | Game Studio: the story and dialogue editors, the story player, the commands (any toolkit). |
| `ColorRise.Saga.Editor.Avalonia` | The views of the cross-platform Game Studio. |

## Building

Saga references Stride through packages only. Against the Stride checkout it sits in:

1. Build the engine and the cross-platform editor once (`dotnet build sources/Stride.Editor.Avalonia.slnf` and `dotnet build sources/assets/Stride.AssetCompiler/Stride.AssetCompiler.csproj` from the repository root): a local build puts its packages in `bin/packages`, at version `<major>.<minor>.<patch>-dev`.
2. `dotnet build extensions/ColorRise.Saga/ColorRise.Saga.slnx` and `dotnet test extensions/ColorRise.Saga/ColorRise.Saga.slnx`, in the configuration the engine was built in (Debug by default).

The Stride version is read from the checkout. To use another one, copy `Directory.Build.local.props.example` to `Directory.Build.local.props` (git-ignored) and set `SagaStrideVersion`.

## Documents

- [Design](docs/design.md) and [implementation plan](docs/plan.md) of the first version.
- [Plugin model findings](docs/plugin-findings.md): what Saga met in Stride's plugin model.
