# ColorRise.Tessera, step 4: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task, in one session, with one fresh reviewer of the whole branch at the end (Nicolas's choice for Tessera, 2026-11-24). Steps use checkbox (`- [ ]`) syntax for tracking.

**Status: APPROVED by Nicolas on 2026-11-26 (17:41, Paris): "go ahead".** Written 2026-11-26. He answered the three questions for him (decisions 1 to 3 below) the same day, each with the answer the plan is written on; the art, decision 1, after it was checked from the three packs. The build runs in a thread of its own, from the execution brief (`/mnt/project-files/tilemap/2026-11-26-tessera-step4-execution-brief.md`).

**Goal:** A small top-down game, Thornwick, is made with Tessera's editors and played: a hero walks a town of three layers, cuts brambles, goes into a house and out again, among villagers who wander. With it, a guide and a README that say how it was made.

**Architecture:** The sample is a game beside the extension, laid out as Skein's Brinemoor: a game library, Linux and Windows heads, a test project, and a solution that also holds Tessera's projects. The rules of the game are plain classes over Tessera's runtime (a place, a walker on a grid, a wander plan, the camera's frame, brambles and doors read from tile data), tested without a running game; four short scripts connect them to Stride. Doors and brambles are tile data, a kind and properties, so the design's three tests read only the map and tile set files. The town's cells are written by a small tool from text pictures; the rest of the sample is made by hand in Game Studio, and that making is the by-hand check and the source of the guide's pictures.

**Tech Stack:** .NET 10, C# (latest), Stride 4.5.4-dev packages, xunit 2.9.3, a .NET file-based app for the map tool, Lavapipe (software Vulkan), Xvfb and xdotool for Game Studio and the game by hand, ImageMagick for the pictures.

**Spec:** [design.md](design.md), approved by Nicolas on 2026-11-24, with its notes of 2026-11-25. "The design" below means this file, and "section N" is a section of it. This step is section 12 (item 4: "the sample game, the README and the guide"), section 13 (the sample), section 14 (its tests, and the run by hand) and section 16 (the sample's name and its small story). The plans of steps 1 to 3 are [plan.md](plan.md), [plan-step2.md](plan-step2.md) and [plan-step3.md](plan-step3.md): their "Done differently" notes say what the code really is.

## Scope

Step 4 of the design's four, the last of the first version, on `feature/colorrise-tessera-sample`, from `feature/colorrise-tessera-flipbooks` at `cbce8f846`: the sample game in `extensions/ColorRise.Tessera/sample/Thornwick/`, its tests, the guide `docs/guide.md`, the README, the third-party notice of the art, and the by-hand run of section 14 over the sample.

Not in this step:

- A screen of text over the game (a HUD), sound, a save, a title screen, a fight. The window's title says how many brambles are left.
- Free movement with `OverlapsSolid`: the hero moves cell by cell (decision 6). The README keeps its example of `OverlapsSolid`.
- Object layers: the villagers are entities of the scene, and doors are tiles with a kind (decision 7).
- A check on Windows and the WPF Game Studio. The Windows head is written as Brinemoor's is, and not built here.
- The minors of earlier steps that decision 3 leaves waiting.
- Terrain autotiling, Tiled import, physics, parallax (section 15).

## What step 4 decides

The brief of this step lists eight things to decide. Three go to Nicolas on cards (1 to 3); the rest are the planner's calls, to correct while reading.

**For Nicolas (D)**

1. **The art.** *Decided by Nicolas on 2026-11-26 (17:40, Paris): "Urban's people, outline darkened", the first of the four ways below, on a card with the picture `art/check/looks.png` of the four in one small scene.* Read from the three zips he uploaded, all CC0 with tiles of 16 pixels: Kenney's "Tiny Town" (the design's pick) has the village and no figure at all; "Tiny Dungeon" has the indoors and about 20 figures, each one picture that faces the front; the "RPG Urban Pack" has six people in four directions with three pictures each (standing and two steps), in a modern city. Kenney's platformer packs that Nicolas named on 2026-11-26 ("Platformer Art Deluxe", "Platformer Art Extended Enemies", "Platformer Pack Remastered") are side views, far larger than 16 pixels: not for this game. The four ways: **(a)** Tiny Town for the town, Tiny Dungeon for the house, and the Urban pack's people, their outline darkened to Tiny Town's by a script; **(b)** the same, the people's colours untouched; **(c)** Tiny Dungeon's figures, whose walk is a bob made by a script, a flip for left and right, and no back view; **(d)** the Urban pack alone, a city without a room indoors, with another story. The first keeps the village of the story and gives the flipbooks real clips for each direction, which is what the sample is there to show. See [The art](#the-art).
2. **How the maps are made.** *Decided by Nicolas on 2026-11-26 (14:54, Paris): "Script the overworld, paint the house".* A tool, `tools/MakeMaps.cs`, writes the town (60 by 40 cells, three layers) from text pictures, as `tools/MakeWorld.cs` writes the world of Skein's second sample. The house (about 12 by 9 cells), the tile sets, the tile data, the animated water, the flipbooks, the scene and the compositor are made by hand in Game Studio. The two other answers: the tool writes both maps (Task 9 then gives the tool a second picture and paints nothing), or both maps are painted by hand under `xdotool` (Task 8 then has no tool, and several hundred actions in the map editor).
3. **Which waiting minors go in.** *Decided by Nicolas on 2026-11-26 (14:55, Paris): "Four of the flipbook editor".* Four of step 3's eight, all in the flipbook editor, which whoever follows the guide uses: m2 (the hint "Select or add an animation first." when the animation has no clip), m3 (no way to change a frame's tile), m4 (Delete does nothing after a click on a button of the strip) and m6 (no warning for a clip whose frames all last 0). Task 1. The four others of step 3 (m1, m5, m7, m8) and the five of step 2 keep waiting: they need a merge, a hand-edited file or a measurement to be met.

**The planner's calls (C)**

4. **The name and the story** (section 16). The sample is **Thornwick**, a village name in the family of Skein's Brinemoor and Ashcombe. Its story fits a window's title: *brambles grew over Thornwick in the night; step out of your house and cut them all.* The game starts with the hero stepping out of the house's door, and the title counts: "Thornwick: 9 brambles left", then "Thornwick: all the brambles are cut".
5. **The layout**, as Brinemoor's: `sample/Thornwick/` with `Thornwick.slnx` (Tessera's four projects in a folder `Tessera`, then the game's), `Thornwick.Game` (the library with the assets), `Thornwick.Linux`, `Thornwick.Windows`, `Thornwick.Tests`, and `tools/`. The game references Stride's packages at `$(TesseraStrideVersion)` and Tessera's runtime project, as the check game does.
6. **Moving on the grid**: cell by cell with a tween, as the design says ("moved on the grid"). A walker stands on a cell or walks to a neighbour in 0.2 s (the hero) or 0.4 s (a villager); a held key keeps it walking without a hitch between cells; a key against a wall turns it. Two walkers never share a cell: a walker holds its cell and the one it walks to. No free movement in this sample.
7. **Places, doors and the second map.** The scene has both places: two entities with a tile map component, `Town` at the origin and `House` at x = 100, far outside any view of the town. A place is an entity of the scene that has a tile map component, and its name is the entity's; the script `World`, on an entity of its own, finds the places by name. Every question goes through the component (`WorldToCell`, `IsSolid`), and the cut brambles stay cut when the hero comes back. **A door is tile data**: a tile of the kind `door` with two string properties, `to` (the name of the place it leads to) and `map` (the URL of that place's map). The hero walks onto the door's cell and is taken. The `House` entity has no map in the scene: the first time the hero takes the door, **the game loads it** (`Content.Load<TileMap>` with the URL of the property) and gives it to the component, then puts the hero beside the door of that place that leads back. While someone stands where the hero would arrive, the hero waits on the door. Tile data belongs to the tile, so every cell that holds the door's tile leads to the house: the town has that tile once, and its other buildings show another door, which is solid. This is the design's "entered through a door: the game loads it and moves the player", with the door's data in the tile set and not in the scene, since the first version has no object layer.
8. **Brambles.** A tile of the kind `bush` is solid. Space cuts the bush on the cell the hero faces: the game reads the tile's kind, and puts in the cell the tile its string property `cut` names (a sprout, in this art; a tile id as text), or empties the cell when the tile has no such property. The town has 12, three of them across a lane.
9. **Villagers.** Three, each an entity of the scene with a flipbook and the script `Wanderer`, placed on a cell of the town: it walks one cell at a time to a free neighbour no further than 4 cells from where it stood at the start, then stands for 0.5 to 2.5 s. Each has a seed, so two runs look the same. They never step on a door. They are at the hero's z, so Tessera orders them and the hero by Y.
10. **The camera** is orthographic and looks straight at the map. Its zoom is a whole number: the largest that still shows 12 cells of height (3 in a window of 1280 by 720, where a pixel of a tile is 3 by 3 on screen). It follows the hero, stops at the frame of the place (`TileMap.Size`), centres a place smaller than the view, and stays on whole screen pixels.
11. **The tests of the sample** need no running game and no scene: they read the tile set, map and flipbook files with Tessera's own asset classes. The design's three: every cell's tile exists; the door leads to the second map (a walker takes it there and back); the hero is not walled in at the start. Then: the tile data the game reads is there, the flipbooks have `idle` and `walk` in four directions, the rules (walker, wander, camera, brambles, doors) by unit tests, and one test that draws each map to a picture for a person to look at (section 14: "the pictures the tests draw are saved beside the test results").
12. **The guide** is `docs/guide.md`: "Make a small game with Tessera", eight short parts in the order Thornwick was made (a tile set from an image; tile data and an animated tile; a map; a flipbook with Sheet; the scene; the compositor and the camera; the scripts; the build and the run), each with one or two pictures from the by-hand run, cropped and stored in `docs/guide/` (at most 14 pictures of at most 150 KB). It points at the sample's files for the whole code.
13. **The art in the repository**: the sheet without spacing of each pack (`Tilemap/tilemap_packed.png`) with the pack's `License.txt`, in `Thornwick.Game/Resources/Kenney/<Pack>/`; the two pictures the scripts of "The art" make from them, each beside the sheet it comes from; and a notice in `THIRD-PARTY-NOTICES.md`. The Urban pack's sheet (17 KB) is there as the source of the people's picture only: no tile set reads it. Not the zips, and not the single-tile files. The licence is read again from each zip before the first commit of art (Task 2).
14. **The by-hand check is the making of the sample.** Tasks 7, 9 and 10 make the tile sets, the flipbooks, the house, the scene and the compositor in Game Studio, each line with its picture, and Task 11 plays the game. The lines go to a part "Step 4" of `docs/studio-check.md`. Steps 2 and 3 each found four defects this way: each task that opens Game Studio has a step for fixes, with a test that fails first.
15. **Keys**: the arrows or W, A, S, D walk; Space cuts; Escape quits.

## The art

*Checked from the three zips on 2026-11-26, and written on the first of the four ways of decision 1, which Nicolas chose the same day. The zips are in `/mnt/project-files/tilemap/art/`. The folder `check/` beside them has the numbered sheet of each pack, the picture of the four ways (`looks.png`), the two scripts below and the pictures they make.*

**What the zips have.** Each has a `License.txt`, and `Tilemap/tilemap_packed.png`, the sheet without spacing, which is the one taken (`Tilemap/tilemap.png` is the same sheet with 1 pixel between tiles).

| Pack, as its licence names it | Zip (its sha256 starts with) | Sheet | What it has |
|---|---|---|---|
| Tiny Town (1.1), 11-01-2023 | `kenney_tiny-town.zip` (`9768692d`) | 192 by 176: 12 by 11 tiles of 16 pixels | A village: grass, earth, trees, roofs, walls, doors, fences, a bush, a well. No figure, and no water |
| Tiny Dungeon (1.0), 05-07-2022 | `kenney_tiny-dungeon.zip` (`c109438a`) | 192 by 176: 12 by 11 tiles of 16 pixels | Indoors: sand floors, brick walls, doors, furniture. About 20 figures, each one picture that faces the front. No water |
| RPG Urban Pack 1.0, 05-01-2019 | `kenney_rpg-urban-pack.zip` (`4541d89d`) | 432 by 288: 27 by 18 tiles of 16 pixels | A modern city, without a room indoors. Six people in columns 23 to 26: a column for each side (left, down, up, right), and three rows for each person (standing, a step, the other step). Water of one tile, in a pool |

The licence of each has the same four lines: "License: (Creative Commons Zero, CC0)", "http://creativecommons.org/publicdomain/zero/1.0/", "This content is free to use in personal, educational and commercial projects." and "Support us by crediting Kenney or www.kenney.nl (this is not mandatory)".

**Two things the files showed, and what the plan does about each.**

- **The people are laid out the other way.** The flipbook editor's `Sheet` makes one clip for each row of a block, and the Urban pack has a column for each side. Frame by frame, the four flipbooks would be 80 clicks in the palette, and the sample would show nothing of `Sheet`. So a script, `tools/draw-people.sh`, makes `people.png` from the pack's sheet: the six people side by side, each a block of three columns by four rows. The rows face down, left, right and up, which is the order `Sheet` proposes; the columns are a step, standing, the other step, so that `There and back` makes a walk (a step, standing, the other step, standing). The same script gives the outer line of each figure the colour of Tiny Town's outlines (`#3f2631` in place of the pack's lighter brown: 2970 pixels over the 72 pictures). That is the way Nicolas chose (decision 1). A `Sheet` that reads a column for each side is a wish for later, written in the report, and not built in this step.
- **Tiny Town has no water.** A script, `tools/draw-water.sh`, makes `water.png` from the pack's own patch of earth with its grass banks, nine pieces: the earth is turned to the blue of the pack's well (`#009adc`) and its specks to the light blue (`#76e4ff`). The middle piece gets two short waves, and two more frames with the waves lower. Only the middle piece plays: a pond is the nine pieces, the middle one on every cell inside.

**The files of the art**, in `T/Thornwick.Game/Resources/Kenney/` (decision 13). The two pictures the scripts make are derived from CC0 art, and the notice names them.

| File | From | Read by |
|---|---|---|
| `TinyTown/tilemap_packed.png`, `TinyTown/License.txt` | the zip | `Tiles/Town`, as its source 1, and the pond's script |
| `TinyTown/water.png` | `tools/draw-water.sh` | `Tiles/Town`, as its source 2 |
| `TinyDungeon/tilemap_packed.png`, `TinyDungeon/License.txt` | the zip | `Tiles/Indoors` |
| `RpgUrbanPack/tilemap_packed.png`, `RpgUrbanPack/License.txt` | the zip | the people's script only |
| `RpgUrbanPack/people.png` | `tools/draw-people.sh` | `Tiles/People` |

**The two scripts**, in `T/tools/`, as they were run while planning (ImageMagick 6.9.12). Two runs write the same bytes. The pictures they made are `check/people.png` and `check/water.png`: `compare -metric AE <the picture made> <the check's> null:` says 0 when the pixels are the same.

```bash
#!/usr/bin/env bash
# Draws Resources/Kenney/RpgUrbanPack/people.png from the pack's sheet with ImageMagick: 288 by 64 pixels, six people side
# by side, each a block of three columns by four rows of 16 pixel tiles. The rows of a block, from the top, face down, left,
# right and up; its columns are a step, standing, the other step. The pack has them the other way: a column for each side
# (left, down, up, right) and a row for each pose (standing, a step, the other step), in columns 23 to 26 of its sheet. The
# outer line of each figure takes the colour of the outlines of Tiny Town, so that the people sit on its tiles.
set -euo pipefail
cd "$(dirname "$0")/../Thornwick.Game/Resources/Kenney/RpgUrbanPack"

# The column of the pack for each row of a block (down, left, right, up), and its row for each column (a step, standing, the other step)
sides=(24 23 26 25)
poses=(1 0 2)
# The outer line of a tile: its pixels that touch a transparent one, or the edge of the tile, on one of their four sides
line=(\( +clone -fill '#3f2631' -colorize 100 \) \( -clone 0 -alpha extract -virtual-pixel black -morphology EdgeIn Diamond:1 -threshold 50% \) -geometry +0+0 -composite)

parts=()
for figure in 0 1 2 3 4 5; do
    for row in 0 1 2 3; do
        for column in 0 1 2; do
            x=$((sides[row] * 16))
            y=$(((figure * 3 + poses[column]) * 16))
            parts+=(\( tilemap_packed.png -crop "16x16+$x+$y" +repage "${line[@]}" \) -geometry "+$(((figure * 3 + column) * 16))+$((row * 16))" -composite)
        done
    done
done

convert -size 288x64 xc:none "${parts[@]}" -depth 8 +set date:create +set date:modify +set date:timestamp PNG32:people.png
```

```bash
#!/usr/bin/env bash
# Draws Resources/Kenney/TinyTown/water.png from the pack's sheet with ImageMagick: 48 by 64 pixels, three by four tiles of
# 16 pixels. Rows 0 to 2 are a pond in nine pieces: the pack's patch of earth with its grass banks (columns 0 to 2, rows 1
# to 3 of its sheet), the earth turned to the blue of the pack's well and its specks to the light blue. The middle piece has
# two short waves. Row 3 has the middle piece twice more, its waves 4 and 8 pixels lower, for an animated water; its third
# tile is empty.
set -euo pipefail
cd "$(dirname "$0")/../Thornwick.Game/Resources/Kenney/TinyTown"

convert -size 48x64 xc:none +antialias \
    \( tilemap_packed.png -crop 48x48+0+16 +repage -fill '#009adc' -opaque '#eaa56c' -fill '#76e4ff' -opaque '#cf8254' -opaque '#fec99c' \) \
    -geometry +0+0 -composite \
    -stroke none -fill '#009adc' -draw 'rectangle 0,48 31,63' \
    -fill '#76e4ff' \
    -draw 'rectangle 19,20 22,20' -draw 'rectangle 25,26 28,26' \
    -draw 'rectangle 3,56 6,56' -draw 'rectangle 9,62 12,62' \
    -draw 'rectangle 19,60 22,60' -draw 'rectangle 25,50 28,50' \
    -depth 8 +set date:create +set date:modify +set date:timestamp PNG32:water.png
```

**What the tasks take from this section.** The middle column is read from the files, with tile ids as the tile set editor shows them: `source:column,row`, the columns from the left and the rows from the top, both from 0. An id without a tile set's name is of `Tiles/Town`. The tasks name tiles by what they are, never by an id, and the tests find them by their data.

| What | Where it is in the art | Used by |
|---|---|---|
| The sheet of each tile set, its tile size and its number of tiles | `Tiles/Town`: `Kenney/TinyTown/tilemap_packed.png` first (source 1: 12 by 11, 132 tiles), then `Kenney/TinyTown/water.png` (source 2: 3 by 4, 12 positions, of which the last is empty). `Tiles/Indoors`: `Kenney/TinyDungeon/tilemap_packed.png` (12 by 11, 132 tiles). `Tiles/People`: `Kenney/RpgUrbanPack/people.png` (18 by 4, 72 tiles). Every tile is 16 by 16, without margin or spacing | Task 2 (the files), Task 7 (line 3) |
| The tiles that stop the hero | Solid: the walls, `1:0,6` to `1:7,7`, but not the door of the hero's house; the trunks `1:3,1` and `1:4,1`; the small trees `1:3,2` and `1:4,2`; the tree groups `1:6,0` to `1:11,2`, which stand on `Details` at the edge of the town; the fences, the cart and the signpost, `1:8,3` to `1:11,6`; the well `1:8,7` and `1:8,8`; the beehive `1:10,7`; the bush `1:5,0`; the nine pieces of the pond, `2:0,0` to `2:2,2`. **Not** solid: the roofs, `1:0,4` to `1:7,5`, which go on `Overhead` and hide the hero; the crowns `1:3,0` and `1:4,0`; the grass, the earth, the cobbles, the stone floor, the sprout, the mushrooms. In `Tiles/Indoors`: the walls and the furniture, below | Task 7 (line 4) |
| The water, and its frames | The middle of the pond, `2:1,1`, plays: its frames are `2:1,1`, `2:0,3` and `2:1,3`, 0.3 s each. The eight pieces around it, in `2:0,0` to `2:2,2`, are the banks, and do not play. All nine are solid, and go on `Ground` | Task 7 (Step 3, line 5) |
| The bush, and the tile left after a cut | The bush `1:5,0`: the kind `bush`, solid, `cut` = `1:5,1`. What is left is the sprout `1:5,1`, which is not solid | Task 7 (line 4) |
| The door of the hero's house, and another door for the other buildings | The hero's house has stone walls, as the room inside has: its door is `1:5,7`, with the kind `door`, and it is not solid. The other buildings have wooden walls and solid doors: `1:1,7`, or the double door `1:2,7` and `1:3,7`. The stone double door, `1:6,7` and `1:7,7`, is solid too | Task 7 (line 4), Task 8 |
| The door back, or a mat, indoors | A mat: the flagstones on sand, `1:6,3` of `Tiles/Indoors`, with the kind `door` and `to` = `Town`, not solid, on `Ground` | Task 7 (line 4), Task 9 |
| Roofs, walls, trunks and crowns, paths, fences, flowers | Ground: grass `1:0,0`, with tufts `1:1,0`, with flowers `1:2,0`; a patch of earth in nine pieces, `1:0,1` to `1:2,3`, and four pieces of earth for a wide path, `1:3,3` to `1:6,3`; cobbles on grass `1:7,3`; a stone floor in nine pieces, `1:0,8` to `1:2,10`. Roofs, two rows of a left end, a middle and a right end: grey `1:0,4` to `1:2,5`, with a chimney `1:3,4` and a gable `1:3,5`; red `1:4,4` to `1:6,5`, with `1:7,4` and `1:7,5`. Walls, a left end, a middle and a right end: wood `1:0,6`, `1:1,6`, `1:3,6`, and a window `1:0,7`; stone `1:4,6`, `1:5,6`, `1:7,6`, and a window `1:4,7`. A tall tree is a crown over a trunk: green `1:4,0` over `1:4,1`, yellow `1:3,0` over `1:3,1`. Fences: the level one `1:8,6`, `1:9,6`, `1:10,6`; the upright one `1:11,3` to `1:11,5`; a pen of three by three, `1:8,3` to `1:10,5`. Mushrooms `1:5,2`. `Sample.png` in the zip is Kenney's own town made of these | Task 8 (the `tile` lines) |
| The floor, the walls and the furniture indoors | In `Tiles/Indoors`. Floor: sand `1:0,4`, with pebbles `1:1,4`, and with the shadow of a wall, `1:2,4` and `1:3,4`, for the row under the back wall. Walls, solid: bricks `1:4,3`, with a grate `1:4,2`, with a banner `1:5,2`; a wall with its two ends, `1:9,4`, `1:10,4`, `1:11,4`. Furniture, solid: a table `1:0,6`, a stool `1:1,6`, shelves `1:3,5` over `1:3,6`, a chest `1:5,7`, a stove `1:6,4`, an anvil `1:2,6`. The pack has no bed and no rug | Task 9 |
| The hero's block: the size of a figure, which side each row faces, which column stands and which step | In `Tiles/People`, the first of the six, in a green shirt: `1:0,0` to `1:2,3`, three columns by four rows of figures of 16 by 16. The rows, from the top: Down, Left, Right, Up, as `Sheet` proposes them. The columns: a step, standing, the other step. `walk` is `Sheet` over the block with `There and back`, 0.1 s a frame: two frames a cell at the hero's pace. `idle` is `Sheet` over the middle column, `1:1,0` to `1:1,3` | Task 7 (line 6) |
| The blocks of three villagers, the same way | `Villager1`: the second, in a red top, `1:3,0` to `1:5,3`. `Villager2`: the third, with grey hair, `1:6,0` to `1:8,3`. `Villager3`: the sixth, with dark hair and a headband, `1:15,0` to `1:17,3`. Their frames last 0.2 s: a villager walks a cell in 0.4 s. The fourth (a hard hat) and the fifth (bald) are to spare | Task 7 (line 8) |

## Global Constraints

- Every file of this work is inside `extensions/ColorRise.Tessera/`. Nothing else in the repository changes, and nothing in the engine.
- Stride is reached through packages only, at `$(TesseraStrideVersion)` (`4.5.4-dev`). No `ProjectReference` to an engine project, in the sample too: Game Studio must open it. The `src/` projects and the sample's game use no engine internal, by reflection or any other way.
- The sample's game references `ColorRise.Tessera` only (the runtime). Its tests also reference `ColorRise.Tessera.Assets`, to read the asset files.
- A gap in the plugin model is a row in `docs/plugin-findings.md`, in the format of the file. The next number is 21.
- All projects: `net10.0` (the Windows head `net10.0-windows`), `LangVersion` latest, `Nullable` enable, `ImplicitUsings` enable; the sample's projects are not packed (`IsPackable` false).
- Code: explicit access modifiers, classes sealed by default, the `field` keyword in place of a new private backing field, no licence header in source files, plain English in comments and documentation, `<summary>` on public types and members, `<userdoc>` on what the property grid shows. A comment says what the code is, not what it was. The sample's code is read as an example: short scripts, no cleverness.
- Commits: conventional titles with the scope `tessera`. Author `Nicolas Musset <musset.nicolas@gmail.com>` (run the two `git config` commands before the first commit). No `Co-Authored-By` line, no link to Claude, no model name anywhere.
- Branch `feature/colorrise-tessera-sample`, from `origin/feature/colorrise-tessera-flipbooks`. No other branch name, and no `-tmp` branch. Push it after each task: the container does not last.
- Nothing stays only in the conversation (Nicolas, 2026-11-24). In the commit of each task: the task's boxes ticked in `docs/plan-step4.md`, the result of its gate, a "Done differently" note under the task where what was done differs, and a row in `docs/plugin-findings.md` for each gap met. After each task, one line in the ledger (`/mnt/project-files/tilemap/2026-11-2x-tessera-step4-ledger.md`) and in the project's memory (`tilemap-plugin`). The ledger and the memory are written with the file tools, never with a shell command.
- Never read or change YAML or XML with a regular expression (the repository's rule): the tool writes the map's file as text and reads none; the tests read assets with `AssetFileSerializer`.
- Do not build Tessera while Game Studio has the sample's solution open: close Game Studio first.
- Values from the design: the town is "about 60 by 40 cells" with the three layers ground, details and overhead; the water is animated; the second map is a house interior entered through a door; the hero has idle and walk flipbooks in four directions, is blocked by solid tiles and hidden under the overhead layer; "two or three" villagers; the camera stops at the map's frame, at a whole-number zoom; the art is CC0 and listed in `THIRD-PARTY-NOTICES.md`.
- Values of this plan: the town is exactly 60 by 40, its layers are named `Ground`, `Details` and `Overhead`, bottom first; the house's are `Ground` and `Details`; a cell is one unit and 16 pixels; the hero walks a cell in 0.2 s and a villager in 0.4 s; a frame of the game counts for at most 0.1 s; a villager stays within 4 cells and stands 0.5 to 2.5 s; 3 villagers; 12 brambles; the zoom shows at least 12 cells of height; the kinds are `bush` and `door`; the properties are `cut`, `to` and `map`; the animations are `idle` and `walk`; the places are `Town` and `House`; the window is 1280 by 720.
- Tests: the sample's tests are in `Thornwick.Tests` and run with `dotnet test sample/Thornwick/Thornwick.Tests`. They read the game's files from the folder beside them, never a built database. No gold image: the picture test checks a size and a few pixels, and a person looks at the picture.
- Each test is seen to fail before the code, or the asset, that makes it pass. No mutation testing (Nicolas, 2026-11-25).
- After a failed run, run again only what failed. The whole of Tessera's solution and of the sample's tests is run at the gates named in the tasks.
- The engine build and Game Studio's build are long runs. They run in the container, in the background, with their output in a log file: no check on GitHub builds an extension.
- Screenshots of what is made and checked by hand go to `/mnt/project-files/tilemap/step4/`, named `studio-NN-*.png` after the line of the list. The pictures the guide uses are cropped copies in `docs/guide/`.
- A fallback that loses a goal of section 6 or a line of section 13 stops the work: write what was found and ask Nicolas.
- Engine names in this plan come from the fork's `master` at `659bb239e`, which the branch is built on. A name marked *(check)* was not read while planning: read the model file named beside it before relying on it.

## Review Focus

Inputs the design implies and that will reach this game, most likely first. Each has its test in the task named.

1. **A step toward the edge of the map, or onto a cell that has no tile.** A cell outside the map's frame is not solid for `IsSolid`, so a walker that only asked `IsSolid` would walk off the town into the void. Nobody steps outside the frame, the hero and the villagers alike. (Task 3)
2. **A long frame**: the window dragged, a first load, a breakpoint. A walker given seconds at once arrives on one cell at most per update, so no door and no bramble is passed without being seen, and nobody goes through a solid tile. (Tasks 3 and 10)
3. **Two walkers and one cell**: a villager on the cell where the hero would arrive through a door, the hero and a villager stepping to the same cell in the same frame, a villager walking onto a door. The second one waits, the door does not take the hero until its arrival is free, and no villager steps on a door. (Tasks 3, 4 and 6)
4. **A window of any size**: resized, as tall as 100 pixels, minimized to a height of 0, or larger than the whole town. The zoom is at least 1, nothing divides by zero, the camera stays inside the frame, and a place smaller than the view is centred. (Task 5)
5. **Tile data that is not what the game expects**: a bush whose `cut` names no tile or is not a tile id, a door whose `to` names no place or whose `map` names no map, a cut at the edge of the map or on a cell without a bush. The cell is emptied, or nothing happens, with one line in the log and no exception. (Tasks 6 and 10)

## Risks tried in this step

| # | Risk | Tried first | Fallback |
|---|---|---|---|
| R14 | Game Studio opens a solution with a Windows head beside the Linux one, on Linux | Task 2, Step 6: open the scaffold before anything is made in it; Brinemoor's solution has the same shape | The Windows head stays in the folder and out of `Thornwick.slnx`, with a line in the sample's README and a finding |
| R15 | The map file the tool writes is the file Game Studio would write: a stamp and a save in the map editor change one line, and nothing else | Task 8, Step 7, by hand | The tool is changed to write what Game Studio wrote; if a difference cannot be matched, the saved file is committed and the tool's README line says the first save reorders it |
| R16 | The keys `xdotool` sends reach the game's window under Xvfb, held for a time | Task 11, Step 1 | The game reads its keys from the environment variable `THORNWICK_KEYS` when it is set (a list of keys and times), through the same `HeroKeys` the keyboard goes through; written in the sample's README as a way to record a run |
| R17 | The scripts of the game show in *Add component* of the cross-platform Game Studio (seen in step 3 for a script without properties), their properties show in the property grid, and a property that is an entity (`FollowCamera.Target`) can be set there | Task 10, line 12 | What the property grid cannot set is written into the scene's file by hand, as text, and the line of the check says so; a finding |
| R18 | A map loaded by URL from a tile's property is in the built game: it needs to be a root asset | Task 10, line 16: *Include in build as root asset* in the asset view; then Task 11, line 23, where the hero goes in | The line is written in the `.sdpkg` by hand (Brinemoor's has one) |
| R19 | A whole-number zoom and a camera on whole pixels give tiles of equal size without a seam or a shimmer while the camera follows | Task 11, by pictures taken while walking | Nothing is lost: Tessera's batch already closes the seams; a shimmer is written in the report |
| R20 | The licence of a pack is not what the brief says | Task 2, Step 1: read `License.txt` of each zip | Stop and ask Nicolas |

## The pieces

What the tasks build, and who knows whom. The names are fixed here so that every task uses the same ones. All of the game's types are in the namespace `Thornwick`.

| Piece | Where | What it is |
|---|---|---|
| `Place` | `Thornwick.Game` | A map shown in the scene, its frame, and who stands on which cell |
| `GridWalker` | `Thornwick.Game` | Something that stands on a cell of a place or walks to a neighbour |
| `WanderPlan` | `Thornwick.Game` | Where a villager steps next, and how long it stands |
| `CameraFraming`, `CameraFrame` | `Thornwick.Game` | Where the camera looks and how much it shows |
| `TileKinds`, `TileProperties`, `Brambles`, `Doors`, `Door`, `DoorResult` | `Thornwick.Game` | What the game reads from tile data, and the taking of a door |
| `HeroKeys`, `FrameTime` | `Thornwick.Game` | Which way the pressed keys ask for, and how long a frame counts for |
| `World`, `PlayerController`, `Wanderer`, `FollowCamera` | `Thornwick.Game` | The four scripts: the places and the count of the brambles, the hero, a villager, the camera |
| `Tiles/Town`, `Tiles/Indoors`, `Tiles/People` | assets | The tile sets: the town (Tiny Town's sheet, and the pond), the house (Tiny Dungeon's sheet), the people |
| `Maps/Town`, `Maps/House` | assets | The two maps |
| `Flipbooks/Hero`, `Flipbooks/Villager1` to `3` | assets | The flipbooks |
| `MainScene`, `GraphicsCompositor`, `GameSettings` | assets | The scene, the compositor without post effects, the settings |
| `tools/MakeMaps.cs`, `tools/maps/Town.txt` | `sample/Thornwick` | The tool and the town's text pictures |
| `SampleFiles`, `SampleAssets`, `Reach`, `MapPicture` | `Thornwick.Tests` | What the tests read the game's files with |

Paths in the tasks are from `extensions/ColorRise.Tessera/` unless they start with `/`. `T/` stands for `sample/Thornwick/`.

---

### Task 0: The container, the branch, the art and the plan

**Files:**
- Create: `docs/plan-step4.md` (a copy of this plan), `/mnt/project-files/tilemap/2026-11-2x-tessera-step4-ledger.md`

**Interfaces:**
- Produces: `dotnet` 10 on the `PATH`; the worktree `/home/user/stride-master` on the branch `feature/colorrise-tessera-sample`; Stride's packages at `4.5.4-dev` in `/home/user/stride-master/bin/packages`; step 3's tests green; the Avalonia Game Studio built; `Xvfb`, `xdotool` and ImageMagick installed; the three zips of the art in `/mnt/project-files/tilemap/art/`.

- [x] **Step 1: Set up the container as Task 0 of [plan-step2.md](plan-step2.md) says** (its steps 1 to 4, with the notes of what was done): the .NET 10 SDK in `~/.dotnet`; the worktree, this time `git worktree add -b feature/colorrise-tessera-sample /home/user/stride-master origin/feature/colorrise-tessera-flipbooks`; the author's two `git config` lines; `git lfs pull` and the restore of the one file it stages outside LFS; the three builds in the background with logs (the engine and the editor, the asset compiler, Game Studio). Build in this worktree before any other checkout of the container: the first checkout that builds gets the version `4.5.4-dev`, and a later one `4.5.4-dev2`. If `bin/packages` has `-dev2`, set `TesseraStrideVersion` in `Directory.Build.local.props`, which git ignores, and never in a committed file.
- [x] **Step 2: See step 3's tests pass** before anything changes.

Run: `dotnet test extensions/ColorRise.Tessera/ColorRise.Tessera.slnx`, then `extensions/ColorRise.Tessera/tests/PackagesCheck/run.sh`
Expected: PASS, 843 tests (410, 364 and 69), none skipped, then `TESSERA-PACKAGES-CHECK OK`.

- [x] **Step 3: See the art.** `ls /mnt/project-files/tilemap/art/` lists the three zips this plan's section "The art" names, and `check/` with the planner's pictures and scripts. If one is missing, stop and ask Nicolas for it: kenney.nl cannot be reached from the container.
- [x] **Step 4: Copy this plan** to `extensions/ColorRise.Tessera/docs/plan-step4.md`, start the ledger with Nicolas's answers to decisions 1 to 3, commit (`docs(tessera): the plan of step 4`) and push with `git push -u origin feature/colorrise-tessera-sample`.
- [x] **Step 5: Gate.** If the engine or Game Studio cannot be built in the container, stop and tell Nicolas what failed, with the end of the log.

**Done (2026-11-26).** .NET SDK 10.0.404. The engine and editor built in 4 min 18 s, the asset compiler in 1 min 19 s, Game Studio in 42 s, all without an error; `bin/packages` has `4.5.4-dev`. The baseline: 410, 364 and 69 tests pass, none skipped, and `TESSERA-PACKAGES-CHECK OK`. The three zips are in `art/`, their sha256 as the table of "The art" says. xdotool was installed with apt; Xvfb and ImageMagick 6.9.12-98 were there.

**Done differently.** As in step 2, `git lfs pull` staged `TestBrokenImage.txt`, which was restored. The plan was committed and pushed before the builds, which showed early that the push works.

### Task 1: Four deferred fixes of the flipbook editor (decision 3)

The four Nicolas picked, numbered as in step 3's report. Each is a fix with a test that fails first, in the file that owns the code. Whoever follows the guide meets them in the flipbook editor.

**Files:**
- Modify: `src/ColorRise.Tessera.Editor/FlipbookEditor/FlipbookEditorViewModel.cs` (m2, m3), `FlipbookEditor/FlipbookGraph.cs` (m3), `src/ColorRise.Tessera.Editor/TesseraCommands.cs`, where `Tessera.Flipbook.Mirror` is registered (m3), `src/ColorRise.Tessera.Editor.Avalonia/Views/FlipbookEditorView.axaml` and `.axaml.cs` (m3, m4), `src/ColorRise.Tessera.Assets/FlipbookAssetChecks.cs` (m6), `README.md` (the line of "Not yet" about a frame's tile goes)
- Test: `tests/ColorRise.Tessera.Editor.Tests/FlipbookEditorTests.cs`, `tests/ColorRise.Tessera.Editor.Avalonia.Tests/FlipbookEditorViewTests.cs`, `tests/ColorRise.Tessera.Tests/Assets/FlipbookAssetChecksTests.cs`

**Interfaces:**
- Produces:

```csharp
// FlipbookEditorViewModel
/// While on, a tile picked in the palette becomes the tile of the selected frames instead of a new frame.
public bool ReplacesTile { get; set; }
/// Sets the tile of the first cell of each selected frame, in one step named "Change frame tile"; turns ReplacesTile off.
public void SetFramesTile(TileId tile);
public ICommandBase ReplaceTileCommand { get; }     // toggles ReplacesTile; can run when a frame is selected

// FlipbookGraph
public static void SetCellTile(FlipbookViewModel flipbook, FlipbookCell cell, TileId tile);

// The seventh command of the flipbook editor, with no default shortcut
"Tessera.Flipbook.ReplaceTile"   // "Replace the tile of the selected frames"
```

- [x] **Step 1: Write the failing tests**

```csharp
// m2, FlipbookEditorTests
[Fact] public async Task A_pick_in_the_palette_on_an_animation_without_a_clip_says_to_add_a_clip()
    // an animation whose last clip was removed with -, selected: AddFrames(stamp of one tile) adds nothing and
    // Message is "Add a clip to the animation first, with + Direction."
    // with no animation at all, Message stays "Select or add an animation first."

// m3, FlipbookEditorTests
[Fact] public async Task A_pick_while_replacing_changes_the_tile_of_the_selected_frames()
    // walk/Down of three frames, frames 1 and 3 selected, ReplacesTile on, AddFrames(stamp of the tile 1:2,3):
    // the clip still has three frames; the first cell of frames 1 and 3 has the tile 1:2,3, with its offset, its flip and
    // the frame's duration as before; frame 2 is as before; the two stay selected; ReplacesTile is off;
    // the step is named "Change frame tile"; one undo brings the two tiles back
[Fact] public async Task A_pick_of_several_tiles_while_replacing_is_refused()
    // a stamp of 2 by 1 tiles: nothing changes, Message is "Pick one tile to replace with.", ReplacesTile stays on
[Fact] public async Task Replacing_needs_a_selected_frame()
    // no frame selected: ReplaceTileCommand cannot run; a frame selected: it can; the selection emptied while
    // ReplacesTile is on turns it off

// m3 and m4, FlipbookEditorViewTests
[AvaloniaFact] public void The_replace_button_follows_the_view_model()
    // the toggle button "Replace" above the palette is checked when ReplacesTile is on, and a click in the palette
    // then changes the selected frame's tile and unchecks it
[AvaloniaFact] public void Delete_removes_a_frame_after_a_click_on_a_button_of_the_strip()
    // three frames, the second selected; a click on ">" (the frame is now third), then the key Delete on the
    // focused element: the clip has two frames

// m6, FlipbookAssetChecksTests
[Fact] public void A_clip_whose_frames_all_last_nothing_is_told()
    // walk/Left with two frames of 0 s and -1 s: Of(asset) has
    // "No frame of the clip Left of 'walk' lasts above 0 s: it shows nothing that moves."
    // one frame of 0.1 s among them: no such line
```

- [x] **Step 2: Run them to see them fail.** Run each test project with a filter on the seven names. Expected: FAIL, each for its reason (`ReplacesTile` does not compile, the message is the old one, Delete removes nothing, the line is missing).
- [x] **Step 3: Write the fixes.** m2: the message depends on whether an animation is selected. m3: `AddFrames` goes to `SetFramesTile` when `ReplacesTile` is on and the stamp has one tile; the comment of `ShowsMember` stays true. m4: the three buttons of the strip give the focus back to the strip, as the tool bar of the map editor gives it back to its canvas. m6: the line is added after "has no frame", for a clip that has frames.
- [x] **Step 4: Run the tests of the three files.** Expected: PASS, with step 3's.
- [x] **Step 5: Commit and push**: `fix(tessera): four deferred minors of the flipbook editor`

**Done (2026-11-26).** The seven tests failed first, each for its reason: with `ReplacesTile` and `ReplaceTileCommand` as bare stubs (so that the project compiled and each test could fail on its own), the message was the old one, `ReplaceTileCommand` was null, no tile changed, Delete removed nothing, the button was not found, and the line of m6 was missing. Then they passed, with the whole solution: 411, 368 and 71 tests.

**Done differently.**
- The test of m3 replaces with the tile `1:3,1`, not `1:2,3`: the tile set of the editor's tests has 4 by 2 tiles, and a tile it does not have would add a line to `Problems`. The frames are given an offset, a flip and a duration of their own before, so that the test sees each kept; the stamp's flip is not taken, only its tile.
- `Tessera.Flipbook.ReplaceTile` makes 24 commands: `TesseraCommandsTests` checks its id, title, the toggle and that it can run only with a frame selected.
- The toggle `Replace` is on the row of the tile set's name, left of `Change`, above the palette. It is a `ToggleButton` whose `Command` is `ReplaceTileCommand` and whose `IsChecked` follows `ReplacesTile` one way, so the command decides when it is enabled. The view test reads `IsEffectivelyEnabled`, since a command does not change a button's own `IsEnabled`.
- m4: the strip (`ListBox`) is made focusable, and the buttons of its bar give it the focus; the other buttons still give it to the preview. The test of step 3 that saw the preview take the focus after `>` now sees the strip have it.
- `SetFramesTile` leaves a frame without a cell as it is, and is no step when no tile changes.

### Task 2: The sample's scaffold, and the art

A game that builds, runs and shows an empty scene, in the layout of Brinemoor. The model of every file is the check game, `tests/StudioCheck`, whose projects already have this shape, and `sample/Brinemoor` on `origin/feature/colorrise-skein` for the Windows head and the tests (`git show origin/feature/colorrise-skein:extensions/ColorRise.Skein/sample/Brinemoor/<file>`).

**Files:**
- Create: `T/.gitignore`, `T/README.md`, `T/Thornwick.slnx`
- Create: `T/Thornwick.Game/Thornwick.Game.csproj`, `Thornwick.Game.sdpkg`, `Assets/GameSettings.sdgamesettings`, `Assets/GraphicsCompositor.sdgfxcomp`, `Assets/MainScene.sdscene`
- Create: `T/Thornwick.Linux/Thornwick.Linux.csproj`, `ThornwickApp.cs`; `T/Thornwick.Windows/Thornwick.Windows.csproj`, `ThornwickApp.cs`
- Create: `T/Thornwick.Tests/Thornwick.Tests.csproj`, `SampleFiles.cs`, `TestAssemblies.cs`, `ScaffoldTests.cs`
- Create: `T/Thornwick.Game/Resources/Kenney/TinyTown/`, `TinyDungeon/` and `RpgUrbanPack/`, each with the pack's `tilemap_packed.png` and `License.txt` (the table of files of "The art")
- Modify: `THIRD-PARTY-NOTICES.md`

**Interfaces:**
- Produces:

```csharp
// Thornwick.Tests
internal static class SampleFiles
{
    /// The folder of the game library, T/Thornwick.Game, found from the folder the tests run in.
    public static string Game { get; }
    /// The file of an asset of the game: Asset("Maps/Town", ".sdtilemap").
    public static string Asset(string url, string extension);
    /// A file of the game's resources: Resource("Kenney/TinyTown/tilemap_packed.png").
    public static string Resource(string path);
}
```

- `Thornwick.slnx`: the folder `/Tessera/` with Tessera's four `src` projects, then `Thornwick.Game`, `Thornwick.Linux` (`DefaultStartup="true"`), `Thornwick.Tests`, `Thornwick.Windows`, as `Brinemoor.slnx`.
- `Thornwick.Game.csproj`: `RootNamespace` `Thornwick`, `IsPackable` false, `PackageReference` to `Stride.Engine` and to `Stride.AssetCompiler` (`IncludeAssets="build;buildTransitive"`) at `$(TesseraStrideVersion)`, `ProjectReference` to `src/ColorRise.Tessera`.
- `Thornwick.Tests.csproj`: xunit 2.9.3 as Brinemoor's, `ProjectReference` to the game and to `src/ColorRise.Tessera.Assets`. `TestAssemblies` registers Tessera's two assemblies with `AssemblyRegistry`, as `tests/ColorRise.Tessera.Tests/TestAssemblies.cs` does.
- The three assets are copies of the check game's with new ids (`uuidgen`), and the references between them changed to the new ids. The scene keeps only the camera. The compositor is the check game's **without** Tessera's two features and with Stride's `BackToFrontSortMode` on the transparent stage: Task 10 adds the features and the sort mode in Game Studio, as a user does. The settings: 1280 by 720, the scene and the compositor above.

- [x] **Step 1: Read the licence of each pack** in its zip (`unzip -p <zip> License.txt`, or the name the zip gives it). Expected: Creative Commons Zero (CC0), for each. Anything else: stop and ask Nicolas (R20). Copy the text of each to the ledger.
- [x] **Step 2: Write the failing test**

```csharp
// ScaffoldTests
[Fact] public void The_game_is_beside_the_tests()
    // File.Exists(Path.Combine(SampleFiles.Game, "Thornwick.Game.sdpkg")), and of SampleFiles.Asset("MainScene", ".sdscene")
[Fact] public void The_art_comes_with_its_licence()
    // every folder under SampleFiles.Resource("Kenney") has a License.txt that contains "Creative Commons Zero"
    // and at least one .png, and THIRD-PARTY-NOTICES.md (two folders above the sample) names the folder's pack
```

- [x] **Step 3: Run it to see it fail.** Run: `dotnet test sample/Thornwick/Thornwick.Tests`. Expected: FAIL (the project does not exist, then the files).
- [x] **Step 4: Write the scaffold and bring the art in.** From each zip, only the sheet without spacing (`Tilemap/tilemap_packed.png`) and the licence, to the folders of "The art"; the two pictures the scripts make come in Task 7. `THIRD-PARTY-NOTICES.md` loses "will use" and gains, for each pack: its name, "by Kenney (www.kenney.nl)", "Creative Commons Zero (CC0 1.0)", the files taken and where they are.
- [x] **Step 5: Build, test and run.**

Run: `dotnet test sample/Thornwick/Thornwick.Tests`, then `dotnet build sample/Thornwick/Thornwick.Linux`, then the game under Xvfb as the check game's README says (`dotnet run` in `Thornwick.Linux`), for 5 seconds.
Expected: PASS (2 tests); the build compiles the assets; the game opens a window of the clear colour and its log has no error.

- [x] **Step 6: Open it in Game Studio (R14).** `Stride.GameStudio.Avalonia.Desktop sample/Thornwick/Thornwick.slnx` under Xvfb: the solution loads, *View > Plugins* lists Tessera's two editor plugins, the asset view shows the three assets, and the log has no error about the Windows head. Picture: `studio-01-opens.png`. If the Windows head stops the load, take R14's fallback.
- [x] **Step 7: Commit and push**: `feat(tessera): the scaffold of the sample game, Thornwick, and its art`

**Done (2026-11-26).** Each zip's `License.txt` says Creative Commons Zero (CC0); its text is in the ledger, and the two files taken from each zip are its bytes (`cmp`). The two tests failed first (no `Resources/Kenney`, no `.sdpkg`), then passed. The Linux head builds and compiles the assets (`Bin/Linux/Debug/data/db`); its only warnings are the NU1902 and NU1903 of Stride's own packages, as in the check game. The game ran 12 seconds under Xvfb with Lavapipe: a window of 1280 by 720 in the clear colour, and a log of four `Info` lines. Game Studio opened `Thornwick.slnx`: the asset view shows the three assets, *Asset errors* is at 0, the Output is empty, the solution explorer lists `Thornwick.Windows` with the other projects, and *View > Plugins* lists `ColorRise.Tessera.Editor` and `ColorRise.Tessera.Editor.Avalonia`, both on. R14 holds: no fallback. Pictures: `studio-01-opens.png` (the asset view) and `studio-01-opens-plugins.png` (the plugins). Closing Game Studio changed no file.

**Done differently.**
- The scene's camera is Stride's default, perspective, and not the check game's orthographic camera: Task 10 (line 15) sets it in Game Studio, as a user does.
- The compositor is the check game's without Tessera's two features, with `BackToFrontSortMode` on the transparent stage. The ids inside it (its stages, the camera slot, its renderers) are kept, because they come from Stride's archetype `DefaultGraphicsCompositorLevel10`, which the file overrides: only the asset's own id is new. Its post effects are still there; Task 10 takes them out in Game Studio.
- The art is in Git LFS, as every `.png` of the repository is (`.gitattributes`).

### Task 3: Places, and walking on a grid

The two classes every other rule stands on. No art and no Game Studio: the tests make their maps in code.

**Files:**
- Create: `T/Thornwick.Game/Place.cs`, `T/Thornwick.Game/GridWalker.cs`
- Test: `T/Thornwick.Tests/TestMaps.cs`, `PlaceTests.cs`, `GridWalkerTests.cs`

**Interfaces:**
- Consumes: `TileMapComponent.IsSolid`, `TileMap.Size`, `TileMap.CellSize`, `TileMapComponent.LayerSpacing`, `TileMap.Layers`.
- Produces:

```csharp
/// <summary>A map shown in the scene, and who stands on which of its cells.</summary>
public sealed class Place
{
    public const string OverheadLayer = "Overhead";

    public Place(string name, TileMapComponent component);
    public string Name { get; }
    public TileMapComponent Component { get; }   // its Map is the place's map, and can be given later

    /// Whether a cell is inside the map's frame. Every cell is, while the component has no map or the map has no frame.
    public bool InFrame(Int2 cell);
    /// Whether a walker can step on a cell: inside the frame, not solid, and no walker stands on it or walks to it.
    public bool IsFree(Int2 cell);
    /// The frame in world units, X and Y its bottom-left corner; null without a frame.
    public RectangleF? Frame { get; }
    /// The z, in the map's space, of what walks here: half a layer spacing under the layer "Overhead" when the map has
    /// one, else half a spacing under where one more layer would be.
    public float WalkerZ { get; }
    /// The point of the world of a position in cells, at WalkerZ. The bottom centre of the cell (x, y) is (x + 0.5, y).
    public Vector3 ToWorld(Vector2 cells);

    internal bool TryHold(Int2 cell, GridWalker walker);   // false when another walker holds it
    internal void Release(Int2 cell, GridWalker walker);
}

/// <summary>Something that stands on a cell of a place, or walks to a neighbour.</summary>
public sealed class GridWalker
{
    /// <exception cref="InvalidOperationException">The cell is not free.</exception>
    public GridWalker(Place place, Int2 cell, FlipbookDirection facing = FlipbookDirection.Down);

    public Place Place { get; }
    public Int2 Cell { get; }            // where it stands, or the cell it is leaving
    public Int2 Target { get; }          // the cell it walks to; Cell while it stands
    public FlipbookDirection Facing { get; }
    public bool IsMoving { get; }
    public Int2 Ahead { get; }           // the neighbour of Cell that it faces
    public float StepSeconds { get; set; } = 0.2f;
    /// In cells: the bottom centre of Cell, moved toward the bottom centre of Target by the part of the step done.
    public Vector2 Position { get; }

    /// Turns to a side. Standing, it also starts a step when the next cell is free, and holds that cell. False when no
    /// step starts. Only Down, Left, Right and Up are sides.
    public bool TryStep(FlipbookDirection side);
    /// Moves along the step. True in the call that arrives: Cell is then the target, the cell left is released, and the
    /// time not used, one step's time at most, is already walked if TryStep is called before the next Advance.
    public bool Advance(float seconds);
    /// Standing, goes to a free cell of a place, the same or another, at once. False, with nothing changed, when it
    /// walks or the cell is not free.
    public bool TryTravel(Place place, Int2 cell, FlipbookDirection facing);

    /// (0, -1) for Down, (0, 1) for Up, (-1, 0) for Left, (1, 0) for Right, (0, 0) for the others.
    public static Int2 Offset(FlipbookDirection side);
}
```

```csharp
// Thornwick.Tests/TestMaps.cs: maps made in code, over one tile set without textures
internal static class TestMaps
{
    public static readonly TileId Floor = TileId.Parse("1:0,0");   // not solid
    public static readonly TileId Wall = TileId.Parse("1:1,0");    // solid
    public static readonly TileId Bush = TileId.Parse("1:2,0");    // solid, kind "bush", cut = "1:3,0"
    public static readonly TileId Stump = TileId.Parse("1:3,0");   // not solid
    public static readonly TileId Door = TileId.Parse("1:4,0");    // kind "door", to = "House", map = "Maps/House"
    public static readonly TileId Mat = TileId.Parse("1:5,0");     // kind "door", to = "Town"
    public static readonly TileId Hedge = TileId.Parse("1:6,0");   // solid, kind "bush", no property

    /// A map with a frame of that size, the layers Ground (floor everywhere) and Details (empty), and the tile set above.
    public static TileMap Room(int width, int height);
    public static TileSet Tiles(TileMap map);
    /// A place over a component with that map; with an entity at a position when one is given.
    public static Place Place(TileMap map, string name = "Town", Vector3? at = null);
    public static void Put(TileMap map, TileId tile, params Int2[] cells);   // on Details
}
```

- [x] **Step 1: Write the failing tests**

```csharp
// PlaceTests
[Fact] public void A_cell_outside_the_frame_is_not_free()
    // Room(4, 3): (0, 0) and (3, 2) are free and in the frame; (-1, 0), (4, 0), (0, -1) and (0, 3) are neither
[Fact] public void A_map_without_a_frame_has_every_cell_in_its_frame()
    // Size (0, 0): InFrame((1000, -1000)); a component without a map: InFrame and IsFree of any cell
[Fact] public void A_solid_cell_is_not_free()
    // a wall on Details at (1, 1): not free; in the frame
[Fact] public void The_frame_is_the_maps_size_from_where_its_entity_is()
    // Room(12, 9) on an entity at (100, 0, 0), its world matrix updated: Frame is (100, 0, 12, 9);
    // without an entity (0, 0, 12, 9); a cell size of (2, 2): (0, 0, 24, 18); no frame: null
[Fact] public void What_walks_is_under_the_overhead_layer_or_over_every_layer()
    // layer spacing 0.1. Ground, Details, Overhead: 0.15. Ground, Details: 0.15. Ground: 0.05.
    // Overhead, Ground (overhead at the bottom): -0.05. No map: 0
[Fact] public void A_position_in_cells_is_a_point_of_the_world()
    // the place on an entity at (100, 0, 0): ToWorld((2.5, 1)) is (102.5, 1, WalkerZ)

// GridWalkerTests, in Room(20, 3) unless said
[Fact] public void A_new_walker_holds_its_cell()
    // place.IsFree(cell) is false after; a second walker on the same cell throws InvalidOperationException
[Fact] public void A_step_to_a_free_cell_starts_and_holds_both_cells()
[Fact] public void A_step_against_a_wall_only_turns()
    // a wall at (1, 1), a walker at (0, 1) facing Down: TryStep(Right) is false, Facing is Right, IsMoving is false
[Fact] public void A_step_ends_on_the_next_cell_after_its_time()
    // StepSeconds 0.2 from (0, 1): Advance(0.1f) is false and Position is (1, 1) (halfway from (0.5, 1) to (1.5, 1));
    // Advance(0.1f) is true: Cell (1, 1), not moving, Position (1.5, 1), and (0, 1) is free again
[Theory] [InlineData(1 / 60f)] [InlineData(1 / 30f)] [InlineData(0.07f)]
public void A_walker_that_keeps_walking_loses_no_time_between_cells(float frame)
    // for (int)MathF.Round(2.05f / frame) frames: Advance(frame), then TryStep(Right) when it stands.
    // Cell.X is 10: ten cells in two seconds at 0.2 s a cell
[Fact] public void A_long_frame_arrives_on_one_cell()
    // a step started, Advance(5): true, Cell.X is 1 and not more. TryStep(Right), Advance(0): true, Cell.X is 2
    // (one step's time was kept). TryStep(Right), Advance(0): false
[Fact] public void Two_walkers_never_share_a_cell()
    // A at (0, 1) steps Right; B at (2, 1): TryStep(Left) is false while A walks and after it arrived on (1, 1);
    // once A arrived, a walker can step onto (0, 1)
[Fact] public void A_walker_travels_to_a_free_cell_of_another_place()
    // TryTravel(house, (2, 1), Up): true; Place is the house, Cell (2, 1), Facing Up; its old cell is free in the town
    // and (2, 1) is held in the house. To a held cell, a solid cell or a cell outside the frame: false, nothing changed.
    // While it walks: false
[Fact] public void A_step_time_that_is_zero_or_not_a_number_arrives_at_once()
    // StepSeconds 0, then float.NaN: TryStep(Right), Advance(0.01f) is true, and Position has no NaN
[Fact] public void Time_that_is_negative_or_not_a_number_moves_nothing()
[Fact] public void Only_the_four_sides_are_steps()
    // TryStep(None) and TryStep(UpLeft): false, Facing unchanged
```

- [x] **Step 2: Run them to see them fail.** Run: `dotnet test sample/Thornwick/Thornwick.Tests --filter "FullyQualifiedName~PlaceTests|FullyQualifiedName~GridWalkerTests"`. Expected: FAIL to compile (`Place` and `GridWalker` do not exist).
- [x] **Step 3: Write `Place` and `GridWalker`.** A place keeps who holds each cell in a dictionary. A walker holds its cell, and from the start of a step to its end the target too.
- [x] **Step 4: Run the tests.** Expected: PASS.
- [x] **Step 5: Commit and push**: `feat(tessera): Thornwick's places and its walk on the grid`

**Done (2026-11-26).** The tests failed first to compile (`Place` did not exist), then the sample's 21 tests passed: the 2 of Task 2, 6 of `PlaceTests` and 13 of `GridWalkerTests` (the theory counts three).

**Done differently.**
- A walker that is already walking does not turn: `TryStep` returns false and leaves `Facing` as it is. The interface left it open; the hero's key is read again when it arrives, so nothing is lost.
- A step time that is infinite counts as 0, as one that is not a number does: a walker never stays between two cells.
- `Frame` takes its far corner from `CellToWorld(Size)` instead of multiplying the size, so that a scaled entity has the right frame too.
- `A_walker_that_keeps_walking_loses_no_time_between_cells` starts its first step before the loop, so that ten cells are walked in the 2.05 seconds.

### Task 4: Wandering

**Files:**
- Create: `T/Thornwick.Game/WanderPlan.cs`
- Test: `T/Thornwick.Tests/WanderPlanTests.cs`

**Interfaces:**
- Consumes: `GridWalker`, `Place.IsFree` (Task 3).
- Produces:

```csharp
/// <summary>Where a villager steps next, and how long it stands between two steps.</summary>
public sealed class WanderPlan
{
    public WanderPlan(Int2 home, int radius, Random random);
    public Int2 Home { get; }
    public int Radius { get; }
    public float MinPause { get; set; } = 0.5f;
    public float MaxPause { get; set; } = 2.5f;

    /// A side to step to from a cell: one of the free neighbours no further from Home than Radius (the larger of the
    /// distances along X and Y), each as likely; null when there is none.
    public FlipbookDirection? Next(Int2 cell, Func<Int2, bool> isFree);
    /// How long to stand before the next step: between MinPause and MaxPause; MinPause when MaxPause is under it.
    public float NextPause();
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void It_steps_to_a_free_neighbour_within_its_radius()
    // home (4, 4), radius 1, every cell free: from (5, 4), 200 draws never give Right, and give Left, Up and Down
[Fact] public void It_stays_when_no_neighbour_is_free()
[Fact] public void The_same_seed_gives_the_same_walk()
    // two plans over new Random(7): 50 draws of Next and NextPause are equal
[Fact] public void It_stands_between_its_two_pauses()
    // 200 draws in [0.5, 2.5]; MinPause 3 and MaxPause 1: every draw is 3
[Fact] public void A_thousand_steps_stay_within_the_radius()
    // a walker in Room(21, 21), home (10, 10), radius 4, StepSeconds 0.4: a thousand times Next, TryStep and
    // Advance(0.4f); it was never further than 4 cells from home, and it moved at least 500 times
```

- [ ] **Step 2: Run them to see them fail.** Expected: FAIL to compile.
- [ ] **Step 3: Write `WanderPlan`.** The sides are tried in the order Down, Left, Right, Up, so that a seed gives one walk.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit and push**: `feat(tessera): Thornwick's villagers choose their steps`

### Task 5: The camera's frame

**Files:**
- Create: `T/Thornwick.Game/CameraFraming.cs`
- Test: `T/Thornwick.Tests/CameraFramingTests.cs`

**Interfaces:**
- Produces:

```csharp
/// <summary>Where an orthographic camera looks, and how much it shows.</summary>
/// <param name="Center">The point of the world at the middle of the view.</param>
/// <param name="OrthographicSize">The height of the view in world units, for CameraComponent.OrthographicSize.</param>
/// <param name="Zoom">How many screen pixels one pixel of a tile takes, a whole number.</param>
public readonly record struct CameraFrame(Vector2 Center, float OrthographicSize, int Zoom);

public static class CameraFraming
{
    public const float CellsHigh = 12;

    /// The largest whole zoom at which a view of that height still shows CellsHigh units; at least 1.
    public static int ZoomFor(int viewHeight, float pixelsPerUnit);
    /// The view that follows a target: inside the frame when there is one, centred on the frame along an axis where the
    /// frame is smaller than the view, and with its bottom-left corner on a whole pixel of the screen.
    public static CameraFrame Fit(Vector2 target, RectangleF? frame, Int2 viewPixels, float pixelsPerUnit);
}
```

The numbers: `Zoom` is the whole part of `viewHeight / (pixelsPerUnit * CellsHigh)`, at least 1. `OrthographicSize` is `viewHeight / (pixelsPerUnit * Zoom)`, and the view is `viewPixels / (pixelsPerUnit * Zoom)` units. A view height under 1 counts as 1, a width under 1 as 1, and a pixels per unit that is not a number above 0 as 16.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory] [InlineData(720, 3)] [InlineData(1080, 5)] [InlineData(480, 2)] [InlineData(192, 1)] [InlineData(100, 1)]
[InlineData(0, 1)] [InlineData(-5, 1)] [InlineData(2160, 11)]
public void The_zoom_is_the_largest_whole_number_that_shows_twelve_cells(int height, int zoom)   // pixels per unit 16
[Fact] public void It_follows_the_target_inside_the_frame()
    // frame (0, 0, 60, 40), view 1280 by 720, 16 pixels a unit, target (30, 20): Center (30, 20), OrthographicSize 15, Zoom 3
[Fact] public void It_stops_at_the_frame()
    // target (1, 1): Center (13.3333, 7.5); target (59, 39): Center (46.6667, 32.5), within 0.001
[Fact] public void A_place_smaller_than_the_view_is_centred()
    // frame (100, 0, 12, 9), any target: Center (106, 4.5)
[Fact] public void Without_a_frame_it_follows_the_target()
[Fact] public void The_view_starts_on_a_whole_pixel()
    // target (30.0101, 20.0077): (Center - view / 2) * 48 is a whole number on X and on Y, within 0.001
[Fact] public void A_view_without_height_or_a_pixels_per_unit_of_zero_gives_finite_numbers()
    // viewPixels (0, 0); (1280, 0); pixels per unit 0, -1 and NaN: Zoom is at least 1, OrthographicSize is finite and
    // above 0, Center is finite
```

- [ ] **Step 2: Run them to see them fail.** Expected: FAIL to compile.
- [ ] **Step 3: Write `CameraFraming`.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit and push**: `feat(tessera): Thornwick's camera frame, at a whole zoom`

### Task 6: Brambles and doors, read from tile data

**Files:**
- Create: `T/Thornwick.Game/TileKinds.cs` (with `TileProperties`), `T/Thornwick.Game/Brambles.cs`, `T/Thornwick.Game/Doors.cs`
- Test: `T/Thornwick.Tests/BramblesTests.cs`, `DoorsTests.cs`

**Interfaces:**
- Consumes: `Place`, `GridWalker` (Task 3); `TileMap.GetTile`, `Tile.Kind`, `Tile.Properties`, `TileLayer.GetCell`, `SetCell`, `ClearCell`, `TileLayer.Chunks`, `TileChunk.Position` and `Cells`, `TileId.TryParse`.
- Produces:

```csharp
public static class TileKinds { public const string Bush = "bush"; public const string Door = "door"; }
public static class TileProperties { public const string Cut = "cut"; public const string To = "to"; public const string Map = "map"; }

/// <summary>The bushes the hero cuts.</summary>
public static class Brambles
{
    /// Cuts the bush of a cell. The topmost layer whose tile there has the kind "bush" gets, in that cell, the tile the
    /// string property "cut" names, of the same tile set and with the same flip; the cell is emptied when the property
    /// is missing, is no tile id, or names a tile the set does not have. False when the cell has no bush.
    public static bool TryCut(TileMap map, Int2 cell);
    /// How many cells of the map hold a bush.
    public static int Count(TileMap map);
}

/// <summary>A door of a map: its cell, the name of the place it leads to, and the URL of that place's map if it says one.</summary>
public readonly record struct Door(Int2 Cell, string To, string? Map);

public static class Doors
{
    /// The door of a cell: its topmost tile of the kind "door" whose property "to" is a string that is not empty; null
    /// when there is none.
    public static Door? At(TileMap map, Int2 cell);
    /// Every door of a map, the rows from the bottom, each row from the left.
    public static IReadOnlyList<Door> Of(TileMap map);
    /// Where a walker that comes from another place arrives: beside the first door, in the order of Of, that leads
    /// back there, on its first free neighbour among down, up, left and right, facing away from the door. Null when
    /// the place has no such door, or none of them has a free neighbour.
    public static (Int2 Cell, FlipbookDirection Facing)? Arrival(Place place, string from);
    /// Whether a villager may step on a cell: the cell is free, and has no door.
    public static bool IsOpenGround(Place place, Int2 cell);
    /// Takes the door a walker stands on. find gives the place of a name, or null. load gives the map of a URL, or
    /// null; it is asked only when the place the door leads to has no map yet, and that map is then given to the
    /// place's component. The walker arrives where Arrival says. problem is given with Wrong only.
    public static DoorResult TryTake(GridWalker walker, Func<string, Place?> find, Func<string, TileMap?> load, out string? problem);
}

/// <summary>What became of a walker that asked for the door of its cell.</summary>
public enum DoorResult
{
    None,    // its cell has no door, or it walks
    Taken,   // it is now in the other place, beside the door that leads back
    Waits,   // the door is right, and where it would arrive is not free now
    Wrong,   // the door leads nowhere: the place is not found, it has no map and none could be loaded, or it has no door back
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
// BramblesTests, over TestMaps
[Fact] public void A_cut_bush_becomes_the_tile_its_property_names()
    // a bush at (2, 1), flipped horizontally: TryCut is true; the cell of Details is the stump, in the same slot,
    // flipped horizontally; the place's IsFree((2, 1)) is now true; the layer raised Changed once
[Fact] public void A_bush_without_the_property_leaves_an_empty_cell()   // the hedge
[Theory] [InlineData("stump")] [InlineData("")] [InlineData("1:9,9")]
public void A_property_that_names_no_tile_empties_the_cell(string cut)
    // also with the property an int, 5: the cell is emptied, TryCut is true, nothing is thrown
[Fact] public void A_cell_without_a_bush_is_left_alone()
    // a floor cell, a wall, an empty cell, a cell outside the frame: false, and no layer raised Changed
[Fact] public void Only_the_topmost_bush_is_cut()
    // a bush on Details and one on a third layer over it: the third layer's goes, the other stays
[Fact] public void The_count_is_the_cells_that_hold_a_bush()
    // three bushes, one of them the hedge: 3; after a cut: 2; a map without layers: 0

// DoorsTests
[Fact] public void A_door_is_a_tile_of_the_kind_door_that_says_where_it_leads()
    // At(town, doorCell) is Door(doorCell, "House", "Maps/House"); the mat: Door(cell, "Town", null);
    // a tile of the kind "door" without "to", with "to" empty or with "to" an int: null; a floor cell: null
[Fact] public void The_doors_of_a_map_come_in_the_order_of_their_cells()
    // doors at (5, 2), (1, 2) and (3, 0): Of gives (3, 0), (1, 2), (5, 2)
[Fact] public void A_walker_arrives_beside_the_door_that_leads_back()
    // the house, Room(5, 4), its mat at (2, 0): Arrival(house, "Town") is ((2, 1), Up), the cell under the mat being
    // outside the frame. The town, Room(8, 8), a row of walls at y 5 with the door at (3, 5): Arrival(town, "House")
    // is ((3, 4), Down)
[Fact] public void The_arrival_goes_to_the_next_free_side_and_waits_when_there_is_none()
    // a walker on (3, 4): the arrival is the next free neighbour in the order down, up, left, right; with walls or
    // walkers on all four: null
[Fact] public void A_place_without_a_door_back_has_no_arrival()
    // Arrival(town, "Mill") is null; a place whose component has no map: null
[Fact] public void A_door_is_not_open_ground()
    // a free floor cell: true; the free cell of a door: false; a wall, a cell a walker holds, a cell outside the frame: false
[Fact] public void A_walker_on_a_door_is_taken_beside_the_door_back()
    // the town and the house of the arrival test, the house's place over a component without a map. A walker steps
    // onto the town's door at (3, 5): TryTake is Taken, load was asked once, with "Maps/House", the house's component
    // has the map, and the walker is in the house at (2, 1), facing Up. It steps Down onto the mat: Taken, load is not
    // asked again, and it is in the town at (3, 4), facing Down
[Fact] public void A_walker_that_stands_on_no_door_or_that_walks_is_not_taken()
    // None both times, problem is null, and load is not asked
[Fact] public void A_walker_waits_while_its_arrival_is_not_free()
    // walls beside the mat at (1, 0) and (3, 0), and a second walker on (2, 1): Waits, problem is null, the first is
    // still on the town's door. Once the second stepped away and arrived: Taken
[Fact] public void A_door_that_leads_nowhere_is_wrong_and_says_why()
    // "to" = "Mill": "The door at (3, 5) of 'Town' leads to 'Mill', and the scene has no such place."
    // the house has no map and the door has no "map": "The door at (3, 5) of 'Town' leads to 'House', which has no map, and the door names none."
    // load gives null for "Maps/Hous": "The map 'Maps/Hous' of the door at (3, 5) of 'Town' could not be loaded."
    // the house has no door whose "to" is "Town": "'House' has no door that leads back to 'Town'."
    // each time: Wrong, the walker is where it was, and nothing is thrown
```

- [ ] **Step 2: Run them to see them fail.** Expected: FAIL to compile.
- [ ] **Step 3: Write the three files.** `Count` and `Of` go through the chunks of each layer, not through every cell of the frame: a map without a frame has no cells to list. `TryTake` is the whole of a door: the script of Task 10 only gives it the scene's places and `Content.Load`.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit and push**: `feat(tessera): Thornwick reads brambles and doors from tile data`

---

## The by-hand list

Tasks 2 and 7 to 11 each go through some lines of one list, in the cross-platform Game Studio and then in the game, as [studio-check.md](studio-check.md) says for steps 2 and 3: Xvfb at 1600 by 1000, Lavapipe, the pointer and the keys with `xdotool`. Each line gets "seen", "not as planned: ..." or "could not be checked: ..." in a part "Step 4" of `docs/studio-check.md`, with the names of its pictures (`/mnt/project-files/tilemap/step4/studio-NN-*.png`). The lines are numbered across the tasks: 1 in Task 2, 2 to 8 in Task 7, 9 in Task 8, 10 and 11 in Task 9, 12 to 16 in Task 10, 17 to 28 in Task 11.

Each of these tasks has a step "Fix what the lines found": a fix goes in the file that owns the code, with a test that would have caught it where a test can, in a commit of its own (`fix(tessera): ...`); a gap of the plugin model is a row of `docs/plugin-findings.md`; a line "not as planned" that is not fixed is in the report with its reason. Game Studio is closed before Tessera is built again.

Game Studio's save can write `Thornwick.slnx` again with the same content and other line endings (seen in step 3): when `git diff --stat` shows the solution changed and `git diff --ignore-cr-at-eol` shows nothing, restore it with `git checkout`.

---

### Task 7: The tile sets and the flipbooks, made in Game Studio

The first assets of the game, made as a user makes them. The tests come first: they say what the game reads from the tile data and the flipbooks, and they fail until the assets are there. Lines 2 to 8 of the by-hand list; parts 1, 2 and 4 of the guide take their pictures here.

**Files:**
- Create, in Game Studio: `T/Thornwick.Game/Assets/Tiles/Town.sdtileset`, `Tiles/Indoors.sdtileset`, `Tiles/People.sdtileset`; `Assets/Flipbooks/Hero.sdflipbook`, `Villager1.sdflipbook`, `Villager2.sdflipbook`, `Villager3.sdflipbook`
- Create: `T/tools/draw-people.sh`, `T/tools/draw-water.sh`, and the pictures they make, `T/Thornwick.Game/Resources/Kenney/RpgUrbanPack/people.png` and `Resources/Kenney/TinyTown/water.png`
- Modify: `THIRD-PARTY-NOTICES.md`, `T/README.md` (the two pictures and their scripts)
- Test: `T/Thornwick.Tests/SampleAssets.cs`, `TileSetTests.cs`, `FlipbookTests.cs`
- Modify: `docs/studio-check.md` (the part "Step 4", lines 1 to 8)

**Interfaces:**
- Consumes: `SampleFiles` (Task 2); `TileKinds`, `TileProperties` (Task 6); `AssetFileSerializer.Load<Asset>`, `TileSetAsset.Sources` and `Tiles`, `GridTileSetSource`, `TileSlicer.Grid` and `IsEmpty`, `TileProperty.GetValue`, `TileAssetChecks.Of`, `FlipbookAssetChecks.Of`, `TileMapAsset.ToTileMap`, `FlipbookAsset.ToFlipbook`, `AttachedReferenceManager.GetUrl`, `Stride.Graphics.Image.Load`.
- Produces:

```csharp
// Thornwick.Tests: the game's asset files, read with Tessera's asset classes. Nothing is built.
internal static class SampleAssets
{
    public static readonly string[] TileSets = ["Tiles/Town", "Tiles/Indoors", "Tiles/People"];   // as "The art" names them
    public static readonly string[] Flipbooks = ["Flipbooks/Hero", "Flipbooks/Villager1", "Flipbooks/Villager2", "Flipbooks/Villager3"];
    public static readonly string[] Maps = ["Maps/Town", "Maps/House"];

    public static TileSetAsset TileSetAsset(string url);
    public static TileMapAsset MapAsset(string url);
    public static FlipbookAsset FlipbookAsset(string url);

    /// The pixels of an image file, row by row from the top, as red, green, blue and alpha.
    public static (Color[] Pixels, int Width, int Height) Image(string path);
    /// The runtime tile set of a tile set asset, without textures: a tile for each position of each grid source whose
    /// pixels are not all empty, with the collision, the kind, the properties and the animation of its item in Tiles.
    /// Made once for each URL.
    public static TileSet TileSet(string url);
    /// The runtime map of a map asset, each slot given TileSet(the URL of its reference). The errors as ToTileMap's.
    public static TileMap Map(string url, ICollection<string>? errors = null);
    /// The runtime flipbook of a flipbook asset, its tile set given the same way.
    public static Flipbook Flipbook(string url);
}
```

`Image.Load` gives a PNG as `B8G8R8A8_UNorm`: `Image` swaps red and blue. A grid source's `Image` is a path from the asset's folder, as the file has it (`!file ../../Resources/...`) *(check what the member holds after `AssetFileSerializer.Load`, and make it a full path from the asset's file)*. A source that is not a grid source throws `NotSupportedException`: the sample has none. A color key counts only when the asset's `ColorKeyEnabled` is on.

- [ ] **Step 1: Write `SampleAssets` and the failing tests**

```csharp
// TileSetTests
[Theory] [MemberData(nameof(TileSets))]
public void A_tile_set_has_nothing_to_tell(string url)
    // TileAssetChecks.Of(asset) is empty; every source is a grid source whose image file is there; every item of Tiles
    // names a tile the set has; every frame of an animated tile names a tile the set has and lasts above 0 s
[Fact] public void The_town_has_a_bush_and_what_is_left_of_it()
    // one tile or more of Tiles/Town has the kind "bush"; each is solid, and its property "cut" is a string, the id
    // of a tile of the set that is neither solid nor a bush
[Fact] public void One_door_leads_to_the_house_and_one_leads_back()
    // over every tile set: one tile of the kind "door" has "to" = "House", and its "map" is "Maps/House"; one has
    // "to" = "Town"; neither is solid; no other tile has the kind "door"
[Fact] public void The_water_plays_and_stops_the_hero()
    // a tile of Tiles/Town is solid and animated, with two frames or more

// FlipbookTests
[Theory] [MemberData(nameof(Flipbooks))]
public void A_flipbook_has_nothing_to_tell(string url)   // FlipbookAssetChecks.Of(asset) is empty
[Theory] [MemberData(nameof(Flipbooks))]
public void A_figure_stands_and_walks_the_four_ways(string url)
    // "idle" and "walk" are there, and "walk" loops. Each has a clip of its own for Down, Left, Right and Up (looked
    // for in Clips: FindClip falls back to another clip). A clip of "walk" has two frames or more. Every frame lasts
    // above 0 s and has one cell or more, each with a tile the flipbook's tile set has. The pivot is (0.5, 0)
[Fact] public void The_four_figures_differ()
    // the tile of the first frame of idle, Down, of the four flipbooks: four tiles
```

- [ ] **Step 2: Run them to see them fail.** Run: `dotnet test sample/Thornwick/Thornwick.Tests --filter "FullyQualifiedName~TileSetTests|FullyQualifiedName~FlipbookTests"`. Expected: FAIL, each test with a file that is not found, named in the message (`Tiles/Town.sdtileset`).
- [ ] **Step 3: The people and the pond.** Write `tools/draw-people.sh` and `tools/draw-water.sh` as "The art" has them, and run each: `Resources/Kenney/RpgUrbanPack/people.png` (288 by 64) and `Resources/Kenney/TinyTown/water.png` (48 by 64) are made. Compare each with the planner's: `compare -metric AE <the picture made> /mnt/project-files/tilemap/art/check/<its name> null:` says 0. Run each a second time: `git status` shows no change (if this ImageMagick writes a date in the file, say so in the task's note and keep the first picture). Look at both pictures. The notice gains two lines: each picture, the pack it is derived from, and its script. The sample's README says how the two are made.
- [ ] **Step 4: Make the tile sets** (lines 2 to 5).
  2. **Folders and new assets.** In the asset view: a folder `Tiles`, made with the *New folder* command of its menu (`Stride.Asset.NewFolder`, read in the view's file and not yet tried), then *Add asset > Sprite > Tile set* for each tile set, renamed with F2. If the folder cannot be made: make the assets at the root, close Game Studio, move the files with `git mv`, open the solution again and save (a reference is an id first, and the save writes the new URLs); a finding.
  3. **A tile set from an image.** In each tile set, `+ Image` with the sheet of "The art": the bar shows the sheet's name, the tile size and the number of tiles its table gives, and `Add` makes the source. The canvas shows the whole sheet. `Tiles/Town` gets two images, the pack's sheet first and `water.png` second: the tiles of the second are `2:column,row`. Every sheet is cut in tiles of 16 by 16, the figures too.
  4. **Tile data.** In `Solid` mode, a drag over the tiles that stop the hero, as the table of "The art" lists them: walls, water, trunks, fences, the bush, the furniture, and no roof. In `Select` mode: the bush gets the kind `bush` and a property `cut`, of the type `String`, whose value is the id of the tile left after the cut, as the property grid's title shows it for that tile (`1:5,1`); the town's door gets the kind `door`, `to` = `House` and `map` = `Maps/House`; the house's door, or its mat, gets the kind `door` and `to` = `Town`. Neither door is solid. Undo and redo of one of them from the Edit menu.
  5. **The water.** `Animation` mode: a click on the middle of the pond (`2:1,1`), then on each of its three frames in order (`2:1,1`, `2:0,3`, `2:1,3`); each frame at 0.3 s in the property grid; the water plays on the canvas (two pictures a moment apart differ on it); `Done`.
- [ ] **Step 5: Make the flipbooks** (lines 6 to 8).
  6. **The hero's flipbook.** A folder `Flipbooks`, *Add asset > Sprite > Flipbook*, named `Hero`. `Pick a tile set` takes the figures' tile set. `Sheet`, a drag over the hero's block in the palette (three columns by four rows: the table of "The art"), the rows left as the bar proposes them (Down, Left, Right, Up), `There and back`, 0.1 s a frame, the name `walk`, `Make`: the tree has `walk` with Down, Left, Right and Up, and the preview plays each. A clip of `walk` has four frames: a step, standing, the other step, standing. The people's picture has a row for each side, so `Mirror` is not needed here. Then `idle`: one clip for each of the four sides, each with the standing frame of that side. `Sheet` over the standing column, the middle one of the block, with the name `idle`, adds the four clips to the `idle` that is there (`MakeSheet` gives the animation of that name a clip for each row and keeps its other clips), and the clip "Any direction" of `idle`, which has no frame, is removed with `-`. If the block of one column is not taken, the four clips are added with `+ Direction` and one click in the palette each. The problems under the editor are gone.
  7. **The fixes of Task 1, by hand.** In `Hero`: with a frame selected, `Replace`, then a click in the palette, changes the frame's tile, and one undo brings the old tile back. After a click on `>`, Delete removes the selected frame (undone after). On an animation added with `+` whose clip was removed with `-`, a click in the palette says "Add a clip to the animation first, with + Direction." (the animation is removed after). A clip whose frames are all set to 0 s shows its line under the editor (undone after).
  8. **The villagers.** Three more flipbooks, `Villager1` to `Villager3`, each from the block of its figure, the same way, with frames of 0.2 s.
- [ ] **Step 6: Save all, close Game Studio, and run the tests.** Run: the filter of Step 2. Expected: PASS. `git status` shows the new asset files, and nothing changed outside `sample/Thornwick`.
- [ ] **Step 7: Fix what the lines found**, as "The by-hand list" says.
- [ ] **Step 8: Write the lines** 1 to 8 in `docs/studio-check.md`, then commit and push: `feat(tessera): Thornwick's tile sets and flipbooks, made in Game Studio`

### Task 8: The town, written by a tool from text pictures (decision 2)

The town has 7200 cells on three layers: too many to paint under `xdotool`, and a text picture is easy to read, to review and to change. A small tool turns the pictures into the map's file, as `tools/MakeWorld.cs` writes the world of Skein's second sample (`git show origin/feature/colorrise-skein:extensions/ColorRise.Skein/sample/Ashcombe/tools/MakeWorld.cs`: its head, and its ids).

**Files:**
- Create: `T/tools/MakeMaps.cs`, `T/tools/maps/Town.txt`, `T/Thornwick.Game/Assets/Maps/Town.sdtilemap` (written by the tool)
- Test: `T/Thornwick.Tests/Reach.cs`, `MapPicture.cs`, `TownTests.cs`
- Modify: `T/README.md` (how the town is made), `docs/studio-check.md` (line 9)

**Interfaces:**
- Consumes: `SampleAssets` (Task 7); `Place`, `GridWalker` (Task 3); `Brambles`, `Doors` (Task 6).
- Produces: the tool, the format of its pictures, and two helpers of the tests.

The tool is a .NET file-based app without a package or a project reference, run from `T/`:

```sh
dotnet run tools/MakeMaps.cs -- Thornwick.Game
```

For each `tools/maps/*.txt` it writes `<game>/Assets/<url>.sdtilemap`, whole. A picture:

```text
# Thornwick's town. A line that starts with # is a remark.
map Maps/Town 60 40
tileset 1 = 0f3c9a52-6a0b-4b3e-9d1a-a5c0b1e7d401:Tiles/Town
tile g = 1/1:0,0        # grass: a character stands for the text of a cell, as the map's file writes it
tile w = 1/2:1,1        # the middle of the pond, from the second image of the tile set
tile B = 1/1:5,0        # a bramble
tile ] = 1/1:8,6h       # the left end of a fence, flipped
layer Ground
gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg
...                     40 lines of 60 characters, the top row first; a dot or a space is an empty cell
layer Details
...
layer Overhead
...
```

- `map <url> <width> <height>` comes first. `tileset <slot> = <id>:<url>` is the reference of a tile set as the map's file has it: the id is copied by hand from the line `Id:` of the tile set's file, once (the tool reads no asset). `tile <character> = <cell text>` gives a character its cell, with flips after the tile id as `TileChunkText` writes them (`d`, `h`, `v`); the dot, the space and `#` cannot be given a tile. On these three kinds of lines, what follows a `#` is a remark. `layer <name>` starts a layer; the layers are written in the order of the file, bottom first.
- What the tool refuses, with the file, the line and the reason, and the exit code 1: a character without a `tile` line, a layer that has not `height` lines or a line that has not `width` characters, a layer named twice, a cell text whose slot has no `tileset` line, a file without a `map` line. Whether a tile exists is the tests' business, not the tool's.
- What it writes is the text Game Studio writes: the model is `tests/StudioCheck/StudioCheck.Game/Assets/TileMap.sdtilemap`, key for key and space for space. `CellSize: {X: 1.0, Y: 1.0}`, `Size: {X: 60, Y: 40}`; a layer has `Visible: true`, `Opacity: 1.0`, a white `Tint`, `Offset` 0 and `Collision: true`; `LastSlot` is the highest slot. Only the chunks that hold a cell are written, top to bottom then left to right, as `TileLayerAsset.SortChunks` orders them; a chunk is 16 lines of 16 cells, the top row first, one space between cells; the cells of a chunk past the frame are `.`.
- Ids are UUID v5 of names under a namespace written in the tool, as `MakeWorld.cs` makes them: the asset's id from `map/<url>`, the keys of `TileSets` and `Layers` (32 hex digits) from `<url>/tileset/<slot>` and `<url>/layer/<name>`. Two runs write the same bytes.

```csharp
// Thornwick.Tests
internal static class Reach
{
    /// The cells a walker reaches from a cell by steps to free neighbours, the start included. With throughBrambles, a
    /// cell that holds a bush counts as free.
    public static HashSet<Int2> From(Place place, Int2 start, bool throughBrambles = false);
    /// A shortest way from a cell to another, as the sides to step to, in order; null when there is none.
    public static IReadOnlyList<FlipbookDirection>? Path(Place place, Int2 from, Int2 to, bool throughBrambles = false);
}

internal static class MapPicture
{
    /// Draws the layers of a map of the game inside its frame, the bottom layer first, from the images of its tile
    /// sets: an animated tile by its first frame, the flips as the game draws them, a cell on as many pixels as its
    /// tile has. The top row of the frame is the top of the picture.
    public static (Color[] Pixels, int Width, int Height) Draw(string url);
    /// Saves the picture as pictures/<name>.png beside the test assembly, and gives the path.
    public static string Save(string url);
}
```

**The town.** Whoever builds it draws it, inside these rules, and looks at the picture after each change (`pictures/Town.png`):

1. `Ground` has a tile on every cell of the frame, and no layer has a cell outside it.
2. A pond on `Ground`, in the nine pieces of the table of "The art": the middle piece, the one that plays, on 12 cells or more (a pond of 8 by 4 has 12), its banks around them, and grass to walk along.
3. The hero's house, of stone: its walls and its door on `Details`, the door in its front wall with a free cell under it, and its roof, two rows, on `Overhead`. A roof is not solid, and every layer the tool writes stops a walker: under the lower row of the roof, `Details` has a second row of wall, which the roof hides and which stops the hero; under the upper row nothing is solid, so the hero walks behind the house and the roof hides them. One or two more buildings, of wood, made the same way, with a solid door of another tile (decision 7).
4. Trees: a solid trunk on `Details`, the crown on `Overhead` over the cell above, which is free.
5. Twelve brambles on `Details`. Three stand side by side across a lane, and what is behind them (a garden, the far shore of the pond) cannot be reached without cutting. The nine others are spread over the town, each beside a cell the hero reaches.
6. A square of 8 by 6 free cells or more near the house, where Task 10 puts the villagers, and paths from the door to the square, the pond and the lane.
7. A corner cell of the frame can be walked to: Task 11 takes the hero there to see the camera stop.

- [ ] **Step 1: Write `Reach`, `MapPicture` and the failing tests**

```csharp
// TownTests
[Fact] public void Every_cell_of_the_town_names_a_tile_that_exists()
    // SampleAssets.Map("Maps/Town", errors): no error, TileAssetChecks.Of(the map asset) is empty, and every cell of
    // every chunk of every layer that is not empty gives a tile with map.GetTile
[Fact] public void The_town_is_sixty_by_forty_with_its_three_layers()
    // Size (60, 40), cell size (1, 1); the layers are Ground, Details and Overhead, in that order; Ground has a tile
    // on each of the 2400 cells of the frame; no layer has a cell outside the frame
[Fact] public void The_town_has_water_that_plays_twelve_brambles_and_one_door()
    // 12 cells of Ground or more hold an animated tile; Brambles.Count is 12; Doors.Of has one door, to "House",
    // with the map "Maps/House"
[Fact] public void The_hero_is_not_walled_in_at_the_start()
    // the start is Doors.Arrival(town, "House"): it is there, and faces Down. Reach.From(town, start) has 100 cells or more
[Fact] public void The_hero_can_walk_to_a_corner_of_the_town()
    // one of the four corner cells of the frame is reached from the start
[Fact] public void The_brambles_close_a_part_of_the_town()
    // walked through, the brambles open 10 cells or more that are not reached without, their own cells not counted
[Fact] public void The_hero_walks_under_the_overhead_layer()
    // 6 cells or more that are reached from the start have a tile on Overhead
[Fact] public void The_town_is_drawn_to_a_picture()
    // MapPicture.Save("Maps/Town"): the file is there, 960 by 640 pixels, and no pixel of it is transparent
```

- [ ] **Step 2: Run them to see them fail.** Run: `dotnet test sample/Thornwick/Thornwick.Tests --filter FullyQualifiedName~TownTests`. Expected: FAIL (`Maps/Town.sdtilemap` is not found).
- [ ] **Step 3: Write the tool**, with the model file open beside it, and a first `Town.txt`: the `tileset` line from the id of `Tiles/Town`, a `tile` line for each tile of the table of "The art" the town uses, and three layers of grass and nothing. Run the tool. Expected: `Maps/Town.sdtilemap` is written, and the picture test passes with a field of grass.
- [ ] **Step 4: Draw the town**, a part at a time: change `Town.txt`, run the tool, run the picture test, look at `pictures/Town.png`. Copy the last picture to `/mnt/project-files/tilemap/step4/town.png`.
- [ ] **Step 5: Run the tests.** Expected: PASS.
- [ ] **Step 6: Run the tool again.** Expected: `git status` shows no change after the second run.
- [ ] **Step 7: The tool's file is Game Studio's (R15; line 9).** Commit the map first. Open the solution in Game Studio and the town in the map editor: the three layers, the tile set and the whole town show, and the water plays. On `Ground`, stamp a path tile on one cell that had grass, and save. Expected: `git diff --stat` has one file, `Town.sdtilemap`, with one line changed. Restore the file after (`git checkout`): the town is the tool's. If more changed, take R15's fallback, and say which lines in the check's line.
- [ ] **Step 8: The sample's README** says how the town is made: the command, the format of a picture, and that a change made to the town in Game Studio is lost the next time the tool runs.
- [ ] **Step 9: Write line 9** in `docs/studio-check.md`, then commit and push: `feat(tessera): Thornwick's town, written by a tool from text pictures`

### Task 9: The house, painted in the map editor

The second map, small enough to paint by hand: the map editor's part of the by-hand check, and part 3 of the guide.

**Files:**
- Create, in Game Studio: `T/Thornwick.Game/Assets/Maps/House.sdtilemap`
- Test: `T/Thornwick.Tests/HouseTests.cs`, `PlaythroughTests.cs`
- Modify: `docs/studio-check.md` (lines 10 and 11)

**Interfaces:**
- Consumes: `SampleAssets` (Task 7); `Reach`, `MapPicture` (Task 8); `Doors.TryTake`, `Brambles`, `GridWalker`.
- Produces: the map `Maps/House`, the URL the town's door names.

**The house**: a room of 12 by 9 cells. `Ground` has the floor on every cell of the frame. `Details` has the walls around it (the back wall two cells high, as seen from the front) and the furniture (a table, a stool, shelves, a chest, a stove, all solid: the table of "The art"). The door back to the town is a mat of flagstones, on `Ground` in the bottom row, near the middle, with a free cell above it: the hero arrives there, facing up. 20 cells or more can be walked.

- [ ] **Step 1: Write the failing tests**

```csharp
// HouseTests
[Fact] public void Every_cell_of_the_house_names_a_tile_that_exists()   // as the town's
[Fact] public void The_house_is_a_room_with_one_door_back_to_the_town()
    // a frame from 8 by 6 to 16 by 12 cells, which a view of 26 by 15 cells shows whole; the layers Ground and
    // Details; Ground on every cell of the frame and no cell outside it; one door, to "Town";
    // Doors.Arrival(house, "Town") is there, and Reach.From it has 20 cells or more
[Fact] public void The_house_is_drawn_to_a_picture()   // pictures/House.png, 16 pixels a cell

// PlaythroughTests: the maps of the game, and nothing drawn
[Fact] public void The_door_leads_to_the_house_and_back()
    // the town's place over its map, the house's place over a component without a map. A walker starts on
    // Doors.Arrival(town, "House") and follows Reach.Path to the town's door, a step at a time (TryStep, then
    // Advance(StepSeconds)). Doors.TryTake, with a load that reads the map's file through SampleAssets.Map, is Taken:
    // load was asked for "Maps/House", and the walker stands in the house on a free cell beside its door. It steps
    // onto that door: Taken, and it stands in the town on the cell it started from, facing Down
[Fact] public void The_whole_game_can_be_played()
    // from the start, for each bramble, the nearest first: the walker follows Reach.Path to a cell beside it, turns
    // to it (a TryStep that only turns) and Brambles.TryCut(map, walker.Ahead) is true. At the end Brambles.Count is 0,
    // and the walker never stood outside the frame
```

- [ ] **Step 2: Run them to see them fail.** Expected: FAIL (`Maps/House.sdtilemap` is not found; the playthrough fails at the door).
- [ ] **Step 3: Paint the house** (lines 10 and 11).
  10. **A new map.** *Add asset > Sprite > Tile map*, named `House`, in a folder `Maps` beside `Town`. In its property grid, `Size` is set to (12, 9). In the map editor, `+` beside the tile sets takes the tile set of the indoors. A second layer is added above `Ground` and renamed `Details` with a double click. The canvas shows the frame as an outline, and a stamp outside it paints nothing (`TileMapDocument.CanWrite`): set the size before painting.
  11. **Painting.** The floor with the Rectangle tool (P). The walls with a block picked by a drag in the palette and stamped (B), and a line of them with Shift and a click. The furniture on `Details`, a piece flipped with X before it is stamped. A wrong stamp taken back with Ctrl+Z, a cell emptied with the Erase tool (E), a piece moved with the Select tool (R). `Dim other layers` while painting `Details`. Save. A picture of the editor with the finished room.
- [ ] **Step 4: Close Game Studio and run the tests.** Run: `dotnet test sample/Thornwick/Thornwick.Tests`. Expected: PASS, the whole of the sample's tests. Copy `pictures/House.png` to `/mnt/project-files/tilemap/step4/house.png`.
- [ ] **Step 5: Fix what the lines found**, as "The by-hand list" says.
- [ ] **Step 6: Write the lines** 10 and 11 in `docs/studio-check.md`, then commit and push: `feat(tessera): Thornwick's house, painted in the map editor`

### Task 10: The scripts, the scene and the compositor

Four scripts connect the rules of Tasks 3 to 6 to Stride, and the scene is made in Game Studio as a user makes it. Each script is a few lines over rules that have their tests, so the scripts have no test of their own beyond the three small functions below: Task 11 plays them. Lines 12 to 16 of the by-hand list; parts 5, 6 and 7 of the guide.

**Files:**
- Create: `T/Thornwick.Game/HeroKeys.cs`, `FrameTime.cs`, `World.cs`, `PlayerController.cs`, `Wanderer.cs`, `FollowCamera.cs`
- Modify, in Game Studio: `T/Thornwick.Game/Assets/MainScene.sdscene`, `Assets/GraphicsCompositor.sdgfxcomp`, `T/Thornwick.Game/Thornwick.Game.sdpkg` (a root asset)
- Test: `T/Thornwick.Tests/HeroKeysTests.cs`, `FrameTimeTests.cs`, `WorldTests.cs`
- Modify: `docs/studio-check.md` (lines 12 to 16)

**Interfaces:**
- Consumes: `Place`, `GridWalker` (Task 3); `WanderPlan` (Task 4); `CameraFraming` (Task 5); `Brambles`, `Doors`, `DoorResult` (Task 6); `SyncScript`, `Input.IsKeyDown` and `IsKeyPressed`, `Game.UpdateTime.Elapsed`, `Game.Window.Title` and `AllowUserResizing`, `GameBase.Exit`, `Content.Load<TileMap>`, `ContentManagerException`, `ScriptComponent.Priority` (lower runs first), `CameraComponent.OrthographicSize`, `GraphicsDevice.Presenter.BackBuffer` *(check the last name from a script)*.
- Produces:

```csharp
public static class HeroKeys
{
    /// The side the pressed keys ask for: Left or Right when one of the two is down and not the other, else Up or Down
    /// the same way, else None.
    public static FlipbookDirection Wanted(bool left, bool right, bool up, bool down);
}

public static class FrameTime
{
    public const float Longest = 0.1f;
    /// The seconds a frame of the game counts for: its time, 0 when it is negative, Longest at most.
    public static float Of(TimeSpan elapsed);
}

/// <summary>The places of the scene, the doors between them and the count of the brambles. One in a scene.</summary>
public sealed class World : SyncScript
{
    /// The place of a name: the entity of the scene, at any depth, that has that name and a tile map component; null
    /// when there is none. A place is made the first time it is asked for, and kept.
    public Place? Find(string name);
    /// Doors.TryTake with the places of the scene and Content.Load. A wrong door is one error line of the log; a map
    /// that cannot be loaded is a wrong door, not an exception.
    public DoorResult TakeDoor(GridWalker walker);
    /// Cuts the bush in front of a walker, and counts the brambles again when one was cut.
    public bool Cut(GridWalker walker);
    /// The brambles left, in the places that have a map.
    [DataMemberIgnore] public int BramblesLeft { get; }
    /// The world of the scene an entity is in; null when the scene has none.
    public static World? Of(Entity entity);
    /// "Thornwick: 12 brambles left", "Thornwick: 1 bramble left", and "Thornwick: all the brambles are cut" at 0 or under.
    public static string TitleFor(int brambles);
}

/// <summary>The hero: walks with the arrows or W, A, S and D, cuts with Space, and takes the doors.</summary>
public sealed class PlayerController : SyncScript
{
    /// <userdoc>The name of the place the hero starts in.</userdoc>
    public string Place { get; set; } = "Town";
    /// <userdoc>The hero starts beside the door of its place that leads here, as if coming out of it. Empty: on the cell under the entity.</userdoc>
    public string ComesFrom { get; set; } = "House";
    /// <userdoc>The seconds a step to the next cell takes.</userdoc>
    public float StepSeconds { get; set; } = 0.2f;
    [DataMemberIgnore] public GridWalker? Walker { get; }   // null until Start, and when the hero could not be placed
}

/// <summary>A villager: steps to a free cell near where it stood at the start, then stands a while.</summary>
public sealed class Wanderer : SyncScript
{
    public string Place { get; set; } = "Town";
    public int Radius { get; set; } = 4;
    public int Seed { get; set; }
    public float StepSeconds { get; set; } = 0.4f;
}

/// <summary>Keeps an orthographic camera on a target, at a whole zoom, inside the frame of the place the target is in.</summary>
public sealed class FollowCamera : SyncScript
{
    public Entity? Target { get; set; }
    public float PixelsPerUnit { get; set; } = 16;
}
```

What each script does, and no more:

- **`World`.** `Start` lets the window be resized (`AllowUserResizing`), counts the brambles and writes the title. `Update` quits on Escape, and writes the title when the count changed. `TakeDoor` counts again after a door was taken, since a place may have got its map. Its loader catches `ContentManagerException`, writes one error line and gives null.
- **`PlayerController`.** `Start` finds the world and the place, and makes the walker beside the door `ComesFrom` names (`Doors.Arrival`); with `ComesFrom` empty, or when the place has no such door, on the cell under the entity (`UpdateWorldMatrix`, then `WorldToCell`). When there is no world, no place, or the cell is not free, it writes one error line that says which and where ("The hero stands on (12, 7) of 'Town', which is not free: move the entity in the scene."), and the script then does nothing. Its `Update` is the heart of the game, and the guide shows it whole:

```csharp
public override void Update()
{
    if (Walker is not { } walker)
    {
        return;
    }
    var arrived = walker.Advance(FrameTime.Of(Game.UpdateTime.Elapsed));
    if (!walker.IsMoving)
    {
        // A door takes the hero when they arrive on it, and again each frame while someone stands where they would arrive
        if (arrived || waitsAtDoor)
        {
            waitsAtDoor = world.TakeDoor(walker) == DoorResult.Waits;
        }
        if (Input.IsKeyPressed(Keys.Space))
        {
            world.Cut(walker);
        }
        else
        {
            walker.TryStep(HeroKeys.Wanted(IsDown(Keys.Left, Keys.A), IsDown(Keys.Right, Keys.D), IsDown(Keys.Up, Keys.W), IsDown(Keys.Down, Keys.S)));
        }
    }
    Entity.Transform.Position = walker.Place.ToWorld(walker.Position);
    flipbook?.Direction = walker.Facing;
    flipbook?.Play(walker.IsMoving ? "walk" : "idle");
}
```

`world`, `flipbook` and `waitsAtDoor` are what `Start` found and what the last door answered; `IsDown(a, b)` is `Input.IsKeyDown(a) || Input.IsKeyDown(b)`.

- **`Wanderer`.** `Start` makes its walker on the cell under its entity, with the same error line when that cell is not free, and a `WanderPlan` around it over `new Random(Seed)`. `Update` advances the walker; while it stands, a pause counts down, and at its end the plan gives a side among the cells that are open ground (`Doors.IsOpenGround`), the walker steps there, and the next pause is drawn. It shows its walker as the hero does.
- **`FollowCamera`.** `Update` asks `CameraFraming.Fit` with the target's position, half a unit up (the middle of its cell), the frame of the place its `PlayerController` walks in (none when the target has no such script), and the size of the back buffer; it sets `OrthographicSize` and moves its entity to the centre, keeping its z. Without a target it does nothing.

- [ ] **Step 1: Write the failing tests**

```csharp
// HeroKeysTests
[Theory]
[InlineData(true, false, false, false, FlipbookDirection.Left)] [InlineData(false, true, false, false, FlipbookDirection.Right)]
[InlineData(false, false, true, false, FlipbookDirection.Up)] [InlineData(false, false, false, true, FlipbookDirection.Down)]
[InlineData(false, false, false, false, FlipbookDirection.None)] [InlineData(true, true, false, false, FlipbookDirection.None)]
[InlineData(true, true, true, false, FlipbookDirection.Up)] [InlineData(true, false, true, false, FlipbookDirection.Left)]
[InlineData(false, false, true, true, FlipbookDirection.None)] [InlineData(true, true, true, true, FlipbookDirection.None)]
public void The_keys_ask_for_one_side(bool left, bool right, bool up, bool down, FlipbookDirection side)

// FrameTimeTests
[Theory] [InlineData(16, 0.016f)] [InlineData(100, 0.1f)] [InlineData(5000, 0.1f)] [InlineData(0, 0f)] [InlineData(-20, 0f)]
public void A_frame_counts_for_a_tenth_of_a_second_at_most(int milliseconds, float seconds)

// WorldTests
[Theory] [InlineData(12, "Thornwick: 12 brambles left")] [InlineData(1, "Thornwick: 1 bramble left")]
[InlineData(0, "Thornwick: all the brambles are cut")] [InlineData(-3, "Thornwick: all the brambles are cut")]
public void The_title_counts_the_brambles(int brambles, string title)
```

- [ ] **Step 2: Run them to see them fail.** Expected: FAIL to compile.
- [ ] **Step 3: Write the six files.** Run the tests of the three files. Expected: PASS. Then `dotnet build sample/Thornwick/Thornwick.Game`: Game Studio lists a script once its assembly is built.
- [ ] **Step 4: Make the scene and the compositor in Game Studio** (lines 12 to 16).
  12. **The scripts show (R17).** *Add component > Scripts* lists `FollowCamera`, `PlayerController`, `Wanderer` and `World`, and the property grid shows the properties of each, with the text of their `<userdoc>`.
  13. **The places and the world.** The asset `Maps/Town` dropped in the scene view makes an entity `Town` with the map; its position is set to (0, 0, 0), and the scene view draws the town, its water playing. *Create entity > 2D > Tile map* makes a second one, renamed `House`, at (100, 0, 0), its map left empty. An empty entity, `World`, gets the script `World`.
  14. **The hero and the villagers.** `Flipbooks/Hero` dropped in the scene makes `Hero`, showing `idle`; it gets `PlayerController`. The three villagers' flipbooks, dropped on three cells of the square, make three entities; each gets `Wanderer`, with the seeds 1, 2 and 3. The z of the four figures is set to 0.15, so that the scene view shows them over the ground and under the roofs, as the game will.
  15. **The camera.** The scene's camera is set to `Orthographic`, at z 10. It gets `FollowCamera`, with `Target` set to `Hero` in the property grid (R17) and `Priority` set to 100, so that it runs after the hero moved.
  16. **The compositor, and the root asset.** In the compositor editor: *Add package features* adds `TileRenderFeature` and `FlipbookRenderFeature` with their selectors; the sort mode of the transparent stage is set to "Back to front along an axis"; the post effects of the game's renderer are taken off *(check: how the property grid empties them; if it cannot, the file gets `PostEffects: null` by hand, and a finding)*; the clear colour is black, which is what shows around the house. In the asset view, *Include in build as root asset* on `Maps/House` (R18; the command `Stride.Asset.ToggleRoot` of the asset view's menu): `Thornwick.Game.sdpkg` gains its line under `RootAssets`. Save all.
- [ ] **Step 5: Close Game Studio, build and start the game.** Run: `dotnet build sample/Thornwick/Thornwick.Linux`, then the game under Xvfb for 10 seconds, its output in a log file. Expected: the assets compile with no warning that names an asset of Thornwick; the window's title is "Thornwick: 12 brambles left" (`xdotool getwindowname`); a picture shows the town, the hero under the house's door and the three villagers; the log has no error.
- [ ] **Step 6: Fix what the lines found**, as "The by-hand list" says.
- [ ] **Step 7: Write the lines** 12 to 16 in `docs/studio-check.md`, then commit and push: `feat(tessera): Thornwick's scripts, its scene and its compositor`

### Task 11: The game, played by hand

The run by hand of section 14, over the sample: the game under Xvfb with Lavapipe, driven with `xdotool`, a picture for each line. Lines 17 to 28; part 8 of the guide.

**Files:**
- Modify: `docs/studio-check.md` (lines 17 to 28), `T/README.md` (how to run it, the keys), and what the lines find
- Create, only with R16's fallback: the reading of `THORNWICK_KEYS` in `PlayerController`, and its test in `HeroKeysTests`

**Interfaces:**
- Consumes: everything before; `tests/StudioCheck/README.md` (how the check game is run under Xvfb).
- Produces: the pictures `/mnt/project-files/tilemap/step4/studio-17-*.png` to `studio-28-*.png`, the lines with what was seen, and the fixes.

A key pressed and let go within one step's time moves the hero one cell: a way through the town is a list of taps, read from `tools/maps/Town.txt` (`xdotool key Right`, then 0.25 s, once for each cell).

- [ ] **Step 1: The keys reach the game (R16).** Start the game, give its window the focus (`xdotool search --name "^Thornwick"`, `windowfocus`), and tap Down once. Expected: two pictures show the hero one cell lower. If no key arrives by any of `xdotool`'s ways, take R16's fallback and say so in line 17.
- [ ] **Step 2: Go through the lines.**
  17. **The start.** The hero stands under the door of the house and faces down, as if stepping out. The title says "Thornwick: 12 brambles left". The pond plays: two pictures half a second apart differ on the water, and only there and on the villagers.
  18. **Four ways.** A tap of each arrow, and of W, A, S and D, moves the hero one cell that way, facing it. While a key is held, two pictures 0.1 s apart show two frames of `walk`; once let go, the hero stands on a cell, in the `idle` of that side. Ten cells with the key held take two seconds, within a frame or two.
  19. **Blocked.** A wall, the water, a trunk, a fence and a villager stop the hero, who turns to them and stays. At the edge of the town, the hero stops on the last cell of the frame.
  20. **Under the overhead layer.** Behind the house, the roof hides the hero's head or all of them; under a tree's crown too. In front of the house, the hero is over the wall.
  21. **A bramble.** Facing a bramble, Space puts the cut tile in its place, the title says 11, and the hero walks over the cell. Space in front of a wall, of the water or of nothing does nothing.
  22. **The lane.** The three brambles across the lane cut, the hero walks to what was behind them.
  23. **Into the house.** The hero steps onto the door: the view shows the house, whole and centred, black around it, the hero beside its door, facing up. The log has no error (R18: the map was loaded by its URL).
  24. **And back.** The hero steps onto the house's door: the town again, the hero under the door, facing down. The brambles cut before are still cut. In and out a second time: the same.
  25. **The villagers.** Over 30 seconds, each of the three walks and stands, stays near where it started, and none steps on the door. The hero just above a villager is drawn behind it, and just below, in front (two pictures).
  26. **The camera.** It follows the hero. In the corner of the town the rules made reachable, the view stops at the frame: the picture's edge is the map's edge, and nothing of the clear colour shows. A tile takes 48 by 48 pixels of a picture. Across ten pictures taken while walking, the tiles keep their size and no line shows between them (R19). The window resized to 1536 by 864 (`xdotool windowsize`; the screen of the check is 1600 by 1000) shows tiles of 64 pixels, and to 640 by 360, of 16.
  27. **Colours, and a minute of play.** A pixel in the middle of a grass tile of a picture has the colour of that pixel in the pack's PNG. After a minute of walking, cutting and doors, the log has no error and no warning from Thornwick or Tessera.
  28. **The end.** Escape closes the game, and its process ends with the code 0. The WPF Game Studio and Windows are not checked, as in steps 2 and 3: say so.
- [ ] **Step 3: Fix what the lines found**, as "The by-hand list" says.
- [ ] **Step 4: The sample's README** says how to build and run the game, on Linux and on Windows (not tried), and its keys.
- [ ] **Step 5: Write the lines** 17 to 28 in `docs/studio-check.md`, then commit and push: `test(tessera): Thornwick played by hand, and what was seen`

### Task 12: The guide, the READMEs and the notes

**Files:**
- Create: `docs/guide.md`, `docs/guide/*.png` (14 pictures at most, 150 KB each at most)
- Modify: `README.md`, `T/README.md`, `THIRD-PARTY-NOTICES.md`, `docs/design.md`, `docs/plugin-findings.md`

- [ ] **Step 1: The guide**, "Make a small game with Tessera". It is written for someone who knows Stride and has never seen Tessera, in the order Thornwick was made, and it names a command only as a line of the by-hand list saw it. Eight parts, each with what to do, one or two pictures, and the file of the sample that has the result:
  1. A tile set from an image (lines 2 and 3).
  2. Tile data: what is solid, kinds and properties, an animated tile (lines 4 and 5).
  3. A map: layers, the tools, the frame (lines 10 and 11); one paragraph on writing a large map with a tool, with a link to the sample's README.
  4. A flipbook with `Sheet` (line 6), and a paragraph on a sheet that has a column for each side: the script that lays the people out a row a side.
  5. The scene: maps and flipbooks as entities, and where a character stands between two layers (lines 13 and 14).
  6. The compositor, the sort mode and the camera (lines 15 and 16): a whole zoom, and no post effects for pixel art.
  7. The scripts: `PlayerController.Update` whole, `Brambles.TryCut` whole, and the others by name with a line each. What Tessera answers (`IsSolid`, `GetTile`, `Kind`, `Properties`, `SetCell`, `Direction`, `Play`) is said beside the line that asks.
  8. Build and play (lines 17 to 28): the commands, the keys, and what to try next (a second door, a new kind of tile).

  The pictures are cropped copies of the pictures of the by-hand list, made with ImageMagick. Check the links: every picture the guide names is in `docs/guide/`, and every file of `docs/guide/` is named by the guide.
- [ ] **Step 2: The sample's README**: the story in two lines; how to play (Task 11); how to open it in Game Studio; a table of what is where (the rules, the scripts, the assets, the tool, the tests); how the town is made (Task 8); how the pictures of the people and of the pond are made (Task 7); the credits of the art.
- [ ] **Step 3: Tessera's README.** "Try it" gains a fourth point, the sample: the two commands that build and run it. "What works today" says the four steps are done, names the sample and links the guide. "Not yet" loses the sample. The credits name Kenney's packs, with the link to the notice.
- [ ] **Step 4: The notice.** `THIRD-PARTY-NOTICES.md` names each pack, its author, its licence, the files taken and where they are, and the two pictures Task 7 derived from them, each with its script. Read it against the files of `Resources/Kenney/`.
- [ ] **Step 5: The design.** A short dated note where this plan settled or changed it: section 13 (the sample as built: a door is tile data, both places are in one scene and the game loads the second map the first time, the hero moves by cells, the title of the window counts), section 14 (the sample's tests as built, and the by-hand run), section 16 (the name and the story). The decisions D and C of this plan are copied to the design's lists.
- [ ] **Step 6: The findings.** Every gap of the plugin model this step met, from row 21 on, in the format of the file.
- [ ] **Step 7: Commit and push**: `docs(tessera): the guide, the sample's README and the notes of step 4`

### Task 13: The gates, the review and the report

**Files:**
- Modify: `docs/plan-step4.md`
- Create: `/mnt/project-files/tilemap/2026-11-2x-tessera-step4-report.md` *(the date of the day it is written)*

- [ ] **Step 1: The plan.** Every box of `docs/plan-step4.md` is ticked or has a reason, and each task has its "Done differently" note, even when it says "nothing".
- [ ] **Step 2: The gates**, each once, from the root of the checkout:
  - `dotnet test extensions/ColorRise.Tessera/ColorRise.Tessera.slnx`. Expected: PASS, step 3's 843 tests, the seven of Task 1 and those of the fixes, none skipped.
  - `dotnet test extensions/ColorRise.Tessera/sample/Thornwick/Thornwick.Tests`. Expected: PASS, none skipped.
  - `dotnet build extensions/ColorRise.Tessera/sample/Thornwick/Thornwick.Linux`. Expected: no error, and no warning that names an asset of Thornwick. The solution `Thornwick.slnx` is not built whole here: its Windows head builds on Windows.
  - `extensions/ColorRise.Tessera/tests/PackagesCheck/run.sh`. Expected: `TESSERA-PACKAGES-CHECK OK`.
  - `git diff --stat origin/feature/colorrise-tessera-flipbooks..HEAD -- . ':!extensions/ColorRise.Tessera'`. Expected: empty.
  - `git grep -n "ProjectReference" -- extensions/ColorRise.Tessera/sample`. Expected: only references to Tessera's projects and to the sample's own.
  - `du -sh extensions/ColorRise.Tessera/docs/guide extensions/ColorRise.Tessera/sample/Thornwick/Thornwick.Game/Resources`. Expected: the guide has 14 pictures at most, of 150 KB at most each, and the art is under 1 MB.
- [ ] **Step 3: The review.** One fresh reviewer of the whole branch, as for steps 1 to 3, given: the design, this plan with its notes, the part "Step 4" of `docs/studio-check.md`, the diff from `feature/colorrise-tessera-flipbooks`, and the five lines of Review Focus. It is also asked to read `docs/guide.md` as someone who has never seen Tessera, and to say where a step cannot be followed. Fix what it finds that is a defect, each with a test that fails first where a test can; list the minors that wait, numbered, in the report.
- [ ] **Step 4: The report**, for Nicolas: what was built; how to play it (three commands); the numbers (tests by project); which risks held and which fallbacks were taken; the findings; what waits (the five of step 2, the four of step 3 this step left, this step's, and the wish that `Sheet` reads a sheet with a column for each side); the pictures by name, with the town's, the house's and three of the game first. It ends with what the first version still lacks that the design names (section 15, a check on Windows, a repository of its own). Update the project's memory (`tilemap-plugin`).
- [ ] **Step 5: Commit and push**: `docs(tessera): step 4 written up: the plan's notes`
