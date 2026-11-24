# Plugin model findings

What Tessera met in Stride's plugin model while being built as a third party would build it. Tessera never patches Stride: a gap is written here with what Tessera tried, what happened, the workaround kept inside Tessera, and the change suggested to Stride. A finding that blocks a feature leaves the feature out.

Tessera is the second extension of this kind, after [Skein](https://github.com/Kryptos-FR/stride/tree/feature/colorrise-skein/extensions/ColorRise.Skein). Where Tessera meets a point Skein already wrote about, the third table says only whether Skein's finding still holds. Tessera draws, which Skein did not: its findings are mostly about render features, batches, shaders and graphics tests.

Built on Stride's `master` at `659bb239e` (2026-11-24).

## Findings

| # | Plugin point | What Tessera tried | What happened | Workaround in Tessera | Suggested change |
|---|---|---|---|---|---|

## Tied to the fork

Tessera is meant to move to a repository of its own. What still depends on the checkout it sits in, and what Stride would have to publish to remove it:

| # | What | Why | Goes when |
|---|---|---|---|
| T1 | `nuget.config` has a source `stride-dev` pointing at `../../bin/packages` | The Stride packages with the plugin model exist only as the output of a local engine build | A released Stride has the plugin model; nuget.org then serves the packages |
| T2 | `Directory.Build.targets` names the asset compiler under `$(StrideRoot)sources/assets/Stride.AssetCompiler/bin/` | Skein's finding 1. Only in dev-redirect builds, from properties the dev package stubs set; nothing is read from the tree by Tessera itself | Skein's finding 1 is fixed, or Tessera builds against published packages |
| T3 | Tessera's packages depend on a prerelease Stride (4.5.4-dev), so they have a prerelease version too (0.1.0-dev); a stable version on a prerelease dependency makes `dotnet pack` warn NU5104 | No released Stride has the plugin model | A released Stride: Tessera then gets a stable version |

## Skein's findings, met again

The numbers are those of Skein's `docs/plugin-findings.md`.

| Skein # | Where Tessera met it | Holds |
|---|---|---|
| 2 | Every project, at restore | Yes, on `659bb239e`: NU1902 and NU1903 for `SixLabors.ImageSharp` 3.1.12, and now also for `System.Security.Cryptography.Pkcs` 8.0.1. Left visible, as in Skein |
| 5 | `ColorRise.Tessera.Editor.Tests` | Not tried: the tests turn parallel runs off from the start, as Skein's do |
| 13 | `ColorRise.Tessera.Editor.Avalonia.Tests` | Not tried: the project skips the asset compiler from the start, as Skein's does |
