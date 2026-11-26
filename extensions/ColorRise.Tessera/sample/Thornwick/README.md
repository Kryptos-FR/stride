# Thornwick

A small top-down game made with Tessera's editors: a hero walks a town of three layers, cuts brambles, goes into a house and out again, among villagers who wander.

The solution `Thornwick.slnx` holds Tessera's projects (in the folder `Tessera`), the game library `Thornwick.Game`, its two heads `Thornwick.Linux` and `Thornwick.Windows`, and the tests `Thornwick.Tests`. Game Studio builds Tessera's projects with the game and loads the editor's plugins from them.

The projects use the Stride packages of `TesseraStrideVersion` (see `Directory.Build.props` of Tessera).

## Open it in Game Studio

Build Game Studio from this checkout (`sources/editor/Stride.GameStudio.Avalonia.Desktop`), then give it the solution:

```bash
Stride.GameStudio.Avalonia.Desktop extensions/ColorRise.Tessera/sample/Thornwick/Thornwick.slnx
```

On Linux without a display or a GPU, run it under Xvfb with Lavapipe, as the check game's [README](../../tests/StudioCheck/README.md) says.

## Run the game

```bash
cd Thornwick.Linux
dotnet run
```

The build compiles the assets first (`Bin/Linux/Debug/data`).

## Test it

```bash
dotnet test Thornwick.Tests
```

The tests read the game's files from the folder beside them.

## The art

The art is by Kenney (www.kenney.nl), under Creative Commons Zero (CC0 1.0): Tiny Town, Tiny Dungeon and the RPG Urban Pack. Each pack's sheet without spacing and its licence are in `Thornwick.Game/Resources/Kenney/<Pack>/`. See [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).
