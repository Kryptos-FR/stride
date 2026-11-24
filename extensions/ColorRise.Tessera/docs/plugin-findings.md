# Plugin model findings

What Tessera met in Stride's plugin model while being built as a third party would build it. Tessera never patches Stride: a gap is written here with what Tessera tried, what happened, the workaround kept inside Tessera, and the change suggested to Stride. A finding that blocks a feature leaves the feature out.

Tessera is the second extension of this kind, after [Skein](https://github.com/Kryptos-FR/stride/tree/feature/colorrise-skein/extensions/ColorRise.Skein). Where Tessera meets a point Skein already wrote about, the third table says only whether Skein's finding still holds. Tessera draws, which Skein did not: its findings are mostly about render features, batches, shaders and graphics tests.

Built on Stride's `master` at `659bb239e` (2026-11-24).

## Findings

| # | Plugin point | What Tessera tried | What happened | Workaround in Tessera | Suggested change |
|---|---|---|---|---|---|
| 1 | Graphics tests: the package `Stride.Dependencies.Lavapipe` (2026.10.12), the software Vulkan driver Stride's own graphics tests use | Reference it from `ColorRise.Tessera.Tests`, so that the tests draw without a GPU and without a window | It works: four tests draw and read their frames on Linux with `DISPLAY` unset. But every build of a project that references it warns NETSDK1206, because the package puts a second Linux driver under a made-up runtime identifier, `linux-x64-nodmabuf` | None: the warning is left visible | Put the second driver in a folder under `runtimes/linux-x64/native/` instead of under a runtime identifier of its own |
| 2 | Asset compilation in a test process: `PackageSession`, `PackageCompiler` and `Builder`, as Skein's tests compile Yarn projects | Build a tile map asset into a database inside `ColorRise.Tessera.Tests` | The first `PackageSession` throws `TypeLoadException: Could not load type 'DisposableWriteLock' from assembly 'Microsoft.Build.Framework, Version=15.1.0.0'`. `Stride.Core.Assets` keeps the runtime of the MSBuild packages out of its own output (`ExcludeAssets="runtime" PrivateAssets="all"`), but its `Microsoft.CodeAnalysis.Workspaces.MSBuild` brings `Microsoft.Build.Framework` 17.11.48 into the output of every project that references it. With SDK 10.0.404, `MSBuildLocator` then loads that old copy beside the SDK's MSBuild 18 | The test project references `Microsoft.Build.Framework` 17.11.48 with `ExcludeAssets="runtime"` | `Stride.Core.Assets` (or the dev package of it) carries the exclusion to its consumers, for instance with a `buildTransitive` target that removes `Microsoft.Build*.dll` from the copied runtime assets |
| 3 | Asset YAML: a member typed `object`, here the properties of a tile in the tile set asset (`Dictionary<string, object>` of bool, int, float, string and color) | Save the asset and load it back | A `float` comes back as a `double`, and strings that look like numbers or booleans (`"3"`, `"true"`) come back as an `int` and a `bool`. The serializer writes a primitive without a tag when its type is the default for the YAML scalar (`float` shares the tag `!!float` with `double`), and writes the string without quotes | The asset keeps its properties as typed values (`TileProperty`: `TileIntProperty`, `TileFloatProperty`, and so on), each with a tag of its own; the compiler turns them into the plain values of `Tile.Properties` | In a member typed `object`, write a tag for every primitive that is not the type the scalar reads back as, and quote a string that would read as another type |
| 4 | Textures built by a third party's compiler: the tile set compiler packs its tiles with `TexturePacker` and `AtlasTextureFactory`, reads images with `TextureTool`, and writes textures with `TextureHelper.ImportTextureImage`, as the sprite sheet compiler does | Use them from Tessera's `Assets` package | They work: all are public, and the asset compiler has the texture tools' native libraries. Two gaps: `TextureSerializationData.Version` is internal, so a third party's command cannot hash it and build its textures again when Stride's texture format changes, as `SpriteSheetCommand` does; and `TextureHelper.ImportParameters` has no constructor of its own, so the compiler makes a `TextureAsset` and a `TextureConvertParameters` only to get one | The command's version is raised by hand; the compiler makes the texture asset | Make `TextureSerializationData.Version` public, or give `AssetCommand` a way to add it to the hash; give `ImportParameters` a public constructor |
| 5 | Tests that read a texture a build wrote, without a graphics device | Load the tile set without its textures, then read a texture's pixels | `ImageTextureSerializer`, which reads a texture as an `Image`, is internal. `ContentManagerLoaderSettings.ContentFilter`, which keeps references from loading, is a public delegate whose parameter, `ContentReference`, is internal: only a lambda without parameter types can implement it | The test reads the texture's chunk itself (`ChunkHeader` is public) and gives the image to `Image.Load`; its filter is `(_, ref load) => load = false` | Make `ImageTextureSerializer` public, or let a content manager read a texture as an image; make `ContentReference` public |

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
