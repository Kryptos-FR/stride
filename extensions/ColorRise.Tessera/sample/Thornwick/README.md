# Thornwick

Brambles grew over the village of Thornwick in the night.
Step out of your house and cut them all.

A small top-down game made with Tessera's editors: a hero walks a town of three layers, cuts brambles, goes into a house and out again, among villagers who wander. [The guide](../../docs/guide.md) makes it step by step.

The solution `Thornwick.slnx` holds Tessera's projects (in the folder `Tessera`), the game library `Thornwick.Game`, its two heads `Thornwick.Linux` and `Thornwick.Windows`, and the tests `Thornwick.Tests`. Game Studio builds Tessera's projects with the game and loads the editor's plugins from them.

The projects use the Stride packages of `TesseraStrideVersion` (see `Directory.Build.props` of Tessera).

## Run the game

On Linux, from this folder:

```bash
cd Thornwick.Linux
dotnet run
```

On Windows, the same with the other head (not tried yet: the sample was made and played on Linux):

```bash
cd Thornwick.Windows
dotnet run
```

The build compiles the assets first (`Bin/Linux/Debug/data`, or `Bin/Windows/Debug/data`). On Linux without a display or a GPU, the game runs under Xvfb with Lavapipe, as Game Studio does.

## Play

The window's title counts the brambles left, twelve at the start.

| Key | What it does |
|---|---|
| The arrows, or W, A, S and D | Walk one cell that way; hold the key to keep walking. The hero turns to a wall, a tree, the water, a fence or a villager and stays. |
| Space | Cut the bramble the hero faces: a sprout takes its place, and the hero can walk over it. |
| Escape | Close the game. |

The hero starts under the door of their house. Stepping onto a door goes through it: the house's door, in the town, leads into the house, and the mat inside leads back out. The three brambles across the lane, in the north-west, keep the garden behind them until they are cut.

## Open it in Game Studio

Build Game Studio from this checkout (`sources/editor/Stride.GameStudio.Avalonia.Desktop`), then give it the solution:

```bash
Stride.GameStudio.Avalonia.Desktop extensions/ColorRise.Tessera/sample/Thornwick/Thornwick.slnx
```

On Linux without a display or a GPU, run it under Xvfb with Lavapipe, as the check game's [README](../../tests/StudioCheck/README.md) says.

Build `Thornwick.Linux` once before: Game Studio lists the game's scripts once the game library is built, and shows the help text of their properties from `Thornwick.Game.xml`, which the build puts beside the startup head's output (`Thornwick.Game.csproj` sets `GenerateDocumentationFile` for it).

## What is where

| What | Where |
|---|---|
| The rules, plain classes tested without a running game | `Thornwick.Game`: `Place` (a map of the scene, its frame, who stands where), `GridWalker` (a walker on the cells), `WanderPlan` (a villager's next step), `CameraFraming` (the zoom and the view), `Brambles`, `Doors` and `TileKinds` (what the game reads from tile data), `HeroKeys` and `FrameTime` |
| The scripts | `Thornwick.Game`: `World` (the places, the doors, the count of the brambles in the title), `PlayerController` (the hero), `Wanderer` (a villager), `FollowCamera` |
| The assets, made in Game Studio | `Thornwick.Game/Assets`: the tile sets `Tiles/Town`, `Tiles/Indoors` and `Tiles/People`; the flipbooks `Flipbooks/Hero` and `Villager1` to `3`; the map `Maps/House`; `MainScene` and `GraphicsCompositor` |
| The town, written by a tool | `Thornwick.Game/Assets/Maps/Town.sdtilemap`, from `tools/maps/Town.txt` by `tools/MakeMaps.cs` (see [The town](#the-town)) |
| The art | `Thornwick.Game/Resources/Kenney`, and the two scripts that draw from it, `tools/draw-people.sh` and `tools/draw-water.sh` (see [The art](#the-art)) |
| The tests | `Thornwick.Tests`: the rules, and the asset files read with Tessera's asset classes (every cell's tile exists, the door leads to the house and back, the hero is not walled in, the flipbooks have their clips) |

## Test it

```bash
dotnet test Thornwick.Tests
```

The tests read the game's files from the folder beside them.

## The town

The town, `Assets/Maps/Town.sdtilemap`, is 60 by 40 cells on three layers: 7200 cells, too many to paint by hand. A text picture, `tools/maps/Town.txt`, draws it a character for each cell, and a tool writes the map's file from it, from this folder:

```bash
dotnet run tools/MakeMaps.cs -- Thornwick.Game
```

The tool is a .NET file-based app, without a package or a project. For each picture of `tools/maps` it writes the map's file whole, as Game Studio writes it, with ids made from names: two runs write the same bytes. A picture has, in order:

```text
# A line that starts with # is a remark.
map Maps/Town 60 40                                          # the URL of the map, and its size in cells
tileset 1 = 2199ea15-ee1f-4a43-8905-05ff37348fbc:Tiles/Town  # a tile set: its id is the line Id: of its file
tile g = 1/1:0,0                                             # a character, and the cell it stands for
tile ] = 1/1:8,6h                                            # the flips d, h and v follow the tile id, in that order
layer Ground                                                 # a layer, the bottom one first
gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg
...                                                          # 40 lines of 60 characters, the top row first
```

A cell is written as the map's file writes it: the slot of its tile set, a `/`, the tile id as the tile set editor shows it, and the flips. A dot or a space is an empty cell. The tool stops, with the file, the line and the reason, on a character without a `tile` line, a layer without its rows or with a row of the wrong width, a layer named twice, a slot without a `tileset` line, or a picture without its `map` line; it then writes nothing. Whether a tile exists is for the tests to say (`TownTests`). They also save the town as a picture, `Thornwick.Tests/bin/Debug/net10.0/pictures/Town.png`, to look at after a change.

A change made to the town in Game Studio is lost the next time the tool runs: change the picture instead. The house is the other way: it is painted in the map editor, and the tool does not write it.

## The art

The art is by Kenney (www.kenney.nl), under Creative Commons Zero (CC0 1.0): Tiny Town, Tiny Dungeon and the RPG Urban Pack. Each pack's sheet without spacing and its licence are in `Thornwick.Game/Resources/Kenney/<Pack>/`. See [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).

Two pictures are drawn from the sheets by scripts, with ImageMagick (`convert`); each script writes the same bytes every time it runs:

- `tools/draw-people.sh` draws `RpgUrbanPack/people.png`, the figures. The pack has a column for each side a person faces and a row for each step; the flipbook editor's `Sheet` makes a clip of each row, so the script lays the six people side by side, each a block of three columns (a step, standing, the other step) by four rows (facing down, left, right and up). It also gives the outer line of each figure the colour of Tiny Town's outlines.
- `tools/draw-water.sh` draws `TinyTown/water.png`, the pond, as Tiny Town has no water: the pack's patch of earth with its grass banks, in nine pieces, the earth turned blue. The middle piece has short waves, and two more copies of it with the waves lower make the frames of the animated water.
