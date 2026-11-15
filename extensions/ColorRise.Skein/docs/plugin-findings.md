# Plugin model findings

What Skein met in Stride's plugin model while being built as a third party would build it. Skein never patches Stride: a gap is written here with what Skein tried, what happened, the workaround kept inside Skein, and the change suggested to Stride. A finding that blocks a feature leaves the feature out.

Based on `feature/xplat/plugin-follow-ups` (17f6ee7d7, 2026-11-15).

## Findings

| # | Plugin point | What Skein tried | What happened | Workaround in Skein | Suggested change |
|---|---|---|---|---|---|
| 1 | Building a plugin against a local engine build | `dotnet pack` (Release by default) of the solution after a Debug engine build | `Stride AssetCompiler could not be found (.../Stride.AssetCompiler/bin/Release/net10.0/Stride.AssetCompiler.dll)`: the asset compiler's targets look for the compiler in the project's `$(Configuration)`, while the dev package stubs take every assembly from the engine's (`$(StrideDevConfiguration)`). Saga's finding 2, still there on 17f6ee7d7 | `Directory.Build.targets` sets `StrideCompileAssetCommand` from `$(StrideDevConfiguration)` in dev-redirect builds (T2 below) | `_StridePrepareAssetCompiler` could use `$(StrideDevConfiguration)` when it is set, as the stubs' references do. Dev workflow only; published packages are not affected. |
| 2 | Restoring a project that references Stride's packages | `dotnet build` of the solution | Each project gets NuGet's vulnerability warnings NU1902 and NU1903 for `SixLabors.ImageSharp` 3.1.12, which Stride's packages bring; a third party that builds with warnings as errors must silence them | None: the warnings are left visible | Stride could move to a fixed ImageSharp, or keep it out of the packages a plugin references. |
| 3 | Asset templates (`TemplateAssetFactory` in an `.sdtpl`) | A template with `DefaultOutputName: Dialogue`, used by *Add asset* in a folder named `Dialogue` | The asset view shows the new asset in the folder, but its location is `Dialogue` at the root and it is saved as `Assets/Dialogue.yarn`: `AssetTemplateGenerator.GenerateLocation` takes a name that starts with the target folder's path for a full location (`assetName.StartsWith(parameters.TargetLocation)`). Stride's own templates do the same (a "Material" in a folder `Material`) | The template's default name is `YarnScript`, which is unlikely to be a folder's name | `GenerateLocation` could compare whole directory segments, or always combine the folder and the name. |
| 4 | `SourceCodeAsset` for a third party's text format | Saving a `.yarn` script whose text was loaded or changed | The file gets a UTF-8 byte order mark (`DefaultTextAccessor.Save` writes with `Encoding.UTF8`), whatever the file had; a file never read is copied as it is. Yarn Spinner's tools write no mark, so every script touched in Game Studio shows a change of its first bytes | `YarnScriptAsset` overrides `Save(Stream)`: UTF-8, with a mark only when the file came with one | `DefaultTextAccessor` could keep what the file had, and write no mark for a new file. |
| 5 | Editor test kit (`Stride.Assets.Editor.Testing`) | Three xUnit test classes that each open kit sessions, run as xUnit runs them by default (classes in parallel) | The test run hangs, with no failure and no message: the kit's sessions share one dispatcher and process-wide registries. Two classes had passed, so it showed up late. Stride's own test projects turn parallel runs off, but the kit's documentation does not say a plugin's tests must | `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `ColorRise.Skein.Editor.Tests` | Say it in the kit's documentation, or have the kit fail with a message when a second session opens from another thread while one is open. |

## Tied to the fork

Skein is meant to move to a repository of its own. What still depends on the checkout it sits in, and what Stride would have to publish to remove it:

| # | What | Why | Goes when |
|---|---|---|---|
| T1 | `nuget.config` has a source `stride-dev` pointing at `../../bin/packages` | The Stride packages with the plugin model exist only as the output of a local engine build | A released Stride has the plugin model; nuget.org then serves the packages |
| T2 | `Directory.Build.targets` names the asset compiler under `$(StrideRoot)sources/assets/Stride.AssetCompiler/bin/` | Finding 1. Only in dev-redirect builds, from properties the dev package stubs set; nothing is read from the tree by Skein itself | Finding 1 is fixed, or Skein builds against published packages |

## Saga's findings that the follow-ups fixed

Checked off when Skein uses the fix (the numbers are those of ColorRise.Saga's findings).

| Saga # | Finding | Fix used by Skein |
|---|---|---|
