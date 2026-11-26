# Make a small game with Tessera

This guide makes a small top-down game with Tessera: a hero walks a town of three layers, cuts brambles, goes into a house and out again, among villagers who wander. The game is **Thornwick**, the sample in [`sample/Thornwick`](../sample/Thornwick/README.md), and the guide follows the order in which it was made. Each part says what to do, shows it, and names the file of the sample that holds the result.

It is written for someone who knows Stride (assets, scenes, scripts, the graphics compositor) and has never seen Tessera. The pictures were taken while Thornwick was made, in the cross-platform Game Studio on Linux; the commands are named as that Game Studio shows them.

**Before you start.** Build Stride and Tessera as [Tessera's README](../README.md#try-it) says, then the cross-platform Game Studio from the same checkout. A game uses Tessera through its runtime, `ColorRise.Tessera`: a package reference, or, as in Thornwick, a project reference from the game library (`Thornwick.Game/Thornwick.Game.csproj`). Game Studio then loads Tessera's editors with the game, and *View > Plugins* lists them. Thornwick's solution, `Thornwick.slnx`, holds Tessera's projects beside the game's, so that Game Studio builds them together.

**The game's projects.** No part of this guide makes them: Thornwick's were written by hand, in the shape of every Stride game, and a game that follows the guide needs the same. Copy them from `sample/Thornwick` and rename them:

- the solution, `Thornwick.slnx`, with Tessera's projects in its folder `Tessera`;
- the game library, `Thornwick.Game/Thornwick.Game.csproj` and its package file `Thornwick.Game.sdpkg`. The library references Stride's packages and Tessera's runtime:

  ```xml
  <PackageReference Include="Stride.Engine" Version="$(TesseraStrideVersion)" />
  <PackageReference Include="Stride.AssetCompiler" Version="$(TesseraStrideVersion)" IncludeAssets="build;buildTransitive" />
  <ProjectReference Include="..\..\..\src\ColorRise.Tessera\ColorRise.Tessera.csproj" />
  ```

  The path and `TesseraStrideVersion` (from Tessera's `Directory.Build.props`) hold for a game in a folder beside `sample/Thornwick`. A game elsewhere references the package `ColorRise.Tessera` instead, from the folder `dotnet pack` fills ([Tessera's README](../README.md#try-it), point 3), with the engine's `bin/packages` as a second package source: `tests/PackagesCheck/run.sh` writes such a `nuget.config`;
- a head for each platform: `Thornwick.Linux` (its project and `ThornwickApp.cs`), or `Thornwick.Windows`;
- three assets to start from, in `Thornwick.Game/Assets`: the game settings, a graphics compositor and a scene with nothing but a camera, at (0, 0, 10) and not turned. Thornwick's started as copies of the check game's (`tests/StudioCheck/StudioCheck.Game/Assets`), its scene emptied but for the camera, and its compositor without Tessera's two render features, which part 6 adds. A scene made by Game Studio has a skybox and a light too, and its camera is turned: delete the skybox and the light, and set the camera's rotation to (0, 0, 0), or the skybox shows where black should, around the house;
- the code: the game's rules and its four scripts are the `.cs` files of `Thornwick.Game`. Copy them into your game library, and build its head once, before part 5: parts 5 and 6 give the scripts to entities, which *Add component* lists only once the library is built, and part 7 says what each does.

The art is Kenney's Tiny Town, Tiny Dungeon and RPG Urban Pack (CC0): their sheets are in `Thornwick.Game/Resources/Kenney/`.

## 1. A tile set from an image

A tile set cuts images into tiles, and holds what the game needs to know about each tile.

1. In the asset view, *Create folder* (in its menu) makes a folder; name it `Tiles`.
2. *Add asset > Sprite > Tile set* makes `TileSet`. Click it, then F2 to rename it: `Town`.
3. Double click it to open the tile set editor, then `+ Image`, and pick the pack's sheet. The bar above the canvas shows the image's name, the tile size (16 × 16), the margin and spacing (0), and the number of tiles it gives: "12 × 11 tiles". Check them, then `Add`: the image is the tile set's source 1.

![The tile set editor with the bar of a new image](guide/1-tile-set.png)

A tile set can have several images. Thornwick's `Town` has Tiny Town's sheet as source 1 and a pond drawn from it as source 2. A tile is named by its source, its column and its row: `1:5,0` is source 1, column 5, row 0. The names stay when tiles are added, so a painted map never changes by itself. Thornwick has two more tile sets made the same way: `Indoors` (Tiny Dungeon, for the house) and `People` (the figures).

The result: `Thornwick.Game/Assets/Tiles/Town.sdtileset`, `Indoors.sdtileset` and `People.sdtileset`.

## 2. Tile data: what is solid, kinds and properties, an animated tile

Tile data belongs to the tile: every cell of every map that holds the tile has it. This is where Thornwick says what stops the hero, what a bramble is and where a door leads.

**Solid tiles.** In `Solid` mode, a click on a tile, or a drag across tiles, makes them solid or not; solid tiles are hatched. A drag sets the tiles under the pointer's path, so a block is dragged row by row. In `Town`, the trees, the fences, the walls, the well, the beehive, the bush and the pond are solid; the door of the hero's house is not.

**Kinds and properties.** In `Select` mode, a click on a tile shows its data in the property grid: its collision, its kind (a word), and its properties, each a name with a value of a type (bool, int, float, string or colour). The `+` beside `Properties` asks for the new property's name (`Key name`); then pick its type in the list beside it, and type its value. Thornwick gives:

| Tile | Kind | Properties |
|---|---|---|
| The bush, `1:5,0` of `Town` | `bush` | `cut` = `1:5,1` (string): the sprout that takes its place |
| The house's door, `1:5,7` of `Town` | `door` | `to` = `House`, `map` = `Maps/House` (strings) |
| The mat, `1:6,3` of `Indoors` | `door` | `to` = `Town` |

The list of types that opens beside a new property is hard to read (dark text on a dark background, a finding of Game Studio); `String` is the fifth.

![The bush's data in the property grid](guide/2-tile-data.png)

**An animated tile.** `Animation` turns on the third mode. Click the tile to animate (the middle of the pond, `2:1,1`), then each tile of its frames, in order (`2:1,1`, `2:0,3`, `2:1,3`). The bar under the canvas shows the frames with their durations; select one to set its duration in the property grid (0.3 s each in Thornwick). `Done` ends the mode. Every cell with that tile plays, in the map editor, the scene view and the game.

![The pond's water, an animated tile of three frames](guide/2-water.png)

The result: the `Tiles:` list of `Thornwick.Game/Assets/Tiles/Town.sdtileset` and `Indoors.sdtileset`.

## 3. A map: layers, the tools, the frame

1. In a folder `Maps`, *Add asset > Sprite > Tile map* makes `TileMap`, with one layer, `Ground`. Rename it `House`.
2. In the property grid, set `Size` to (12, 9). This is the map's frame: the editor outlines it, the game's camera stops at it, and Thornwick's walkers never step out of it.
3. Open the map. `+` beside the tile sets adds one (`Indoors`), and the palette shows its tiles. `+` under the layers adds a layer, `Layer 1`, above `Ground`, and a double click renames it: `Details`. Layers are drawn bottom first.
4. Pick tiles in the palette, a click for one tile or a drag for a block, and paint with the tools of the bar: `Stamp` (B), `Rectangle` (P), `Fill` (F), `Erase` (E), `Pick` (I) and `Select` (R, then drag the selection to move it). Shift and a click paints a line from the last painted cell. `Flip X`, `Flip Y` and `Rotate` (X, Y and Z) turn the stamp. The keys work while the canvas has the focus: [Tessera's README](../README.md#keys-of-the-map-editor) lists them all. `Dim other layers` fades the layers you are not painting. A block is stamped centred on the pointer, as in Tiled; the preview under the pointer shows where it goes. Ctrl+Z takes a stroke back, and Ctrl+S saves.

![The house painted in the map editor](guide/3-house.png)

Thornwick's house has two layers: the floor on `Ground`, the walls, the furniture and the mat on `Details`. The town has three: `Ground`, `Details` (walls, trees, fences, brambles: where the hero walks) and `Overhead` (the roofs and the crowns of the trees, drawn over the hero). The game treats a solid tile on any layer as a wall, so a roof is not solid, and the wall under the roof's lower row is.

**A large map, written by a tool.** The town is 60 by 40 cells on three layers, 7200 cells, too many to paint by hand. Thornwick writes it from a text picture, a character for each cell, with a small tool: see ["The town" in the sample's README](../sample/Thornwick/README.md#the-town). Game Studio opens the written map as any other, and a stamp and a save change one line of its file.

![The town, written by the tool, in the map editor](guide/3-town.png)

The result: `Thornwick.Game/Assets/Maps/House.sdtilemap`, and `Town.sdtilemap` written from `tools/maps/Town.txt`.

## 4. A flipbook with Sheet

A flipbook animates a character with tiles of a tile set: animations by name (`idle`, `walk`), each with a clip for each direction, each clip a list of frames.

1. In a folder `Flipbooks`, *Add asset > Sprite > Flipbook* makes `Flipbook`; rename it `Hero`. Its editor opens with an animation `idle` and a clip "Any direction", and says what is missing under the editor.
2. `Pick a tile set`, then `People`: the palette shows the figures.
3. `Sheet`, then drag over the hero's block in the palette, the first person, from `1:0,0` to `1:2,3`: three columns (a step, standing, the other step) by four rows. The bar reads "3 by 4" and gives each row a direction: Down, Left, Right and Up. Pick `There and back`, a duration of 0.1 s and the name `walk`, then `Make`: `walk` gets four clips of four frames, a step, standing, the other step, standing.
4. `Sheet` again over the middle column only ("1 by 4"), the name `idle`, `Make`: one standing frame for each side. Select "Any direction" of `idle` and `-` removes it.

![The Sheet bar over the hero's block](guide/4-sheet.png)

`Loop` under the preview plays the selected clip round and round. `<`, `>` and Delete move and remove frames of the strip, and `Replace` gives the selected frame the next tile picked in the palette.

![The hero's flipbook, with idle and walk in four directions](guide/4-hero.png)

**A sheet with a column for each side.** `Sheet` makes a clip of each row. Many character sheets, the RPG Urban Pack's among them, have a column for each side and a row for each step instead. Thornwick lays its people out again with a script, `tools/draw-people.sh` (ImageMagick): a block of three columns by four rows for each person, one row a side. The same script darkens the figures' outline to the town's.

The result: `Thornwick.Game/Assets/Flipbooks/Hero.sdflipbook`, and `Villager1` to `Villager3`, made the same way from the second, the third and the sixth person (`1:3,0` to `1:5,3`, `1:6,0` to `1:8,3`, and `1:15,0` to `1:17,3`).

## 5. The scene: maps and flipbooks as entities

Maps and flipbooks go into a scene as entities with a `Tile map` or a `Flipbook` component.

1. Drop `Maps/Town` from the asset view into the scene view: an entity `Town` with the map. Set its position to (0, 0, 0).
2. *Create > 2D > Tile map* (the `+` of the scene tree) makes a second one: name it `House`, at (1000, 0, 0), and leave its map empty. The places are in one scene, so they must be far enough apart that no view of one shows the other: a window as wide as 7680 pixels shows 480 cells at zoom 1. The game loads the house's map the first time the hero goes in (part 7).
3. Drop `Flipbooks/Hero` into the scene view: an entity `Hero`, playing `idle`. Give it the script `PlayerController`: its `Comes From` (`House`) puts the hero beside the door that leads to the house when the game starts, whatever the entity's position, and an empty `Comes From` starts the hero on the cell under the entity. Set it on that cell anyway, so that the scene view shows the hero where the game does: (28.5, 23, 0.15). X is the middle of the cell and Y its bottom, as the figure stands with its feet on the cell. Z puts the hero between two layers: the map draws each layer at its own depth (the index of the layer times the component's `Layer Spacing`, 0.1: `Ground` at 0, `Details` at 0.1, `Overhead` at 0.2), so a figure at 0.15 is over the ground and the details, and under the roofs.
4. Drop the three villagers' flipbooks on cells of the square, at the same z: (25.5, 18), (29.5, 20) and (32.5, 17). Give each the script `Wanderer`, with a `Radius` of 4 and a `Seed` of its own (1, 2 and 3), so that they do not walk alike.

If the scene view draws no map, the compositor lacks Tessera's features: do part 6 first.

![The scene: the town, the hero and the villagers](guide/5-scene.png)

The result: `Thornwick.Game/Assets/MainScene.sdscene`.

## 6. The compositor, the sort mode and the camera

**The compositor.** Open `GraphicsCompositor`:

- *Add package features*, above the render features, adds `TileRenderFeature` and `FlipbookRenderFeature`, each with its selector on the transparent stage. Without them, nothing of Tessera is drawn.
- Select the stage `Transparent`, and set its `Sort Mode` to "Back to front along an axis" (`TileAxisSortMode`, along Z): a figure between two layers then stays between them under any camera, the scene view's too.
- In the game's forward renderer, set `Post Effects` to `None`. Tone mapping and the other post effects change the colours of everything drawn before them; without them, a pixel of grass in the game is the pack's colour exactly.
- In the same forward renderer, set `Clear` > `Color` to black, `#FF000000`: it is what shows around the house, which is smaller than the window.

![The compositor with Tessera's two render features](guide/6-compositor.png)

**The camera.** Set the scene's camera to `Orthographic`, looking straight at the map from z 10, its rotation (0, 0, 0). Thornwick's script `FollowCamera` (part 7) moves it: give it the hero as `Target`, with the pick button of the field, which lists the scene's entities, and a `Priority` of 100, so that it runs after the hero moved. Pixel art wants a whole zoom, where every pixel of a tile takes the same number of screen pixels: the script shows the largest whole zoom that still shows 12 cells of height (3 in a window of 1280 by 720, where a tile takes 48 by 48 pixels), keeps the view inside the map's frame, and puts the view on whole pixels, so that no tile changes size as the camera moves.

![The camera, its script and the script's help text](guide/6-camera.png)

**A map loaded by its URL.** The house's map is named only by the door's property, so nothing in the scene says the game needs it. In the asset view, the menu of `Maps/House` has *Include in build as root asset*: the build then keeps it.

The result: `Thornwick.Game/Assets/GraphicsCompositor.sdgfxcomp`, the camera in `MainScene.sdscene`, `RootAssets` in `Thornwick.Game.sdpkg`, and the camera's rules in `CameraFraming.cs`.

## 7. The scripts

Four scripts connect the game's rules to Stride. Once the game library is built, *Add component > Scripts* lists them, and the property grid shows their properties. For the property grid to show a script's `<userdoc>` text too, the game library sets `GenerateDocumentationFile` in its project, and Game Studio reads the file beside the build of the startup head (`Thornwick.Linux`): build it once.

![The game's four scripts in Add component](guide/7-scripts.png)

| Script | On | What it does |
|---|---|---|
| `World` | an entity `World` | Finds the places by name, takes the doors (and loads a place's map the first time), counts the brambles into the window's title, and closes the game on Escape |
| `PlayerController` | `Hero` | Walks the hero with the keys, cuts with Space, takes the doors |
| `Wanderer` | each villager | Walks to a free cell near where the villager started, then stands a while |
| `FollowCamera` | the camera | Follows the hero at a whole zoom, inside the map's frame |

The hero's script, whole. Beside each line, what it asks Tessera:

```csharp
public override void Update()
{
    if (Walker is not { } walker || world is null)
    {
        return;
    }
    var arrived = walker.Advance(FrameTime.Of(Game.UpdateTime.Elapsed));
    // Read every frame: a key pressed during a step is kept for the arrival
    var (cut, side) = commands.Read(Input.Keyboard, walker.IsMoving);
    if (!walker.IsMoving)
    {
        // A door takes the hero when they arrive on it, and again each frame while someone stands where they would arrive
        if (arrived || waitsAtDoor)
        {
            waitsAtDoor = world.TakeDoor(walker) == DoorResult.Waits;  // the tile's Kind "door", its Properties "to" and "map"
        }
        if (cut)
        {
            world.Cut(walker);                                         // Brambles.TryCut, below
        }
        else
        {
            walker.TryStep(side);                                      // a step only to a free cell: IsSolid of the map
        }
    }
    Entity.Transform.Position = walker.Place.ToWorld(walker.Position); // cells to the world, with the map's CellSize
    flipbook?.Direction = walker.Facing;                               // the flipbook plays the clip of that side
    flipbook?.Play(walker.IsMoving ? "walk" : "idle");                 // goes on with the animation when it is the one playing
}
```

A step goes to a neighbour cell only when it is free: inside the frame, solid on no layer (`TileMapComponent.IsSolid`, which reads the solid flag of part 2), and held by no other walker. The walker moves to it in 0.2 s, and a held key keeps it walking.

The brambles, whole. Space cuts the bush on the cell the hero faces:

```csharp
public static bool TryCut(TileMap map, Int2 cell, out string? problem)
{
    problem = null;
    for (var index = map.Layers.Count - 1; index >= 0; index--)
    {
        var layer = map.Layers[index];
        var value = layer.GetCell(cell);                                // the cell: its tile set's slot, its tile, its flip
        if (map.GetTile(value) is not { Kind: TileKinds.Bush } bush)    // the tile's data, and its Kind from part 2
        {
            continue;
        }
        var left = TileCell.Empty;
        if (bush.Properties.TryGetValue(TileProperties.Cut, out var cut))  // the tile's Properties from part 2
        {
            if (cut is string text && TileId.TryParse(text, out var tile) && map.TileSets.TryGetValue(value.Slot, out var tileSet)
                && tileSet.Tiles.ContainsKey(tile))                     // "1:5,1" names a tile of the same tile set
            {
                left = new TileCell(value.Slot, tile, value.Flip);
            }
            else
            {
                problem = $"The bush at ({cell.X}, {cell.Y}) has '{TileProperties.Cut}' = '{cut}', which is no tile of its tile set: the cell is emptied.";
            }
        }
        layer.SetCell(cell, left);                                      // shown in the next frame
        return true;
    }
    return false;
}
```

A bush without `cut` is emptied, and one whose `cut` names no tile is emptied too, with the problem that `World` writes in the log. The rest, by name:

- `Place`: a map of the scene, by its entity's name; its frame (`TileMap.Size`), who stands on which cell, and the depth of what walks there.
- `GridWalker`: a walker on a place, standing on a cell or stepping to a neighbour; the hero and the villagers are walkers.
- `Doors`: the door tiles of a map (`TileKinds.CellsOf(map, "door")`), where a walker arrives through one, and the taking of a door.
- `WanderPlan`: where a villager steps next and how long it stands, from a seed.
- `CameraFraming`: the zoom, and where the camera looks.
- `HeroKeys`, `HeroCommands` and `FrameTime`: which side the keys ask for, what the hero does next (a key pressed during a step is kept for the arrival), and how long a frame counts for (a long frame moves a walker one cell at most).

These rules are plain classes, and `Thornwick.Tests` tests them without a running game; the tests also read the asset files with Tessera's asset classes, to check that every cell's tile exists, that the door leads to the house and back, and that the hero is not walled in.

The result: `Thornwick.Game/*.cs`, and the tests in `Thornwick.Tests`.

## 8. Build and play

From `sample/Thornwick`:

```bash
dotnet build Thornwick.Linux
cd Thornwick.Linux
dotnet run
```

The build compiles the assets first. On Windows, the head is `Thornwick.Windows` (not tried yet). The tests: `dotnet test Thornwick.Tests`.

| Key | What it does |
|---|---|
| The arrows, or W, A, S and D | Walk one cell that way; hold to keep walking |
| Space | Cut the bramble the hero faces |
| Escape | Close the game |

The hero steps out of the house's door, facing down. The window's title counts the brambles, twelve at the start; three of them close a lane, with a garden behind. Under the roofs and the crowns of the trees, the hero is hidden. Stepping onto the door shows the house, centred, black around it; the mat leads back out, and the brambles cut stay cut.

![The game at the start](guide/8-town.png)

![Inside the house](guide/8-house.png)

**What to try next.**

- **A second door.** Pick a door tile that no other building of the town uses, and give it tile data: not solid, the kind `door`, `to` the name of a new place, and `map` its map's URL. Make that map with a mat inside whose `to` is `Town`, an entity of the place's name with an empty tile map component, and include the map in the build as a root asset. `Doors` and `World` need no change.
- **A new kind of tile.** Give a tile a kind of your own (a signpost, `sign`, with a string property `text`) and read it in `PlayerController` on Space, as `Brambles.TryCut` reads `bush`: `map.GetTile(layer.GetCell(cell))`, then its `Kind` and `Properties`.
