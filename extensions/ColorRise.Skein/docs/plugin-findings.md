# Plugin model findings

What Skein met in Stride's plugin model while being built as a third party would build it. Skein never patches Stride: a gap is written here with what Skein tried, what happened, the workaround kept inside Skein, and the change suggested to Stride. A finding that blocks a feature leaves the feature out.

Based on `feature/xplat/plugin-follow-ups` (17f6ee7d7, 2026-11-15).

## Findings

| # | Plugin point | What Skein tried | What happened | Workaround in Skein | Suggested change |
|---|---|---|---|---|---|
| 1 | Building a plugin against a local engine build | `dotnet pack` (Release by default) of the solution after a Debug engine build | `Stride AssetCompiler could not be found (.../Stride.AssetCompiler/bin/Release/net10.0/Stride.AssetCompiler.dll)`: the asset compiler's targets look for the compiler in the project's `$(Configuration)`, while the dev package stubs take every assembly from the engine's (`$(StrideDevConfiguration)`). Saga's finding 2, still there on 17f6ee7d7 | `Directory.Build.targets` sets `StrideCompileAssetCommand` from `$(StrideDevConfiguration)` in dev-redirect builds (T2 below) | `_StridePrepareAssetCompiler` could use `$(StrideDevConfiguration)` when it is set, as the stubs' references do. Dev workflow only; published packages are not affected. |
| 2 | Restoring a project that references Stride's packages | `dotnet build` of the solution | Each project gets NuGet's vulnerability warnings NU1902 and NU1903 for `SixLabors.ImageSharp` 3.1.12, which Stride's packages bring; a third party that builds with warnings as errors must silence them | None: the warnings are left visible | Stride could move to a fixed ImageSharp, or keep it out of the packages a plugin references. |

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
