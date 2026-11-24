# Tile maps for Stride: design of the first version

**Approved by Nicolas on 2026-11-24** (third version), with the name `ColorRise.Tessera` and Kenney's art for the sample. Written for Nicolas, and for the builder who will plan and write the plugin after him. Nicolas answered the four choices D1 to D4 on 2026-11-24 (section 5), and this version follows his answers. The largest change is D1: the plugin is a third-party extension like Skein, so that it proves the plugin model again.

Marks: **(N)** from Nicolas's request, **(D)** a choice Nicolas made, **(C)** my call, open to correction.

Checked against the fork's `master` at `659bb239e` (Stride 4.5.4) and against Skein on `feature/colorrise-skein`. A fresh reader checked the first draft against the engine source as a builder would. It found two wrong claims about how Stride orders sprites and four gaps, and its findings are folded in. The changes for a third-party extension were made after that check, and no second reader has read them.

**Decided by Nicolas** (2026-11-24):

- D1: a third-party extension, to prove the plugin model again.
- D2: a map is its own asset with its own editor tab.
- D3: no autotiling, no Tiled import and no physics in the first version. After it, in his order: terrain autotiling, Tiled support, physics or pathfinding (starting with walkable tiles and obstacles), parallax, then the rest.
- D4: animations are flipbooks, as section 8 describes them.
- The name `ColorRise.Tessera`, and Kenney's CC0 art for the sample ("I love that guy").

**My calls to check while reading** (each is marked **(C)** in its place):

1. The extension is called `ColorRise.Tessera` (a tessera is one tile of a mosaic; Nicolas agreed to the name). It lives in `extensions/ColorRise.Tessera/`, laid out as Skein is, and reaches Stride through packages only (section 7).
2. Nothing in the engine is changed by this work. A gap in the plugin model is written as a finding, with its workaround (section 10).
3. A cell of a map is addressed with Y going up, as the world's Y does, and one tile is one world unit by default (section 4).
4. A tile keeps a stable id: its column and row in a grid image, or a number bound to a sprite's name in a sprite sheet. Adding tiles or reordering sprites never changes a painted map (section 7).
5. A tile set packs all its tiles, those taken from sprite sheets included, into its own atlas with a padded border around each tile. A sprite sheet is used as a list of named regions: its own packing, pixels per unit and centers are not used (section 7).
6. In the map file, the cells of a chunk are one block of text (section 7).
7. Tile layers and flipbooks are drawn in the transparent stage, where the engine sorts by depth. A layer is a depth, and flipbooks at the same depth can sort by their Y. A `SpriteComponent` orders with them by its Z only (section 8).
8. The plugin draws with its own batch, so that neighbouring tiles share exact corners at any camera position. Its shader is the engine's public sprite effect, with one colour per draw call (section 8).
9. Animations are a new "flipbook" asset and component drawn by the plugin itself, not through `SpriteComponent`. A frame is a small composition of tiles, as old consoles composed a sprite (section 8).
10. Views are written for the Avalonia Game Studio only. The WPF Game Studio cannot be checked from here (section 9).
11. The tools' single keys (B, F, E and the others) are read by the map canvas, because a plugin's command cannot have a default shortcut without Ctrl, Alt or Meta (section 9).
12. Graphics tests read pixels back from a game without a window, on a software renderer. There are no gold images, and no CI builds the extension, as for Skein (section 14).
13. Four steps, each on its own branch stacked on the previous one. The first starts with the build from packages and four spikes on the main risks (sections 11 and 12).
14. The sample's art is a CC0 pack (Kenney), listed in the extension's third-party notices. Nicolas agreed (section 13).

## 1. Intent

Nicolas's request, in his words **(N)**:

- "Make another plugin for Stride: tileset or tilemap like the old 2d RPG games."
- "Both runtime support and design time/editor", that "can let you draw a map from one or several tileset".
- Tile sets "either come as a single image or a combination of images (which could also be grouped from the existing spritesheet editor)".
- "The same plugin could also help creating 2d animations from such set (again to mimic how old 2d rpg games worked, such as Zelda III for instance)."

So the plugin has three parts that share one idea, the tile:

1. **Tile sets**: images cut into tiles, with what the game needs to know about each tile.
2. **Tile maps**: layers of cells painted with tiles, drawn by the game and edited in Game Studio.
3. **Flipbooks**: animations whose frames are tiles, for characters and objects.

Success looks like this: someone opens Game Studio, adds a tile set from a PNG, paints a small overworld with three layers, drops it in a scene, adds a character that walks in four directions with a flipbook, and runs the game. No other tool is needed, and the map file can be read in a git diff.

## 2. What exists elsewhere

What I read today, and what I only remember, are kept apart. The sources are at the end.

| Tool | Tile set | Map | Editing | Animation |
|---|---|---|---|---|
| **Tiled** (read today) | An image cut on a grid (`tilewidth`, `tileheight`, `margin`, `spacing`, `columns`), or a collection of images, one per tile. Per tile: class, custom properties, collision shapes, animation, probability. | Layers (tile, object, image, group) with opacity, tint, offset and parallax. A cell is a 32-bit "global tile id": the tile set's `firstgid` plus the tile's local id, with flip flags in the top bits (horizontal, vertical, diagonal). Infinite maps store chunks of 16 by 16. Data as CSV or base64. | Stamp brush (B) that also captures a stamp from the map with the right button, terrain brush (T), bucket fill (F), shape fill (P), eraser (E). X and Y flip the stamp, Z rotates it. Random mode. | Per tile: a list of frames, each a tile id and a duration in milliseconds. |
| **Godot 4** (read today) | A `TileSet` resource with sources. An atlas source is a texture with margins, separation and a region size, and pads each tile by one pixel to stop bleeding (`use_texture_padding`). Set-level physics, navigation, terrain and custom data layers. | One `TileMapLayer` node per layer. A cell is a source id, the tile's coordinates in the atlas and an "alternative" id that also carries flip and transpose bits. Rendered in quadrants of 16 by 16, with optional Y sort. | Paint, line, rectangle, bucket, picker, eraser, random with scattering, saved patterns, terrain painting. | Per atlas tile: frames laid out in the atlas, a duration per frame, a speed, same or random start. Characters use `AnimatedSprite2D` with a `SpriteFrames` resource (from memory). |
| **Unity** (read today) | No tile set: each tile is an asset (sprite, colour, collider type). A "tile palette" is an asset that holds tiles to pick from. | A `Grid`, with one `Tilemap` and `Tilemap Renderer` per layer, stored in the scene. Chunk mode batches tiles, but then nothing can be drawn between tiles of different depths. | In the scene view, with a palette window: paint, box fill, pick, erase, flood fill. Rule tiles pick a sprite from the 3 by 3 neighbours. | Animated tiles and sprite animation clips (from memory). |
| **LDtk** (read today, JSON format only) | A grid image with `tileGridSize`, `spacing`, `padding`, custom data and enum tags per tile. | Levels placed in a world (free, grid, or in a line). Layer types: IntGrid, Tiles, Entities, AutoLayer. A tile instance has pixel positions, a tile id and two flip bits. | Auto-layers: rules match a pattern of 1, 3, 5 or 7 cells on IntGrid values, with flips, chance and modulo. | None in the tool. |
| **RPG Maker MV and MZ** (partly read, mostly memory) | Fixed sheets: A1 animated water, A2 ground, A3 buildings, A4 walls, A5 plain tiles, B to E upper tiles. A1 to A4 are autotiles. Per tile: passage, a "star" for tiles drawn over characters, ladder, bush, counter, damage, terrain tag. | Fixed size, a few layers that the editor manages by itself, events on cells. | Pencil, rectangle, ellipse, fill, shadow pen. | Characters are sheets of 3 frames by 4 directions, played 1, 2, 3, 2. |
| **Unreal Paper2D** (memory) | Tile set asset from a texture. | Tile map asset with layers and its own editor. | In a dedicated asset editor. | "Flipbook" assets: sprites with a duration each. One plugin holds sprites, flipbooks, tile sets and tile maps. |
| **MonoGame** (memory) | Nothing built in. | MonoGame.Extended loads and draws Tiled maps. | Tiled or LDtk, outside. | Sprite sheet animations in MonoGame.Extended. |
| **Zelda III** (partly read) | 8 by 8 pixel tiles, grouped into 16 by 16 and then 32 by 32 blocks to save memory. A table gives each tile a type that drives collision and behaviour (from memory). | Two background layers, with a priority bit to draw a tile over or under Link (from memory). | Not relevant. | Link is drawn from separate head and body sprites, with sword and shield over them. An animation is a list of steps, each naming cells of the sheet with their flips. Water and other tiles animate by swapping their graphics (from memory). |

What I take from them:

- **A map file must be readable.** Tiled's CSV rows and chunks make diffs and merges possible. Unity's maps buried in scene files are the counter-example.
- **A tile needs a stable address.** Tiled's local id is the tile's index in the image, so adding a column to the image shifts every tile. Godot's atlas coordinates do not have that problem.
- **Flips are three bits on the cell.** Tiled and Godot agree: horizontal, vertical, and a diagonal flip (transpose) applied first. The three give the four rotations.
- **Pad every tile in the atlas.** Godot does it by default. Without it, tiles bleed at the edges as soon as the camera moves by a fraction of a pixel.
- **Paint with stamps.** The brush is a block of cells, picked in the palette or captured from the map. Rectangle, fill, erase and pick complete it. Tiled's keys are the ones people know.
- **Per-tile data is what makes a map a game level.** A solid flag and a "kind" (water, stairs, bush) cover what RPG Maker and Zelda do with passage and tile types.
- **Old games compose a character from tiles**, with flips, and animate by changing which tiles are shown. Paper2D shows that flipbooks belong in the same plugin as tile maps.
- **Tiles are under characters or over them.** RPG Maker's star and Zelda's priority bit put a tile on one side or the other. Sorting each tile with the characters by Y is a newer idea, and it can wait.
- **Autotiling matters, but it is a second step everywhere.** Terrain sets (Tiled, Godot), rule tiles (Unity) and rule layers (LDtk) are three designs for it. It is left for later so that the first version can ship, and the data model keeps room for it.

## 3. What Stride has today

- **Sprites.** `SpriteSheetAsset` (`.sdsheet`) lists sprites, each a named region of a source image. Its compiler can pack them into atlas textures with a border around each sprite (`PackingAttributes`), and writes a `SpriteSheet` of `Sprite`s. `SpriteComponent` draws one sprite through an `ISpriteProvider`, and `SpriteRenderFeature` batches them with `Sprite3DBatch`.
- **The packing code is public**: `TexturePacker`, `AtlasTextureFactory` and `TextureHelper` in `Stride.Assets`, with a border size and a border mode per element.
- **No grid slicing.** The sprite sheet editor adds sprites one region at a time (by hand or with the magic wand). There is no "cut this image every 16 pixels".
- **No flip.** A `SpriteComponent` is mirrored only by a negative scale on its entity.
- **Animation by code only.** `SpriteAnimationSystem.Play(component, first, last, repeatMode, fps)` plays a range of sprite indices at one speed. There is no asset that names animations or gives a duration to a frame.
- **Order of drawing.** A sprite with any transparency goes to the transparent stage. That stage sorts back to front on the distance from the camera's plane to the center of each render object's bounding box (the full 32 bits of the float), then on the top 24 bits of the object's `StateSortKey` (`BackToFrontSortMode`). With the default compositor, depth is read-only in that stage (`ForwardRenderer.BindDepthAsResourceDuringTransparentRendering`), so sprites test depth and do not write it. The alpha cutoff effect clips at a fixed alpha of 0.1.
- **Quads.** `Sprite3DBatch` takes a center matrix and a size, and computes three of a quad's corners by adding vectors to the fourth. Two neighbouring quads then agree on their shared corners only when the numbers happen to be exact.
- **A plugin model** (`docs/build/plugins.md`): a runtime package and its `Assets`, `Editor` and per-toolkit view companions, all in `sources/plugins/<Name>/`. Asset editors, panels, commands with shortcuts, scene view tools, render features (`IRenderFeatureProvider`), icons, templates and an editor test kit are all open to a plugin. `Stride.SpriteStudio` and `Stride.UI` are the closest examples.
- **The sprite sheet editor** is the model for an image-based editor without a game inside: view models in `Stride.Assets.Editor`, views per toolkit, source images loaded by `SpriteEditorImageCache` and shown as toolkit bitmaps.
- **The asset graph** treats a `string` as one value everywhere, in Game Studio and in the asset compiler (`AssetNodeContainer`), and the YAML writer puts a string of several lines in a literal block.
- **What a third party can reach** (checked for D1). Public: `BatchBase<T>`, the packing classes, `SpriteSheetAsset`, `SpriteEditorImageCache`, `IRenderFeatureProvider`, and a game context without a window (`GameContextHeadless`). The bytecode of the shader `Sprite3DBatch` uses (`SpriteBatch.Bytecode`) is internal, and the bytecode of the simpler `SpriteEffect` is public. A plugin's command gets a default shortcut only with Ctrl, Alt or Meta, or on a function key (`CommandGestureBindings`).
- **Skein** (`extensions/ColorRise.Skein/` on `feature/colorrise-skein`) is the model of a third-party extension: its own solution, build files and NuGet configuration, Stride through the packages of a local engine build, an `.sdpkg` per project, tests on the editor test kit, a sample game, and a file of findings about the plugin model. Its branch changes nothing outside its folder. It draws nothing itself: a plugin with a render feature is new ground for the plugin model.

## 4. Concepts

- **Tile set**: an asset. It has one or more **sources** and gives **tiles**.
- **Source**: where tiles come from. A **grid source** is one image cut on a grid. A **sprite sheet source** is an existing sprite sheet asset, one tile per sprite. Several sources make "a combination of images" **(N)**.
- **Tile**: one picture of a tile set, with its data: collision, kind, custom properties, animation.
- **Tile id**: the stable address of a tile inside its tile set: the source's id and a local number.
- **Tile map**: an asset. It lists the tile sets it uses and has **layers**.
- **Layer**: a named grid of **cells**, drawn in order, bottom first. A cell is empty or shows one tile, with flips.
- **Chunk**: a block of 16 by 16 cells. Layers store chunks, so a map has no fixed size and empty areas cost nothing.
- **Stamp**: a block of cells used as a brush.
- **Flipbook**: an asset. It holds named **animations** for one character or object. An animation has one **clip** per direction. A clip is a list of **frames**. A frame has a duration and one or more tiles.

Coordinates **(C)**: a cell is `(x, y)` with X to the right and **Y up**, as the world. Cell `(0, 0)` has its bottom-left corner at the origin of the map's entity, and the map lies in the entity's XY plane, facing +Z, as sprites do. Tiled, Godot and LDtk count Y down, and Unity, the other engine with a Y-up world, counts it up. With Y up, moving up in the world is moving up in cells, and game code needs no sign flip. The file still writes the top row first, so the text looks like the map.

Scale **(C)**: a tile set has a `PixelsPerUnit` (16 by default, the tile size), and a map has a `CellSize` in world units (1 by 1 by default). So one 16-pixel tile is one world unit, which suits physics and cameras. Stride's sprites use 100 pixels per unit, which would make a tile 0.16 units wide. Every tile of a tile set is drawn at its size in pixels divided by the tile set's `PixelsPerUnit`, whatever its source.

## 5. The four choices

Nicolas answered them on 2026-11-24. The options he did not take stay here for the record.

### D1. Where the plugin lives **(D: B)**

- A. A Stride plugin, in `sources/plugins/TileMaps/`, built with the engine and tested on the engine's test base.
- **B. A third-party extension**, in `extensions/ColorRise.<Name>/`, like Skein: Stride through packages only, its own solution, and every gap becomes a written finding instead of a fix.

Nicolas: "i want to use it again as way to prove the plugin model". What follows from it is in sections 7, 10, 11 and 14: Skein's layout and build, tests that do not use the engine's test base, and workarounds in place of engine fixes.

### D2. Where you paint **(D: A)**

- **A. The map is its own asset, with a dedicated editor tab**: canvas, layers, palette. A component shows the map in a scene. This is Tiled, LDtk and Paper2D. The canvas is drawn by the editor's toolkit from the source images, like the sprite sheet editor, so it needs no game and can be tested without a GPU.
- B. Paint in the scene editor, on a component, as Unity and Godot do. It needs a scene view tool that writes cell data to an asset from the game thread, a palette panel, and a live preview inside the editor game.
- C. A dedicated editor around a game viewport, like the UI editor.

B can come later on the same data: the palette, the stamps and the tools are view models that a scene view tool can reuse.

### D3. Scope of the first version **(D: none of the three)**

Terrain autotiling, Tiled import and physics are all wanted, and none is in the first version. Nicolas gave the order of what follows it, which is the order of section 15.

### D4. What "animations like Zelda III" means **(D: flipbooks)**

Named animations made of tiles with a duration per frame, one clip per direction, mirrored frames, played by a component. Nicolas: "yes that's what I meant". The data lets a frame hold several tiles (head, body, sword), and the first editor edits frames of one tile.

## 6. Goals, non-goals, anti-goals

**Goals of the first version**

- Make a tile set in Game Studio from one or several images cut on a grid, and from existing sprite sheets **(N)**.
- Give tiles a solid flag, a kind, custom properties and an animation.
- Paint a map with several layers from one or several tile sets **(N)**: stamp, rectangle, fill, erase, pick, select, flips and rotations, undo and redo.
- Show a map in a scene and in a game, with a sure order between layers, flipbooks and sprites, and no bleeding or cracks between tiles.
- Read and change cells from game code, and ask whether a place is solid.
- Make flipbooks from a tile set **(N)**, with clips per direction and mirrored frames, and play them with a component.
- A sample game that uses all of it.
- Map and tile set files that are text, stable, and readable in a diff.
- A record of what the plugin model gives and lacks for a plugin that draws **(D1)**.

**Non-goals of the first version** (candidates for later, section 15)

- Terrain autotiling, rule tiles, RPG Maker autotile sheets.
- Import of Tiled or LDtk files.
- Physics colliders made from tiles, navigation meshes, pathfinding.
- Object layers, image layers, parallax.
- Isometric, staggered and hexagonal maps.
- Painting in the scene editor.
- Editing frames made of several tiles in the flipbook editor (the data and the runtime support them).
- Y order between flipbooks and `SpriteComponent`s at the same depth, and tiles sorted by Y with characters.
- Layers that write depth (for scenes that mix tile maps with 3D objects in front of them).
- Views in the WPF Game Studio.
- Real thumbnails and previews of maps in the asset view (static icons first).

**Anti-goals** (a failure even if it works)

- A map format that is a binary blob, or that changes everywhere when one cell changes.
- Maps that break when a tile set's image grows or a sprite sheet is reordered.
- Seams, bleeding or flicker between tiles at any camera position or zoom.
- A second sprite system. The plugin draws tiles and flipbooks. It does not replace `SpriteComponent`, and both must work in one scene, ordered by depth.
- Breaking the WPF Game Studio: it must open a project that uses the plugin, without the editors.
- A change to the engine, or a use of the engine's internals in the shipped packages (by reflection or any other way). It would prove nothing about the plugin model.

## 7. Packages, assets and data

### Packages **(C)**

The extension is `ColorRise.Tessera`, in `extensions/ColorRise.Tessera/`, with the layout of Skein:

| Path | Kind | Holds |
|---|---|---|
| `src/ColorRise.Tessera` | runtime | `TileSet`, `TileMap`, `Flipbook`, the components, processors, the batch and the render feature. A game references this one |
| `src/ColorRise.Tessera.Assets` | `Assets` companion | the three asset classes, compilers, templates, icons |
| `src/ColorRise.Tessera.Editor` | `Editor` companion, toolkit-neutral | plugin class, view models of the three editors, tools, entity factories, render feature provider |
| `src/ColorRise.Tessera.Editor.Avalonia` | `Editor` companion, Avalonia | views, the canvas and palette controls |
| `tests/ColorRise.Tessera.Tests` | tests | runtime and asset tests, graphics tests |
| `tests/ColorRise.Tessera.Editor.Tests` | tests | view model tests with the editor test kit |
| `tests/ColorRise.Tessera.Editor.Avalonia.Tests` | tests | headless view tests |
| `sample/` | game | the sample of section 13 |
| `docs/` | | `design.md` (this document), `plan.md`, `plugin-findings.md`, and a guide |

What is taken from Skein:

- Its own `ColorRise.Tessera.slnx`, `Directory.Build.props`, `Directory.Build.targets`, `nuget.config` and `global.json`. The folder uses nothing of the repository around it and can move to a repository of its own.
- Stride comes as packages at one version (`TesseraStrideVersion`, `4.5.4-dev` today), from the output of a local engine build (`bin/packages` of the checkout). No project references an engine project.
- The runtime declares the `Assets` and `Editor` projects as its companions (`StrideCompanionProject`), and the `Editor` project declares the Avalonia one. Each project has its own `.sdpkg` and its `StridePackageKind`.
- The packages are MIT, at version `0.1.0-dev`, with a README, a LICENSE and a `THIRD-PARTY-NOTICES.md`.
- The workarounds Skein already found are in from the start: Vulkan as the graphics API off Windows, the asset compiler taken from the engine's build configuration, the `.usrdoc` files in the packages, test classes on the editor test kit not run in parallel, and view tests that are programs, with the asset compiler's build skipped for them (Skein's findings 1, 5, 12, 13 and 22).

The namespace is `ColorRise.Tessera`, and the types keep plain names (`TileSet`, `TileMapComponent`, `Flipbook`). The asset files keep Stride's `.sd` prefix, as the plugin template's assets and Skein's `.sdyarn` do. The runtime declares its asset file extensions (`[assembly: AssetFileExtension(".sdtilemap", typeof(TileMap))]` and the two others).

Game Studio and the asset compiler load the extension because a game references its runtime. The Plugins panel lists it under the plugins of the project, where it can be switched off.

Code rules of the project apply: explicit access modifiers, sealed classes by default, the `field` keyword instead of new backing fields, plain English in comments and docs. The source files have no licence header, as in Skein.

### Tile set asset (`.sdtileset`, compiles to `TileSet`)

```csharp
public sealed class TileSetAsset : Asset
{
    public Int2 TileSize { get; set; } = new(16, 16);     // default size of a tile, in pixels
    public float PixelsPerUnit { get; set; } = 16;
    // Texture options, the same as SpriteSheetAsset: color key, alpha, premultiply, compression (off by default), sRGB.
    // They apply to every source of the tile set.
    public List<TileSetSource> Sources { get; }
    public List<TileDefinition> Tiles { get; }             // only the tiles that have data
}

public abstract class TileSetSource { public int Id; public string Name; }    // Id: 1 to 255, never reused

public sealed class GridTileSetSource : TileSetSource
{
    public UFile Image;
    public Int2 TileSize;      // (0, 0) takes the tile set's
    public Int2 Margin;        // around the grid, in pixels
    public Int2 Spacing;       // between tiles, in pixels
}

public sealed class SpriteSheetTileSetSource : TileSetSource
{
    public SpriteSheet Sheet;                         // a reference to a sprite sheet asset
    public List<SpriteTileBinding> Bindings { get; }  // local number -> sprite name
}
```

**Tile id.** A `TileId` is 32 bits: the source's id (8 bits) and a local number (24 bits).

- In a grid source, the local number is the row and the column in the image (12 bits each, row 0 at the top). A larger image adds tiles and moves none.
- In a sprite sheet source, the local number is given by the tile set the first time it sees a sprite, and bound to the sprite's **name**. Reordering the sheet changes nothing. A renamed or deleted sprite leaves a "missing" tile that the editor shows and can rebind. When two sprites of a sheet have the same name, the first one is taken and the build warns.
- Written as text: `1:3,4` (source 1, column 3, row 4) and `2:#17`.

**Which tiles exist.** In a grid source, every grid position that lies fully inside the image and is not fully transparent (after the color key) is a tile. Empty positions are not tiles: they are not in the palette and not in the atlas. The editor and the build command decide this with the same function, from the pixels. A grid source may have its own tile size, so a 16 by 24 character sheet and a 16 by 16 terrain sheet can share a tile set.

**A sprite sheet source is a list of named regions (C).** The tile set reads the sheet *asset*: each sprite's source image and region. It does not use the compiled sheet. So the sheet's own packing (which may be off, or may rotate sprites), its pixels per unit, its sprite centers and borders play no part: a tile from a sheet is packed, sized, placed and flipped exactly like a tile from a grid. This is what keeps the "no bleeding" rule true for any sheet.

**Tile data** (`TileDefinition`, keyed by `TileId`, present only when not default):

- `Collision`: `None` or `Solid`. A flags enum, so that later versions can add one-way sides.
- `Kind`: a free string such as `water`, `stairs`, `bush`. The game decides what it means. This is Tiled's class and Zelda's tile type.
- `Properties`: named values of type bool, int, float, string or color.
- `Animation`: a list of frames, each a `TileId` of the same tile set and a duration in seconds. Empty for a still tile.

**Compiling (C).** One build command of the plugin makes the atlas and the `TileSet`. It loads the source images (its input files, so the build cache knows when to run it again), lists the tiles, packs them with `TexturePacker` and `AtlasTextureFactory` (a border of 2 pixels around each tile that repeats the tile's edge, no rotation, several textures when one is not enough), and saves the textures with `TextureHelper`, as `SpriteSheetAssetCompiler.SpriteSheetCommand` does. The compiler declares the sprite sheets as asset dependencies and reads them in `Prepare` to give their regions to the command.

The runtime `TileSet` holds `PixelsPerUnit`, `TileSize`, and a table `TileId -> Tile`, where a `Tile` has its texture, its region in the atlas and its data.

### Tile map asset (`.sdtilemap`, compiles to `TileMap`)

```csharp
public sealed class TileMapAsset : Asset
{
    public Vector2 CellSize { get; set; } = Vector2.One;   // world units
    public Int2 Size { get; set; }                         // in cells, (0, 0) for a map without bounds
    public List<TileSetSlot> TileSets { get; }             // Slot (1 to 65535, never reused) and the TileSet
    public List<TileLayerAsset> Layers { get; }            // bottom first
}

public sealed class TileLayerAsset
{
    public string Name; public bool Visible = true; public float Opacity = 1; public Color Tint = Color.White;
    public Vector2 Offset;                                 // world units
    public bool Collision = true;                          // whether its solid tiles count in collision queries
    public List<TileChunkAsset> Chunks { get; }            // hidden from the property grid
}

public sealed class TileChunkAsset
{
    public Int2 Position;      // in chunks
    public string Cells;       // 16 lines of 16 cells, top row first
}
```

**A cell** at runtime is 8 bytes **(C)**: the slot of the tile set (16 bits), the `TileId` (32 bits), three flip bits (horizontal, vertical, diagonal) and spare bits. Ids are the stable ones, in the file and in memory. So a tile set can change without recompiling its maps, and game code addresses any tile of the map's tile sets. A map of 500 by 500 cells with four layers takes 8 MB.

**Flips** follow Tiled: the diagonal flip (a swap of X and Y) is applied first, then the horizontal, then the vertical. The editor's "rotate" sets the three bits. The diagonal flip is offered only for square tiles.

**Chunks.** A layer stores chunks of 16 by 16 cells, by chunk position (the cell position divided by 16, rounded down, so negative cells work). A chunk with no tile is removed. `Size` is a frame, not storage: when set, the editor draws it, the tools do not paint outside, and the game can clamp its camera to it.

**The file (C).** The asset is Stride YAML. The cells of a chunk are **one string**, written as a literal block: one line per row of the map, top row first, one token per cell. The chunks of a layer are kept in a fixed order (top to bottom, then left to right). The same map always gives the same bytes, and a stroke changes only the rows it touched. The exact token of a cell is left to the builder (section 16).

**Editing through the asset graph.** Because a chunk's cells are a string, Game Studio's node graph sees one value per chunk, in the session and in the asset compiler alike, with no special registration. A stroke replaces the strings of the chunks it touched through their nodes (and adds or removes chunk items), in one transaction. Undo, redo, the dirty flag and the reload of the scene view then work as for any property. The editor keeps the decoded cells beside the asset for drawing. This is risk R1 (section 11).

**Compiling** decodes the chunks into a `TileMap` and declares the tile sets as runtime dependencies. A cell whose tile set slot or tile no longer exists is kept as it is, and drawn as nothing in the game.

### Flipbook asset (`.sdflipbook`, compiles to `Flipbook`)

```csharp
public sealed class FlipbookAsset : Asset
{
    public TileSet TileSet { get; set; }
    public Vector2 Pivot { get; set; } = new(0.5f, 0);     // where the entity is in a frame: bottom center by default
    public List<FlipbookAnimation> Animations { get; }
}

public sealed class FlipbookAnimation
{
    public string Name;                      // "walk"
    public FlipbookLoopMode LoopMode;        // Loop, Once, PingPong
    public List<FlipbookClip> Clips { get; } // one per direction
}

public sealed class FlipbookClip
{
    public FlipbookDirection Direction;      // None, Down, Left, Right, Up, and the four diagonals
    public List<FlipbookFrame> Frames { get; }
}

public sealed class FlipbookFrame
{
    public float Duration = 0.1f;            // seconds
    public List<FlipbookCell> Cells { get; } // TileId, offset in pixels, flips. One cell in the first editor
}
```

One flipbook is one character or object: all its animations in one asset, as Godot's `SpriteFrames`, because a character with four directions and six actions would otherwise be 24 assets. A frame is a list of cells so that a character can be composed of parts later (D4). The pivot is a point of the frame's first cell, in fractions of its size. A clip with direction `None` is used when the animation has no clip for the asked direction, and the first clip when there is none either.

## 8. Runtime

### Components

```csharp
public sealed class TileMapComponent : ActivableEntityComponent
{
    public TileMap Map { get; set; }
    public Color4 Color { get; set; } = Color4.White;
    public float LayerSpacing { get; set; } = 0.1f;       // depth between two layers, world units
    public TileSampler Sampler { get; set; }               // Point (default) or Linear
    public float AnimationSpeed { get; set; } = 1;
    public RenderGroup RenderGroup { get; set; }
}

public sealed class FlipbookComponent : ActivableEntityComponent
{
    public Flipbook Flipbook { get; set; }
    public string Animation { get; set; }                  // the one shown at start
    public FlipbookDirection Direction { get; set; }
    public bool PlayOnStart { get; set; } = true;
    public float Speed { get; set; } = 1;
    public bool FlipX { get; set; }
    public Color4 Color { get; set; } = Color4.White;
    public TileSampler Sampler { get; set; }
    public bool SortByY { get; set; } = true;
    public RenderGroup RenderGroup { get; set; }

    public void Play(string animation, bool restart = false);
    public void Queue(string animation);
    public void Pause(); public void Resume(); public void Stop();
    public event EventHandler<FlipbookEventArgs> FrameChanged, AnimationEnded;
}
```

Changing `Direction` while an animation plays keeps the time and switches the clip, so a walking character turns without restarting its step.

### Drawing

One render feature, `TileRenderFeature`, draws tile layers and flipbooks, alpha blended, in the transparent stage. A render processor per component type makes the render objects:

- one per chunk of each visible layer, with the chunk's bounding box, so the visibility system culls chunks out of view;
- one per flipbook.

**The batch (C).** The plugin has its own batch, `TileBatch`, derived from the engine's `BatchBase<T>` as `Sprite3DBatch` is. A quad is given by its four corners, and a corner of the map's grid is always computed by the same expression from its whole-number grid position. Two neighbouring tiles then give bit-identical positions for the corners they share, and the rasterizer leaves no gap between them, at any position, zoom or rotation of the map. `Sprite3DBatch` cannot promise this (section 3).

**The shader (C).** The batch uses the engine's public `SpriteEffect.Bytecode`: a textured quad multiplied by one colour, which is a parameter of the draw call and not a value per vertex. So the batch is flushed when the colour changes. A layer has one colour (its tint and opacity, times the component's colour), and flipbooks of the same colour are drawn together. The shader `Sprite3DBatch` uses has a colour per vertex, but its bytecode is internal to the engine, and a shader loaded by name works only in a game whose build compiled it. The public bytecode is embedded in the engine for each graphics API, so it asks nothing of the game. This is risk R3.

**Order (C).** Layer `i` is drawn at local Z `i * LayerSpacing`, and the stage's back-to-front sort puts lower layers first. A character goes between two layers by its Z: `TileMapComponent.GetLayerZ(name)` gives a layer's depth. A `SpriteComponent` of the engine is ordered the same way, by its Z, since it is drawn in the same stage. Opacity and tint of a layer need nothing special, since everything is blended.

**Y sort for flipbooks.** Flipbooks at the same depth are ordered by their world Y: the one higher on the screen is drawn first. The render processor writes this into the render object's `StateSortKey`, which the stage uses when two objects have the same distance. So characters sort among themselves when they share a Z and the camera looks straight at the map. With a tilted camera, the distance already orders them. Tiles do not sort with characters by Y: a tile is under them or, on an upper layer, over them, as in the old games. This is risk R2.

**Animated tiles.** The map's processor advances a clock (game time by `AnimationSpeed`). When drawing, an animated tile shows the frame its clock gives. Every instance of a tile is in step.

**Tiles larger than a cell** are anchored at the cell's bottom-left corner and extend up and to the right, as in Tiled.

**Compositor.** A game's graphics compositor needs the feature. A compositor created in Game Studio gets the features of the project's packages, and an existing one gets them with *Add package features* (`docs/build/plugins.md`). The editor's own compositors get it from `IRenderFeatureProvider`. For pixel art, the README says to turn off tone mapping and the other post effects, which change the colours of everything drawn before them. Skein met the same thing with UI (its finding 20): the transparent stage is drawn before the post effects, colours blend in linear space, and a library cannot change the compositor of the game that uses it. The sample's compositor has no post effects.

### API for game code

```csharp
var map = entity.Get<TileMapComponent>();
Int2 cell = map.WorldToCell(player.Transform.WorldMatrix.TranslationVector);
Vector3 center = map.CellToWorld(cell, TileAnchor.Center);

TileLayer ground = map.Map.Layers["Ground"];
TileCell c = ground.GetCell(cell);
ground.SetCell(cell, tileSet, TileId.Parse("1:3,4"), TileFlip.None);
ground.ClearCell(cell);

Tile tile = map.Map.GetTile(c);            // null for an empty cell
if (tile?.Kind == "bush") { ... }

bool blocked = map.IsSolid(cell);                    // a solid tile on any layer with Collision
bool hit = map.OverlapsSolid(new RectangleF(...));   // a world rectangle in the map's plane
```

- A loaded `TileMap` is shared content, like a model: a change is seen by every component that shows it. `TileMap.Clone()` gives a private copy.
- A change of a cell raises `TileLayer.Changed` with the chunk, which the renderer and the game can listen to.
- A `TileMap` can be reloaded in place by the content manager (`Content.Reload`), which is how Game Studio's scene view follows an edited asset. The renderer notices.
- Maps and tile sets can also be made in code (`new TileMap()`, `TileSet.FromTexture(texture, tileSize)`), for generated levels. The first version keeps this simple and tested, without more.

## 9. Game Studio (Avalonia)

View models are in `ColorRise.Tessera.Editor` and know no toolkit. Views are in `ColorRise.Tessera.Editor.Avalonia`. The three editors are `AssetEditorViewModel`s, registered with `[AssetEditorViewModel<T>]` and `[AssetEditorView<T>]`, without a game inside: the views draw tiles from the source images, loaded as the sprite sheet editor loads them (`SpriteEditorImageCache`), with the tile set's color key applied and nearest-neighbour scaling.

Two controls are shared by the three editors:

- **Tile canvas**: a custom-drawn control with zoom around the cursor, pan (middle button or space), a grid, and overlays. It draws only what is visible and keeps a bitmap per chunk when zoomed out (risk R4).
- **Tile palette**: the sources of a tile set. A grid source is drawn as its image with the grid: a click picks a tile, a drag picks a block of tiles as a stamp. A sprite sheet source is drawn as a flow of its sprites, each in its own box: a click picks one tile, and there is no block.

### Tile set editor

```
+--------------------------------------------------------------------------+
| [+ Image] [+ Sprite sheet]    Mode: (Select) (Solid) (Animation)   400%  |
+------------------+-------------------------------------------------------+
| Sources          |                                                       |
|  1 overworld.png |   the selected source, with its tile grid             |
|  2 dungeon.png   |   click or drag: select tiles                         |
|  3 Objects sheet |   overlays: solid tiles hatched, animated tiles       |
|                  |   marked, missing sprites in red                      |
+------------------+-------------------------------------------------------+
```

- The property grid shows the selected source (image, tile size, margin, spacing), or the data of the selected tiles (several at once).
- **Solid mode**: a click or a drag toggles the solid flag, as in RPG Maker's passage settings.
- **Animation mode**: select a tile, then click the tiles that are its frames, in order. Durations are edited in the property grid. The tile plays in place.
- Adding an image proposes the tile set's tile size and shows the grid before confirming. Dropping an image file or a sprite sheet asset on the editor adds a source.
- Opening the editor binds the new sprites of its sheets and marks the missing ones.

### Tile map editor

```
+-----------------------------------------------------------------------------+
| Stamp  Rectangle  Fill  Erase  Pick  Select |  Flip X  Flip Y  Rotate | Grid |
+-------------------------------------------------+---------------------------+
|                                                 | Layers                    |
|                                                 |  [eye] [lock] Overhead    |
|                map canvas                       |  [eye]        Details     |
|                                                 |  [eye]        Ground   <  |
|   the stamp follows the cursor                  |  [+] [-] [up] [down]      |
|   the other layers can be dimmed                +---------------------------+
|                                                 | Tile set: [Overworld  v]  |
|                                                 |  palette                  |
|                                                 |  drag: a stamp of several |
|                                                 |  tiles                    |
+-------------------------------------------------+---------------------------+
| cell 12, 7   Ground   Overworld 1:3,4   300%                                |
+-----------------------------------------------------------------------------+
```

- **Tools**, with Tiled's keys where it has one **(C)**: Stamp (B), Rectangle (P), Fill (F), Erase (E), Pick (I, Unity's key), Select (R). X and Y flip the stamp, Z and Shift+Z rotate it.
  - Stamp: left button paints, a drag paints along the way, Shift+click draws a line from the last cell. The right button, dragged over the map, captures a stamp from the current layer.
  - Rectangle: fills the dragged rectangle by repeating the stamp.
  - Fill: fills the connected cells that have the same tile as the clicked one, inside the map's frame or, without one, inside the used bounds of the layer.
  - Erase: left button erases, a right drag erases a rectangle.
  - Pick: takes the tile under the cursor as the stamp and selects it in the palette.
  - Select: a rectangle of cells, to move, copy, cut, paste or delete.
- **One stroke is one undo step**, named after the tool.
- **Layers**: add, remove, rename, reorder, show, lock, dim the others. The property grid shows the selected layer, or the map when none is selected.
- **Tile sets**: adding one to the map gives it a slot. Removing one that is still used asks first and says how many cells use it. A cell whose tile set or tile is missing is drawn as a marked box, and is kept in the file.
- **Keys (C).** The tools and the stamp flips are commands of the editor (`context.Commands` with a `GestureScope`), so they are in the command palette and in the Keyboard shortcuts page, where the user can give them a shortcut. They have no default shortcut there, because the registry refuses a plugin's shortcut without Ctrl, Alt or Meta. The single keys (B, P, F, E, I, R, X, Y, Z) are read by the map canvas while it has the focus, and cannot be rebound in the first version. This goes to the findings (risk R6).
- Lock and dimming are the editor's state, kept in the editor's settings and not in the asset.

### Flipbook editor

```
+------------------+----------------------------------------------------------+
| Animations       |  preview            [play] [pause]  speed 100%  [loop]  |
|  idle            |                                                          |
|  walk            +----------------------------------------------------------+
|    down          |  frames: [ 1:0,0 0.12 s ] [ 1:1,0 0.12 s ] [ 1:2,0 ... ] |
|    left          |  drag to reorder, drop tiles from the palette to add     |
|    right         +----------------------------------------------------------+
|    up            |  palette of the flipbook's tile set                      |
|  attack          |                                                          |
| [+] [-] [Sheet]  |                                                          |
+------------------+----------------------------------------------------------+
```

- **Sheet** is a helper for character sheets: pick a block of tiles, say which row is which direction and how the frames play (in order, or 1, 2, 3, 2), and it makes the clips. "Mirror" makes the left clip from the right one with flipped frames.
- The property grid shows the selected animation, clip or frame.

### In the scene editor

- `Create entity > 2D > Tile map` and `Flipbook` (entity factories), and dropping a tile map or flipbook asset in the scene makes the entity (add-asset policies, as for sprite sheets).
- The scene view shows maps and flipbooks with the real renderer. After a change in the map editor, the scene view follows through the editor's normal reload of a changed asset (risk R5).
- Icons for the two components and the three assets (`[assembly: TypeImage]`), asset templates in *Add asset*, and static thumbnails.
- The cross-platform Game Studio cannot add a component to an existing entity yet (Skein's finding 17). The entity factories and the drop of an asset are the two ways to get a map or a flipbook into a scene.

### WPF Game Studio

The neutral `Editor` package should load there, with the assets in the property grid and maps drawn in scenes, and the three editors, which have no view, should not open. None of this can be checked from the Linux container the work is done in, as for Skein. The README lists it as not checked until someone opens a project on Windows.

## 10. Plugin points exercised

This is the point of D1: what the plugin model is asked to do by a plugin that draws, which Skein did not. A gap goes to `docs/plugin-findings.md`, in the format of Skein's file: what was tried, what happened, the workaround, and what would close the gap in Stride. Nothing in the engine is changed from this work **(C)**. A fix of the engine is a separate piece of work, which Nicolas decides on. Where Tessera meets a point Skein already wrote about, its file says only whether the finding still holds.

| Point | Used for | New compared with Skein |
|---|---|---|
| Runtime package with `Assets`, `Editor` and `Editor.Avalonia` companions, built from packages | the layout | no |
| Asset classes, compilers with asset and runtime dependencies | tile set, tile map, flipbook | no |
| A build command that reads images and packs an atlas with the engine's public packing classes | tile set | yes |
| `AssetFileExtension` URL constants | the three content types | no |
| Asset templates, `TypeImage`, `StaticThumbnail` | *Add asset*, icons | no |
| `IRenderFeatureProvider`, a root render feature, a batch derived from `BatchBase<T>` on a public shader bytecode | drawing in the editor and in games | yes |
| The picking stage of the scene editor | selecting a map by a click | yes |
| Entity factories in an existing category, add-asset policies | the scene editor | partly |
| `AssetEditorViewModel` and Avalonia views that draw images, without a game | the three editors | partly |
| The session's property grid for an editor's own selection | tiles, layers, frames | no |
| Commands with a gesture scope | the tools in the palette and the shortcuts page | no |
| `[EditorSettings]` | grid colour, dimming, last zoom | yes |
| Editor test kit and headless view tests | section 14 | no |
| A game without a window in a third party's tests | graphics tests | yes |

## 11. Risks, and what is tried first

Step 1 starts with R9, since nothing can be built before it, then spikes on R1, R2, R3 and R10. A fallback that loses a goal comes back to Nicolas, and no fallback changes the engine.

| # | Risk | Tried first | Fallback |
|---|---|---|---|
| R1 | Chunks as strings in the asset graph: a list of a thousand chunk items, replaced through nodes | A test asset with 1000 chunks: load, save, same bytes; replace a chunk's string through its node; add and remove chunks; undo and redo; open the asset in Game Studio without a slowdown and without the chunks in the property grid | Keep the chunks out of the graph (a member the graph ignores) and write the undo steps by hand, with a custom dirtying operation that still tells the session the asset changed |
| R2 | Order in the transparent stage: layers by depth, flipbooks between layers, flipbooks by Y through `StateSortKey`, a `SpriteComponent` between two layers | A graphics test with three layers, one of them half transparent, a `SpriteComponent`, and three flipbooks at one depth, under an orthographic camera looking straight at the map, then tilted | A render stage of the extension's own, which a game adds to its compositor after the transparent stage (a library cannot change the compositor of the game that uses it), with the steps in the README |
| R3 | A batch of the plugin with exact shared corners, on a shader a third party can reach. `BatchBase<T>` is public and made to be derived from, but the bytecode `Sprite3DBatch` gives it (`SpriteBatch.Bytecode`) is internal | Derive `TileBatch` with the public `SpriteEffect.Bytecode` and a vertex layout of its own. A graphics test scrolls and zooms a map by fractions of a pixel over a bright background and counts background pixels inside the map. The same map is drawn by a game outside the checkout, from the packed packages | A small shader of the extension's own, compiled by the game's build, with what a game must do for it written in the README and in a finding. Last resort: `Sprite3DBatch`, exact only for maps at whole positions, with the limit written in the README |
| R4 | The canvas on a large map (256 by 256 cells, four layers) zoomed out | A headless test that draws it and measures a pan | A coarser picture when zoomed out, drawn once per chunk |
| R5 | The scene view following a map being edited: the editor rebuilds the asset and reloads the content in place | Change a map asset while a scene that shows it is open | A "refresh" note in the first version, then the in-scene work of section 15 |
| R6 | Single-key shortcuts (B, F, E): the registry refuses a plugin's default shortcut without Ctrl, Alt or Meta. This is known from the source, so the design already reads the keys in the canvas (section 9) | That the canvas gets a key before the main window or a text box does, and not while the user types a layer's name | Shortcuts with a modifier (Alt+B), as commands |
| R7 | Clicking a map or a flipbook in the scene view to select its entity: `SpriteRenderFeature` has special code for the picking stage | Draw the picking stage as the sprite feature does | Selection through the entity tree only, written as a finding |
| R8 | Bleeding between tiles with point and linear sampling, and with MSAA | The test of R3, with a zoom that is not a whole number, on both samplers | A larger border in the atlas, or texture coordinates inset by a fraction of a texel |
| R9 | Building from packages only. The engine's packages come from a local engine build, which is long in the container, and for Skein the asset compiler's own restore of companion packages failed in this sandbox (its finding 21) | Build the engine and the editor once, as Skein's README says, then the extension's empty projects, a first test and a `dotnet pack` | Skein's workarounds. If the engine cannot be built in the container in a reasonable time, this comes back to Nicolas |
| R10 | Graphics tests without the engine's test base: a game without a window on a software Vulkan driver, from a third party's test project | A test that draws one coloured tile and reads its pixel back, with `GameContextHeadless` and the `Stride.Dependencies.Lavapipe` package from nuget.org, which the engine's own tests use | The software driver installed in the container (under Xvfb, as the project's memory describes), with the graphics tests skipped on a machine that has none |

## 12. Steps and branches **(C)**

Each step is a branch from the previous one, the first from `master`. Every commit stays inside `extensions/ColorRise.Tessera/`. Conventional commit titles with the scope `tessera` (`feat(tessera): ...`), as Skein's have `skein`.

1. **`feature/colorrise-tessera`**: the scaffold of the extension as Skein's, with this document in `docs/`, then the spikes, the tile set asset and build command, tile map data, asset and compiler, `TileMapComponent`, the batch and the render feature, the API and the tests. At the end, a map written in a test is drawn by a game without a window, and the packages pack.
2. **`feature/colorrise-tessera-editor`**: the canvas and palette controls, the tile set editor and the tile map editor, templates, icons, entity factory, add-asset policy. At the end, a map painted in Game Studio shows in a scene and in a game.
3. **`feature/colorrise-tessera-flipbooks`**: animated tiles in the tile set editor, the flipbook asset, compiler, component, drawing and editor.
4. **`feature/colorrise-tessera-sample`**: the sample game, the README and the guide.

Each step ends with its tests green in the container, the findings file up to date, and screenshots in `/mnt/project-files/tilemap/`. No CI builds an extension today, and Skein's branch added none, so the test output goes in each step's report. The engine build that makes the packages is a long run. It has to be done in the container, because no check on GitHub packs the engine for an extension.

## 13. The sample **(C, for approval)**

A small top-down game in `extensions/ColorRise.Tessera/sample/` (its name is left to the builder). It is laid out as Skein's Brinemoor is: a game library, Linux and Windows heads, a test project, and a solution of its own that also holds the extension's projects, so that Game Studio and the asset compiler find the companions. The game references Stride as packages and the extension's runtime project, never an engine project, and it must open in Game Studio.

- one overworld map of about 60 by 40 cells with three layers (ground, details, overhead) and animated water;
- one house interior, a second map, entered through a door: the game loads it and moves the player;
- a character with idle and walk flipbooks in four directions, moved on the grid, blocked by solid tiles, hidden under the overhead layer;
- bushes that the player cuts: the game reads the tile's kind and replaces the cell;
- two or three wandering characters, Y-sorted with the player;
- an orthographic camera that follows the player and stops at the map's frame, at a whole-number zoom;
- tests that need no running game, as Brinemoor's: every cell's tile exists, the door leads to the second map, and the player is not walled in at the start.

**Art.** My pick is Kenney's "Tiny Town" and "Tiny Dungeon" packs (16 by 16 tiles, CC0), listed in the extension's `THIRD-PARTY-NOTICES.md`. They look right and cost nothing in licence terms. The other way is art generated by a script, as Orb Hunt's textures were, which is free of any third party and looks poorer. Nicolas agreed to Kenney's art on 2026-11-24. The packs' licence must still be read again from the download before they are committed.

## 14. Testing and checks

- **Runtime unit tests**, no GPU: tile ids and their text, cells and flips, chunks with negative coordinates, world and cell conversions, the animation clock, flipbook playback (loops, direction changes, queue, events), solid queries, binary round trip of the three content types, reload in place.
- **Asset tests**: the YAML of a map is stable (a golden file), a stroke changes only its rows, the tile set command makes the expected regions with their borders and skips empty tiles, a second build does nothing, a missing sprite gives a warning and not a failure.
- **Graphics tests**, without gold images **(C)**: a game without a window (`GameContextHeadless`) on a software Vulkan driver draws maps whose tiles are flat colours made by the test, and the test reads pixels back at known places. Covered: layers and order, the eight flip combinations (on a tile with four coloured quarters), an animated tile at fixed times, opacity and tint within a tolerance, flipbooks with Y sort, the scroll and zoom of R3. The engine's regression tests are not the model here: their gold images are made by the engine's own CI, which a third party does not have. The pictures the tests draw are saved beside the test results, for a human to look at.
- **Editor view model tests** with the editor test kit (`StrideEditorTestSession`, in the package `Stride.Assets.Editor.Testing`): each tool on a map, undo and redo, stamps and their flips, tile set operations (add a source, bind sprites, toggle solid), the sheet helper of the flipbook editor.
- **Headless view tests** (Avalonia.Headless), in their own project and written as Skein's are (its finding 12 says how a kit session and the UI thread work together): the canvas draws the expected pixels for a small map, a drag in the palette makes a stamp, the keys reach the tools.
- **From the packages**: after `dotnet pack`, a small game in a folder outside the checkout, with only the package reference, restores, builds its tile set and its map, and draws them. Skein made the same check, and it is what a user of the extension does.
- **By hand, with screenshots**: the cross-platform Game Studio on Linux (Xvfb and a software driver, as the project's memory describes for Orb Hunt) opens the sample, each editor is used once, the game runs.

The tests run in the container, with `dotnet test` on the extension's solution. No CI runs them (section 12).

## 15. Later

The first four are in Nicolas's order **(D3)**. The rest is in the order I would take it.

1. **Terrain autotiling.** Terrain sets as in Tiled and Godot (corner, edge or both), a terrain brush, and an import of RPG Maker's autotile sheets. `TileDefinition` keeps room for terrain bits and a probability.
2. **Tiled support**: an import of `.tmx` and `.tsx` files and of their JSON forms, as an asset importer. The data model already holds Tiled's orthogonal maps: the same three flip bits, chunks of 16, frames with durations, class and properties, layer opacity and tint.
3. **Physics or pathfinding**, simple first, as Nicolas put it: say which tiles are walkable and which are obstacles. The first version already has half of this, the solid flag of a tile and the queries `IsSolid` and `OverlapsSolid`. The next step is a walkable grid made from the layers, with a path search on it and a movement cost per kind of tile. Static colliders for Bepu, merged into rectangles, come after it, in an optional package so that the core needs no physics.
4. **Parallax**: a scroll factor per layer, and image layers for backgrounds.
5. **The rest**:
   - Painting in the scene editor (D2 B): a scene view tool and a palette panel on the same view models, with a preview in the editor game.
   - Object layers and Y-sorted tile layers, random and scatter modes for the stamp, saved stamps.
   - Flipbook parts in the editor (head, body, sword), frame events, and a provider that shows a tile or a flipbook in a `SpriteComponent`.
   - Layers that write depth, with an alpha cutoff, for scenes that mix tile maps and 3D objects, and Y order shared with `SpriteComponent`s.
   - Speed: cached vertex buffers per chunk, or one quad per chunk with a lookup texture.
   - Real thumbnails and previews, WPF views, isometric and hexagonal maps, LDtk import.

## 16. Left to the builder

- The token of a cell in a chunk's text, within the rules of section 7, and the bit layout of a cell within 8 bytes.
- How `Tiles` is keyed in YAML (a dictionary with a scalar key, or a list of entries).
- The names of small types and members not written here, and the split of the render objects and processors.
- The default of `LayerSpacing`, and how many world units of Y one step of the sort key covers, after the spike of R2.
- Whether the canvas caches bitmaps per chunk from the start or only when zoomed out.
- The entity factories' order in the `2D` category, the icons, the templates' default names.
- The sample's name and its small story.
- How the batch groups draws by colour, texture and sampler.
- The plan itself: tasks, their order inside a step, and the tests of each.

## Sources

Read on 2026-11-24:

- Tiled, from the documentation sources on GitHub (the site is blocked here): [TMX map format](https://github.com/mapeditor/tiled/blob/master/docs/reference/tmx-map-format.rst), [Global tile ids](https://github.com/mapeditor/tiled/blob/master/docs/reference/global-tile-ids.rst), [Editing tile layers](https://github.com/mapeditor/tiled/blob/master/docs/manual/editing-tile-layers.rst), [Terrains](https://github.com/mapeditor/tiled/blob/master/docs/manual/terrain.rst), [Infinite maps](https://github.com/mapeditor/tiled/blob/master/docs/manual/using-infinite-maps.rst).
- Godot: [Using TileSets](https://docs.godotengine.org/en/stable/tutorials/2d/using_tilesets.html), [Using TileMaps](https://docs.godotengine.org/en/stable/tutorials/2d/using_tilemaps.html), [TileMapLayer](https://docs.godotengine.org/en/stable/classes/class_tilemaplayer.html), [TileSetAtlasSource](https://docs.godotengine.org/en/stable/classes/class_tilesetatlassource.html).
- Unity: [Tilemaps](https://docs.unity3d.com/Manual/tilemaps/tilemaps-landing.html) and its reference pages, [Rule Tile](https://docs.unity3d.com/Packages/com.unity.2d.tilemap.extras@4.0/manual/RuleTile.html).
- LDtk: [JSON format](https://github.com/deepnight/ldtk/blob/master/docs/JSON_DOC.md) (the site is blocked here, so the editor's own pages were not read).
- Zelda III: [SpriteAnimator](https://github.com/spannerisms/SpriteAnimator) for how Link's animations are made of steps and cells, and The Cutting Room Floor's [ROM map](https://tcrf.net/Proto:The_Legend_of_Zelda:_A_Link_to_the_Past/Mid-1991_Overdump/ROM_Map) for the 32 and 16 pixel block tables (search result only).
- RPG Maker MZ: only search results that name the A1 to A5 sheets. The help site is blocked here.

From memory, not read today: RPG Maker's passage and star settings, Unreal's Paper2D, MonoGame.Extended, Godot's `AnimatedSprite2D`, Unity's animated tiles, and the details of Zelda III's layers and tile types. Check them before relying on one.

In the fork (`master` at `659bb239e`): `docs/build/plugins.md`, `docs/editor/custom-editor.md`, `docs/editor/editors.md`, `sources/engine/Stride.Assets/Sprite/`, `sources/engine/Stride.Assets/Textures/Packing/`, `sources/engine/Stride.Rendering/Rendering/Sprites/`, `sources/engine/Stride.Rendering/Rendering/BackToFrontSortMode.cs` and `SortModeDistance.cs`, `sources/engine/Stride.Graphics/BatchBase.cs` and `Sprite3DBatch.cs`, `sources/engine/Stride.Engine/Rendering/Compositing/ForwardRenderer.cs`, `sources/assets/Stride.Core.Assets.Quantum/AssetNodeContainer.cs`, `sources/core/Stride.Core.Yaml/Emitter.cs`, `sources/editor/Stride.Assets.Editor/EditorGame/ContentLoader/EditorContentLoader.cs`, `sources/editor/Stride.Core.Assets.Editor/Commands/CommandGestureBindings.cs`, `sources/engine/Stride.Graphics/Shaders.Bytecodes/` and `Shaders/SpriteEffect.sdsl`, `sources/engine/Stride.Games/GameContextHeadless.cs`, `sources/plugins/SpriteStudio/` and `sources/plugins/UI/`.

In Skein (`feature/colorrise-skein` at `7f74e1030`, `extensions/ColorRise.Skein/`): `README.md`, `docs/design.md` for the shape of this document, `docs/plugin-findings.md`, the build files (`Directory.Build.props`, `Directory.Build.targets`, `nuget.config`), the project files of `src/` and `tests/`, and the layout of the Brinemoor sample.
