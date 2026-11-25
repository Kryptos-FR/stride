# StudioCheck

A small game to check Tessera in Game Studio by hand. What was seen in it is in [`docs/studio-check.md`](../../docs/studio-check.md).

The solution `StudioCheck.slnx` holds Tessera's projects (in the folder `Tessera`), the game library `StudioCheck.Game` and its Linux head `StudioCheck.Linux`. Game Studio builds Tessera's projects with the game and loads the editor's plugins from them, so a change to Tessera shows the next time the solution is opened.

## What is in it

- `StudioCheck.Game/Resources/town.png`: four by three tiles of 16 pixels (grass, sand, water, wall; roof, door, tree, flower; the water twice more with its waves moved, a path, a bush). `draw-town.sh` draws it again with ImageMagick.
- `StudioCheck.Game/Resources/hero.png`: three by four figures of 16 by 24 pixels, a row for each way they face (down, left, right, up), a column for each step. `draw-hero.sh` draws it again with ImageMagick.
- `Assets/Sprites/Props`: a sprite sheet of the tree and the flower of `town.png`.
- `Assets/TileSet`: a tile set with the grid source `town` and the sheet source `Props`. Its water is an animated tile of three frames, the water of row 0 and the two of row 2.
- `Assets/Hero`: a tile set with the grid source `hero`, of 16 by 24 pixel tiles.
- `Assets/HeroFlipbook`: a flipbook on `Hero`, with its pivot at the bottom middle. `idle` has one frame for any direction; `walk` loops, with a clip of four frames for each of the four directions.
- `Assets/TileMap`: a map of two layers, `Bridge` and `Layer 1`, on the tile set.
- `Assets/MainScene`: an orthographic camera, the sprite `Hero` between the two layers, two entities with a tile map, and two figures with `HeroFlipbook` between the two layers: `HeroFlipbook` stands and shows `idle`, and `Walker` plays `walk` and has the script `Walker`.
- `StudioCheck.Game/Walker.cs`: a script that walks its entity around a square of two units a side, two seconds a side, facing the way it goes.
- `Assets/GraphicsCompositor`: Stride's forward renderer, with Tessera's two render features and the sort mode "Back to front along an axis" on the transparent stage.

## Open it in Game Studio

Build Game Studio from this checkout (`sources/editor/Stride.GameStudio.Avalonia.Desktop`), then give it the solution:

```bash
Stride.GameStudio.Avalonia.Desktop extensions/ColorRise.Tessera/tests/StudioCheck/StudioCheck.slnx
```

On Linux without a display or a GPU, run it under Xvfb with Lavapipe:

```bash
Xvfb :99 -screen 0 1600x1000x24 &
export DISPLAY=:99
export VK_DRIVER_FILES=~/.nuget/packages/stride.dependencies.lavapipe/<version>/runtimes/linux-x64/native/lvp_icd.json
```

The projects use the Stride packages of `TesseraStrideVersion` (see `Directory.Build.props` of Tessera).

## Run the game

```bash
cd StudioCheck.Linux
dotnet run
```

The game shows the map from the scene's camera, its water playing, and the walker going around the standing figure, behind it on the top side of its square and in front of it on the bottom side. The build compiles the assets first (`Bin/Linux/Debug/data`).
