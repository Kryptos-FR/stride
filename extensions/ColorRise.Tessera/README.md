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

This section follows the work: it says what can be tried at the current commit. Step 1 of the design is done: the runtime, the assets and their compilers. There is no editor yet, so asset files are written by hand. The game of the packages check, `tests/PackagesCheck/Game`, has a tile set, a map and the code that draws them.

- **Tile sets from images.** A tile set asset (`.sdtileset`) cuts images into tiles. A grid source cuts an image by tile size, margin and spacing. A sprite sheet source takes the sprites of a sprite sheet asset and binds each, by name, to a tile. The build packs the tiles into textures of 2048 by 2048 pixels at most, with a border of 2 pixels around each tile that repeats its edge. It leaves out empty tiles and makes a color key transparent. Each tile keeps its collision, its kind, its properties and its animation. The build warns about sprites that are missing or not bound, about animation frames that name a tile the set does not have, and about tile data given twice or for a tile the set does not have. Tile data can be in any order in the file.
- **Tile maps.** A tile map asset (`.sdtilemap`) has tile sets in numbered slots and layers of chunks of 16 by 16 cells. Each row of a chunk is one line of text (`1/1:2,0 1/1:3,0 ...`, with `h`, `v` and `d` for flips), so a change of one cell changes one line of the file and a merge stays readable. The build checks the chunks and names the layer and the chunk of an error.
- **Maps in a game.** `Content.Load<TileMap>("Maps/Town")` reads a built map with its tile sets. A map can also be made in code: `TileSet.FromTexture(texture, tileSize)`, then `map.Layers.Add("Ground").SetCell(cell, tileSet, tileId)`. A cell changed at run time shows in the next frame. `Content.Reload` replaces a map in place and raises `TileMap.Reloaded`.
- **Drawing.** `TileMapComponent` draws the layers of a map in Stride's transparent stage, each layer at a depth of its own. A sprite, a flipbook or another map with a depth between two layers is drawn between them under any camera once the transparent stage of the game's compositor sorts with `TileAxisSortMode` ("Back to front along an axis", see the compositor below). With the engine's default sort, that holds only under a camera that looks straight at the map: under a tilted one, the layers keep their order, but a sprite between two layers can be drawn over or under both, depending on where it stands (finding 8 in [docs/plugin-findings.md](docs/plugin-findings.md)). Each layer has a tint, an opacity, an offset and can be hidden. Each cell can be flipped. Point sampling (the default, for pixel art) or linear. Only the chunks in view are drawn. Tiles meet without a gap at any zoom and position, and no pixel of a neighbouring tile shows. With MSAA, that holds while a tile is drawn on at least a quarter of its pixels (a tile of 16 pixels on 4 pixels of the screen); smaller, the sampler can read past the border of 2 pixels.
- **Prefabs.** A map and its tile sets are content: the entities cloned from a prefab share them, as they share models and textures. `TileMap.Clone()` makes a copy that a game can change on its own.
- **Animated tiles**, such as water or torches: frames with their durations, played by the clock of the component, whose speed can be changed.
- **Questions a game asks the map.** `WorldToCell`, `CellToWorld`, `GetLayerZ`, `IsSolid(cell)`, and `OverlapsSolid(rectangle)` for simple collisions against tiles marked solid.
- **The compositor.** A game adds `TileRenderFeature` to its graphics compositor, with a `TileTransparentRenderStageSelector` on its transparent stage, as `Compositor` in `tests/PackagesCheck/Game/Program.cs` does. In Game Studio, they should be added in the compositor editor, under render features; this was not tried, as step 1 did not run Game Studio. The forward renderer needs an opaque stage too, even when it draws nothing. For a sprite or a flipbook between two layers under a tilted camera (Game Studio's scene view has one), set the sort mode of the transparent stage to `TileAxisSortMode` ("Back to front along an axis"), whose axis is Z by default: it orders everything of that stage by its position along the axis instead of by its distance to the camera's plane, which is right for flat things in the XY plane and costs nothing more than the default sort. A 3D object that is transparent and stands across the axis is sorted by its centre along the axis too. For pixel art, leave out the post effects: tone mapping and the others change the colours of everything drawn before them.
- **From packages only.** `extensions/ColorRise.Tessera/tests/PackagesCheck/run.sh` packs Tessera, copies a small game to a new folder outside the checkout, builds it with only Tessera's and Stride's packages (the asset compiler builds its tile set and its map), draws one frame without a window and checks four pixels. It prints `TESSERA-PACKAGES-CHECK OK`. Set `STRIDE_FEED` to build the game against other Stride packages than those of the local engine build; Tessera itself is still packed against the local build.

### Not yet

- The editors in Game Studio: the tile set editor, the map editor with its brushes, and the flipbook editor. Templates to create the assets from Game Studio.
- The render feature given by the plugin, so that a compositor created in Game Studio gets it without a step by hand.
- Flipbooks: characters and objects animated from the tiles of a tile set, with their component.
- The sample game with Kenney's art.
- A check on Windows: everything here is built and run on Linux, with a software Vulkan driver.
- The WPF Game Studio, which gets none of Tessera's views.

## Tied to the fork

What still depends on the checkout Tessera sits in, and what removes it:

- `nuget.config` takes the Stride packages from `../../bin/packages`, the output of a local engine build. Once a released Stride has the plugin model, nuget.org serves them and that source goes.
- `Directory.Build.targets` tells the build where the engine's asset compiler is when Tessera is built in another configuration than the engine (a Release pack after a Debug engine build). It only acts against a local engine build, and goes with it.
- The packages have a prerelease version, 0.1.0-dev, because they depend on a development version of Stride. They get a stable version with a released Stride.

## Credits and licenses

Tessera is under the MIT license: see [LICENSE](LICENSE). The licenses of what it ships from others are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
