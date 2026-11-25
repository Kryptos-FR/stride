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

This section follows the work: it says what can be tried at the current commit. Steps 1 and 2 of the design are done: the runtime, the assets and their compilers, then the editors of tile sets and tile maps in the cross-platform Game Studio. The game of the packages check, `tests/PackagesCheck/Game`, has a tile set, a map and the code that draws them. The check game `tests/StudioCheck` opens in Game Studio (see its README), and [docs/studio-check.md](docs/studio-check.md) says what was seen in it.

In Game Studio:

- **New assets.** *Add asset > Sprite* has `Tile set` and `Tile map`. A new map has one layer, `Ground`.
- **The tile set editor** opens on a double click on a tile set. Its sources are listed on the left, and the selected one is shown whole on the canvas, at the zoom that fits it. `+ Image` asks for an image and shows a bar with its name, tile size, margin, spacing and the number of tiles it gives, until `Add` makes the source. `+ Sprite sheet`, or a sprite sheet dragged from the asset view onto the canvas, adds a sheet source; an image file dragged onto the canvas is proposed as `+ Image` does (not tried by hand: the check ran without a file manager). In `Select` mode, a click or a drag selects tiles, and the property grid shows their collision, kind and properties (bool, int, float, string or colour), for one tile or several at once, in one undo step named `Tile data`. In `Solid` mode, a press or a drag sets the tiles solid or not, hatched, in one step. A tile whose sprite is gone from its sheet shows as missing and can take another sprite. `Refresh` reads the images again, and what is wrong (an image that is not found, a sprite that is not bound) shows under the canvas.
- **The map editor** opens on a double click on a map, with the grid. `+` beside the tile sets adds one through the asset picker; the palette shows its sources, and a click or a drag in the palette picks a stamp of one tile or a block. The tools are Stamp, Rectangle, Fill, Erase, Pick and Select (move, cut, copy, paste and delete of a rectangle of cells); the stamp can be flipped and turned. The layers can be added, removed, moved up and down, renamed (double click), hidden (the eye is the layer's `Visible`) and locked, and `Dim other layers` fades the others. Each stroke is one undo step, and a save writes only the chunks it changed: one more cell changes one line of the file. The keys are below.
- **Commands.** The tools and the modes are commands of Game Studio: the command palette (Ctrl+Shift+P) lists them under "Tessera:", and *Edit > Keyboard shortcuts...* gives them shortcuts (Ctrl+1 for `Stamp tool`, for example).
- **The scene editor.** *Create entity > 2D > Tile map* makes an entity with the component, a map dropped from the asset view makes one with the map set, and *Add component* offers `Tile map`. The scene view draws the map and shows a saved change of it at once. A click on a tile selects the map's entity; a click where a layer has no tile selects what is behind.
- **The compositor editor.** *Add package features* adds `TileRenderFeature` with its selector on the transparent stage, and the sort mode of a stage can be set to "Back to front along an axis" (see [Setting up the compositor](#setting-up-the-compositor)).

In the runtime and the build:

- **Tile sets from images.** A tile set asset (`.sdtileset`) cuts images into tiles. A grid source cuts an image by tile size, margin and spacing. A sprite sheet source takes the sprites of a sprite sheet asset and binds each, by name, to a tile. The build compiles the sprite sheet itself too, so a sheet that only a tile set uses is still in the game (row 14 of `docs/plugin-findings.md`). The build packs the tiles into textures of 2048 by 2048 pixels at most, with a border of 2 pixels around each tile that repeats its edge. It leaves out empty tiles and makes a color key transparent. Each tile keeps its collision, its kind, its properties and its animation. The build warns about sprites that are missing or not bound, about animation frames that name a tile the set does not have, about tile data given twice or for a tile the set does not have, about two sources with the same id (the first is built), and about a pixels per unit that is not above 0 (16 is used). Tile data can be in any order in the file.
- **Tile sets in a map.** The build warns about a slot given twice in a map (the first is used).
- **Tile maps.** A tile map asset (`.sdtilemap`) has tile sets in numbered slots and layers of chunks of 16 by 16 cells. Each row of a chunk is one line of text (`1/1:2,0 1/1:3,0 ...`, with `h`, `v` and `d` for flips), so a change of one cell changes one line of the file and a merge stays readable. The build checks the chunks and names the layer and the chunk of an error.
- **Maps in a game.** `Content.Load<TileMap>("Maps/Town")` reads a built map with its tile sets. A map can also be made in code: `TileSet.FromTexture(texture, tileSize)`, then `map.Layers.Add("Ground").SetCell(cell, tileSet, tileId)`. A cell changed at run time shows in the next frame. `Content.Reload` replaces a map in place and raises `TileMap.Reloaded`.
- **Drawing.** `TileMapComponent` draws the layers of a map in Stride's transparent stage, each layer at a depth of its own. A sprite, a flipbook or another map with a depth between two layers is drawn between them under any camera once the transparent stage of the game's compositor sorts with `TileAxisSortMode` ("Back to front along an axis", see the compositor below). With the engine's default sort, that holds only under a camera that looks straight at the map: under a tilted one, the layers keep their order, but a sprite between two layers can be drawn over or under both, depending on where it stands (finding 8 in [docs/plugin-findings.md](docs/plugin-findings.md)). Each layer has a tint, an opacity, an offset and can be hidden. Each cell can be flipped. Point sampling (the default, for pixel art) or linear. Only the chunks in view are drawn. Tiles meet without a gap at any zoom and position, and no pixel of a neighbouring tile shows. With MSAA, that holds while a tile is drawn on at least a quarter of its pixels (a tile of 16 pixels on 4 pixels of the screen); smaller, the sampler can read past the border of 2 pixels.
- **Prefabs.** A map and its tile sets are content: the entities cloned from a prefab share them, as they share models and textures. `TileMap.Clone()` makes a copy that a game can change on its own.
- **Animated tiles**, such as water or torches: frames with their durations, played by the clock of the component, whose speed can be changed.
- **Questions a game asks the map.** `WorldToCell`, `CellToWorld`, `GetLayerZ`, `IsSolid(cell)`, and `OverlapsSolid(rectangle)` for simple collisions against tiles marked solid.
- **The compositor** needs Tessera's render feature: see [Setting up the compositor](#setting-up-the-compositor).
- **From packages only.** `extensions/ColorRise.Tessera/tests/PackagesCheck/run.sh` packs Tessera, copies a small game to a new folder outside the checkout, builds it with only Tessera's and Stride's packages (the asset compiler builds its tile set and its map), draws one frame without a window and checks four pixels. It prints `TESSERA-PACKAGES-CHECK OK`. Set `STRIDE_FEED` to build the game against other Stride packages than those of the local engine build; Tessera itself is still packed against the local build.

### Not yet

- Flipbooks: characters and objects animated from the tiles of a tile set, with their component and their editor, and the Animation mode of the tile set editor (the frames of an animated tile are written in the file for now).
- The sample game with Kenney's art.
- Painting in the scene view: a map is painted in its own editor.
- Thumbnails that show the tiles: the asset view shows an icon for tile sets and maps.
- Rebinding the map editor's keys: the commands can get shortcuts, but the single keys stay as they are (finding 13).
- The WPF Game Studio, which gets none of Tessera's views and was not checked at all.
- A check on Windows: everything here is built and run on Linux, with a software Vulkan driver.

## Setting up the compositor

A game draws its maps once its graphics compositor has `TileRenderFeature`, with a `TileTransparentRenderStageSelector` on its transparent stage.

- **In Game Studio**, open the compositor and use *Add package features* above the render features: it adds the feature with its selector.
- **In code**, add them by hand, as `Compositor` in `tests/PackagesCheck/Game/Program.cs` does. The forward renderer needs an opaque stage too, even when it draws nothing.

**The sort mode.** A sprite or a flipbook whose z is between two layers is drawn between them under a camera that looks straight at the map with the engine's sort. Under a tilted or turned camera, as the scene view's, it can be drawn over or under both layers. To keep it between them under any camera, select the transparent stage in the compositor editor and set its sort mode to "Back to front along an axis" (`TileAxisSortMode`), whose axis is Z by default. It orders everything of that stage by its position along the axis instead of by its distance to the camera's plane, which is right for flat things in the XY plane, and it costs no more than the engine's sort. A 3D object that is transparent and stands across the axis is sorted by its centre along the axis too. The check game's pictures `studio-09-axis-sort.png` and `studio-09-back-to-front.png` show the two sorts (see [docs/studio-check.md](docs/studio-check.md), line 9).

For pixel art, leave out the post effects: tone mapping and the others change the colours of everything drawn before them.

## Keys of the map editor

These keys work while the map's canvas has the focus, and not while a layer's name is typed.

| Key | What it does |
|---|---|
| B | Stamp tool |
| P | Rectangle tool |
| F | Fill tool |
| E | Erase tool |
| I | Pick tool (an eyedropper: it goes back to Stamp) |
| R | Select tool |
| X, Y | Flip the stamp horizontally, vertically |
| Z, Shift+Z | Turn the stamp clockwise, counterclockwise (only a stamp of square tiles) |
| Delete | Empty the selected cells |
| Escape | Cancel the stroke in progress, else clear the selection |

With the Stamp tool, Shift and a click paint a line from the last painted cell, and a drag with the right button takes the cells under it as the stamp. With the Erase tool, a drag with the right button erases a rectangle. Ctrl and Alt with a key are left to Game Studio (Ctrl+Z is undo).

The keys cannot be rebound: Game Studio does not let a plugin's command have a shortcut of one key (finding 13). Each tool is also a command, which can get a shortcut with Ctrl, Alt or Meta in *Edit > Keyboard shortcuts...*.

## Speed of the map editor

The map's canvas draws a picture of each chunk of each layer, made once and kept while the chunk does not change. On a map of 256 by 256 cells and 4 layers (1024 chunk pictures), seen whole at the zoom 0.125, measured by `TileMapSceneTests` and `LargeMapTests` in the container, without a GPU (Skia on the CPU):

| What | Time |
|---|---|
| The list of what to draw, the first time: the 1024 pictures are made | 130 ms |
| The same list again, from the pictures made | 1.1 ms |
| A frame of a pan, drawn in a window, on average | 29.7 ms |

## Tied to the fork

What still depends on the checkout Tessera sits in, and what removes it:

- `nuget.config` takes the Stride packages from `../../bin/packages`, the output of a local engine build. Once a released Stride has the plugin model, nuget.org serves them and that source goes.
- `Directory.Build.targets` tells the build where the engine's asset compiler is when Tessera is built in another configuration than the engine (a Release pack after a Debug engine build). It only acts against a local engine build, and goes with it.
- The packages have a prerelease version, 0.1.0-dev, because they depend on a development version of Stride. They get a stable version with a released Stride.

## Credits and licenses

Tessera is under the MIT license: see [LICENSE](LICENSE). The licenses of what it ships from others are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
