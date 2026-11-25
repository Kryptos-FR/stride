# ColorRise.Tessera, step 3: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task, in one session, with one fresh reviewer of the whole branch at the end (Nicolas's choice for Tessera, 2026-11-24). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A character made in the flipbook editor walks in four directions in a game, and a tile animated in the tile set editor plays in a map.

**Architecture:** A flipbook is data the asset and the game share: animations, a clip per direction, frames of cells. The component keeps what plays by name and by time, so a flipbook reloaded under it changes nothing but the pictures. A second render feature draws flipbooks with the tile batch, in the same stage as the layers, and orders flipbooks of one depth by their Y through the state part of the sort key. In Game Studio, a canvas can now be told the time: the tile set editor plays animated tiles in place, the map editor plays them in its chunks, and the flipbook editor has a preview, a strip of frames and the palette of step 2.

**Tech Stack:** .NET 10, C# (latest), Stride 4.5.4-dev packages, Avalonia 12.1.1, xunit 2.9.3 with the editor test kit `Stride.Assets.Editor.Testing` for view models, `Avalonia.Headless.XUnit` on xunit.v3 with Skia for views, Lavapipe (software Vulkan) for graphics tests, Xvfb and xdotool for Game Studio by hand.

**Spec:** [design.md](design.md), approved by Nicolas on 2026-11-24, with its notes of 2026-11-25. "The design" below means this file, and "section N" is a section of it. This step is section 7 (*Flipbook asset*), section 8 (`FlipbookComponent`, *Y sort for flipbooks*, *Animated tiles*), section 9 (*Animation mode*, *Flipbook editor*, *In the scene editor*), section 11 (R2 and R7 for flipbooks), section 12 (item 3) and section 14. The plans of steps 1 and 2 are [plan.md](plan.md) and [plan-step2.md](plan-step2.md): their "Done differently" notes say what the code really is.

## Scope

Step 3 of the design's four (section 12), on `feature/colorrise-tessera-flipbooks`, from `feature/colorrise-tessera-editor` at `673c4e01a`: "Animated tiles in the tile set editor, the flipbook asset, compiler, component, drawing and editor." With them, as section 9 says: an entity factory `Create entity > 2D > Flipbook`, a drop policy for a flipbook asset, a template in *Add asset*, an icon for the asset and the component.

Not in this step:

- The sample game and Kenney's art (step 4). The by-hand check uses the check game of step 2, with a character sheet drawn by a script.
- Frames of several tiles in the flipbook editor (section 6). The data, the build and the drawing hold several cells, and a test draws a frame of two; the editor makes and shows frames of one cell, and keeps the other cells of a frame written by hand.
- A list of the flipbook's animation names in the component's property grid: `Animation` is typed. Frame events, a provider that shows a flipbook in a `SpriteComponent`, Y order shared with `SpriteComponent`s (section 15).
- A drag of tiles from the palette onto the strip of frames. A click or a drag in the palette adds the frames (decision 9).
- Views for the WPF Game Studio, painting in the scene view, real thumbnails.
- The five deferred minors of step 2's review that wait (decision 1).

## What step 3 decides

The brief of this step lists six things to decide. Three questions went to Nicolas on cards on 2026-11-25. He answered the three the same day, each with the option the plan was written on, so no task changed. **He approved the plan, with the calls 4 to 12 below, on 2026-11-25 (20:59, Paris): "Go".**

**Decided by Nicolas (D)**

1. **Which deferred minors of step 2's review go in.** **Answered on 2026-11-25: "Six: two needed, four cheap".** Task 1: six of the eleven. Two because this step builds on them: M5 (Solid mode finds each tile's definition by a linear search every frame, and Animation mode adds a second such look-up per tile) and M9 (bitmaps left to finalizers, when playing makes pictures far more often). Four because they are small and self-contained: M1 (a fill goes through a chunk the editor does not paint), M3 (a tile set whose pixels per unit is not above 0 draws as missing boxes with no reason), M6 (the grid bar slices the whole image at each key) and the build that throws on an empty chunk item. The five others wait: M2 (the spelling of a hand-edited chunk) and the two layers of one name are rulings that stand, M4 (the step that binds new sprites) needs a design of its own, and M10 and the cost of many new chunks are measurements.
2. **Whether the map editor plays animated tiles.** **Answered on 2026-11-25: "Play, with a toggle".** Task 12: the map editor plays them, with a button of the tool bar and a setting that stop it, as Tiled's "Show tile animations". Stopped, an animated tile shows its first frame, where step 2 showed the tile itself.
3. **Whether the scene view plays flipbooks and animated tiles.** **Answered on 2026-11-25: "Play them".** Nothing is built for it: the scene editor's game updates the entity processors (`EditorServiceGame.Update` calls the game's), so the clocks of Tessera's processors run there. Task 17 checks it by hand (risk R13).

**The planner's calls, approved with the plan (C)**

4. **The names** (section 16). The small types of a flipbook are one set of classes in the runtime, used by the asset and by the content: `FlipbookAnimation`, `FlipbookClip`, `FlipbookFrame`, `FlipbookCell`, with `FlipbookDirection` and `FlipbookLoopMode`. The asset does not have a second copy of them, as `TileDefinition` already uses the runtime's `TileAnimationFrame`. The asset and the component are as sections 7 and 8 write them, with these additions and precisions:
   - `FlipbookComponent.Animation` is both the one shown at start and the one playing now: `Play("walk")` sets it. `IsPlaying`, `Time`, `CurrentClip`, `CurrentFrameIndex` and `CurrentFrame` say where the playback is. `Advance(seconds)` moves it, which the processor calls each frame and a test calls directly. The two events are raised from `Advance` only.
   - `Play(name)` leaves a playing animation alone, resumes a paused one and starts an ended one again; `restart: true` always starts at 0. `Queue(name)` plays after the current pass of the current animation: at the end of a `Once`, at the next turn of a `Loop` or a `PingPong`. `Stop()` stops and goes back to the first frame.
   - A name the flipbook does not have, a flipbook without a tile set, a clip without a frame: nothing is drawn and nothing throws.
   - A direction without a clip falls back as section 7 says (the clip `None`, then the first clip). Before that, a diagonal tries its horizontal part, then its vertical part: `UpLeft` shows `Left`, then `Up`. This is an addition to section 7.
   - `FlipbookDirections.FromVector(move, diagonals)` gives the direction of a movement, and `Mirrored()` the direction mirrored left to right.
   - `PingPong` plays 1, 2, 3, 2 and again: the two end frames are shown once per pass. A frame whose duration is not above 0 is never shown, as for animated tiles.
5. **Two render features, not one** (section 8 says one). The engine finds the feature of a render object by its exact type (`RenderSystem.AddRenderObject` looks up `renderObject.GetType()`), so one root render feature draws one class of render objects. `FlipbookRenderFeature` ("Flipbooks") draws `RenderFlipbook`s, beside `TileRenderFeature`. Both use `TileBatch` and the same stage selector, and every feature has the sort key 128, so chunks, flipbooks and sprites still interleave by depth. *Add package features* adds the feature a compositor lacks (it skips by type). The other way, one class of render object for chunks and flipbooks, would rewrite step 1's `RenderTileChunk` for no gain a user sees. Finding 18.
6. **Order (R2).** A flipbook's sort key is its world Y, the Y of its entity, where its pivot is: 256 steps a unit, from -32768 to 32768 units, the higher Y drawn first. The key decides only between render objects of the same depth, so:
   - with `TileAxisSortMode`, flipbooks of one z order by Y under any camera, because their depth along the axis is exactly their z;
   - with Stride's `BackToFrontSortMode`, they order by Y under a camera that looks straight at the map, and by the distance to the camera's plane under a tilted one, as section 8 says;
   - a flipbook at a layer's own z is drawn over that layer (chunks have the key 0), and a `SpriteComponent` (key 0) at the z of flipbooks is drawn under them.
   The Y is the world's: a map turned into another plane does not get this order (README).
7. **Picking (R7)** as for maps: in the stage named `Picking`, the feature draws each cell's quad in the id of the component, and `TilePickingService` gives both features their selector. The whole rectangle of a frame answers the click, its transparent pixels too, as a tile does.
8. **Animation mode.** The third mode of the tile set editor.
   - The first click picks the tile to animate. Each click after it adds the clicked tile as a frame, after the selected frame or at the end, with the duration of the frame before it (0.2 s for the first). A tile can be a frame several times, and frames can come from another source of the same tile set: changing the source shown keeps the animated tile. Escape or `Done` lets go of the animated tile.
   - A bar under the canvas shows the frames in order, each with its picture and its duration. A frame selected there is removed by Delete or `x`, and moved by `<` and `>`. `Clear` removes them all.
   - Durations are edited in the property grid, as section 9 says: in this mode the grid shows the animated tile's data with its `Animation`, a list that cannot gain or lose items there and whose frames show their `Duration` only. If the grid cannot edit a struct inside a list of a copy (risk R11, tried first in Task 10), the bar gets a number box per frame instead.
   - Every animated tile of the source shown plays in place, in the three modes, and has a small corner mark; the frames of the animated tile carry their numbers. A clock of the canvas plays them: the Avalonia control ticks a scene that says it is running, 30 times a second, and the scene asks for a redraw only when a frame changed. A test moves the time itself.
9. **The flipbook editor**, laid out as section 9 draws it.
   - **The tree**: animations, and under each its clips by direction. `+` adds an animation with one clip `None`; `+ Direction` adds a clip for a direction the animation lacks; `-` removes the selected clip or animation (it asks first when there are frames); a name is typed in the tree. The strip and the preview show the selected clip, and selecting an animation selects its first clip.
   - **The palette adds frames**: a click adds the tile as a frame after the selected frames or at the end, and a drag adds the tiles of the block, row by row from the top, each row left to right. So a drag along a row of a character sheet adds a walk cycle in order. The design says "drop tiles from the palette": a drag out of a custom-drawn canvas onto another control cannot be driven in a headless test and would need the canvas to start a drag itself, so it waits.
   - **The strip**: frames with their picture, tile and duration; several can be selected; Delete or `x` removes them, `<` and `>` move them, and a drag moves them too (checked by hand). The property grid shows the selected frames: `Duration`, and the cell's `Offset` and `Flip`.
   - **The preview** plays the selected clip around a cross at the pivot: play and pause, a speed (25, 50, 100 or 200%), and `Loop`, which makes the preview start again at the end whatever the animation's loop mode. Without it the preview plays as the game will.
   - **Sheet** opens a bar, as adding an image does in the tile set editor: a block picked in the palette (while the bar is open, a pick sets the block and adds no frame), a direction or "skip" per row (Down, Left, Right, Up by default, RPG Maker's order; one row is `None`), the order ("In order" or "There and back", which is 1, 2, 3, 2), a duration (0.15 s) and the name of the animation. `Make` writes the clips of those directions in one step, into the animation of that name or a new one.
   - **Mirror** makes the clip of the mirrored direction from the selected one (Left from Right, and the diagonals alike): the same frames, each cell flipped left to right. It asks before it replaces a clip that has frames.
   - **A flipbook without a tile set** shows a `Pick a tile set` button where the palette goes.
   - A frame whose tile the set no longer has is a marked box in the strip and the preview, named in the editor's problems, and kept in the file.
10. **The scene editor's pieces**: `[Display(50, "Flipbook", "2D")]` after `Tile map` (40); a flipbook asset dropped in a scene makes an entity whose component names the flipbook and its first animation, so it shows at once; the template `Flipbook` in the group `Sprite`, whose new asset has one animation, `idle`; icons drawn by the script of step 2.
11. **The build of a flipbook** declares its tile set as a runtime dependency, as a map does, and does not read the tile set: a cell whose tile is gone is kept and draws nothing in the game. It warns about what it can see alone: no tile set, a name given twice or empty, two clips of one direction, an animation without a clip, a clip without a frame, a frame without a cell.
12. **The by-hand check** is Task 17, over step 2's check game, extended with a character sheet of 16 by 24 pixel frames (so a second tile size is seen), a flipbook made in the editor, an animated water tile, and a script that walks the character around a square by itself, so that screenshots at four times show four directions without a key pressed.

## Global Constraints

- Every file of this work is inside `extensions/ColorRise.Tessera/`. Nothing else in the repository changes, and nothing in the engine.
- Stride is reached through packages only, at `$(TesseraStrideVersion)` (`4.5.4-dev`). No `ProjectReference` to an engine project. The `src/` projects use no engine internal, by reflection or any other way. A test may, with a finding that says why.
- `ColorRise.Tessera.Editor` references no Avalonia package and no `*.Avalonia` assembly. Everything that names an Avalonia type is in `ColorRise.Tessera.Editor.Avalonia`.
- A gap in the plugin model is a row in `docs/plugin-findings.md`, in the format of the file. The next number is 18.
- All projects: `net10.0`, `LangVersion` latest, `Nullable` enable, `ImplicitUsings` enable, version `0.1.0-dev`, MIT, authors `Color-Rise`.
- Code: explicit access modifiers, classes sealed by default, the `field` keyword in place of a new private backing field, no licence header in source files, plain English in comments and documentation, `<summary>` on public types and members, `<userdoc>` on what the property grid shows. A comment says what the code is, not what it was.
- A render feature's `[DataContract]` says `DefaultMemberMode = DataMemberMode.Never` (finding 17).
- Commits: conventional titles with the scope `tessera`. Author `Nicolas Musset <musset.nicolas@gmail.com>` (run the two `git config` commands before the first commit). No `Co-Authored-By` line, no link to Claude, no model name anywhere.
- Branch `feature/colorrise-tessera-flipbooks`, from `origin/feature/colorrise-tessera-editor`. No other branch name, and no `-tmp` branch. Push it after each task: the container does not last.
- Nothing stays only in the conversation (Nicolas, 2026-11-24). In the commit of each task: the task's boxes ticked in `docs/plan-step3.md`, the result of its gate, a "Done differently" note under the task where what was done differs, and a row in `docs/plugin-findings.md` for each gap met. After each task, one line in the ledger (`/mnt/project-files/tilemap/2026-11-2x-tessera-step3-ledger.md`) and in the project's memory (`tilemap-plugin`). The ledger and the memory are written with the file tools, never with a shell command.
- An editor changes an asset only through the asset's graph, inside a transaction of the session's undo service with a name. The names of this step: `Add frame`, `Remove frame`, `Move frame`, `Clear frames` (the tile set editor); `Set tile set`, `Add animation`, `Remove animation`, `Rename animation`, `Add clip`, `Remove clip`, `Add frames`, `Remove frames`, `Move frames`, `Sheet`, `Mirror clip` (the flipbook editor). A change made in the property grid keeps the grid's own name (finding 12); tile data written after it is `Tile data`, as in step 2.
- The id of a command starts with `Tessera.` and never changes once it is pushed. No command of Tessera has a default shortcut. The six of this step: `Tessera.TileSet.Mode.Animation`, `Tessera.Map.ToggleAnimations`, `Tessera.Flipbook.PlayPause`, `Tessera.Flipbook.AddAnimation`, `Tessera.Flipbook.Sheet`, `Tessera.Flipbook.Mirror`.
- Every input of a `TileCanvas` implements `ICanvasInput.PointerCaptureLost`, and a button of a tool bar gives the focus back to its canvas (step 2's final review).
- Values from the design: the pivot of a flipbook is (0.5, 0), the bottom centre of the frame's first cell; a frame lasts 0.1 s by default; the directions are None, Down, Left, Right, Up and the four diagonals; the loop modes are Loop, Once and PingPong; flips are applied diagonal first, then horizontal, then vertical; the asset file is `.sdflipbook`.
- Values of this plan: the sort key of a flipbook has 256 steps a unit of world Y, from -32768 to 32768; a canvas ticks a running scene 30 times a second and gives it at most 0.25 s at once; a frame added in Animation mode lasts as the one before it, or 0.2 s; the Sheet bar proposes 0.15 s.
- Tests: what a view model does is tested in `ColorRise.Tessera.Editor.Tests`, on the editor test kit, without a control. Only what needs a control is in `ColorRise.Tessera.Editor.Avalonia.Tests`. No gold image: a test reads pixels at known places. A test of something that plays sets the time or calls `Advance`: it never waits for a timer, but for the one view test that shows the canvas ticks. The test classes of the two editor projects do not run in parallel.
- Each test is seen to fail before the code that makes it pass is written. No mutation testing (Nicolas, 2026-11-25).
- After a failed run, run again only what failed. The whole solution is run at the gates named in the tasks.
- The engine build and Game Studio's build are long runs. They run in the container, in the background, with their output in a log file: no check on GitHub builds an extension.
- Screenshots of what is checked by hand go to `/mnt/project-files/tilemap/step3/`, named `studio-NN-*.png` after the line of the list.
- A fallback that loses a goal of section 6 stops the work: write what was found and ask Nicolas.
- Engine names in this plan come from the fork's `master` at `659bb239e`, which the branch is built on. A name marked *(check)* was not read while planning: read the model file named beside it before relying on it.

## Review Focus

Inputs the design implies and that will reach this code, most likely first. Each has its test in the task named.

1. **A frame whose tile is gone, and a flipbook whose tile set is gone or not set** (an image cut again, a sprite removed, a tile set deleted). The editors open, the frame is a marked box in the strip and the preview and is named in the problems, the file keeps it, the build succeeds, and the game draws nothing for that cell without throwing. (Tasks 4, 5, 13 and 14)
2. **A name or a direction the flipbook does not have, from game code or from a hand edit**: `Play("wlak")`, a direction without a clip, an animation without a clip, a clip without a frame, durations of 0 or less, a time that is not finite. Nothing is drawn or the fallback clip is, the events stay quiet, and no update throws or loops for ever. (Tasks 2 and 3)
3. **Undo, redo or a change from elsewhere while something is selected or plays.** A frame removed while it is selected or shown, an animation removed while its clip is in the preview, the animated tile's source removed, the flipbook reloaded under a playing component: the selection that names things that are gone is cleared, the preview shows the first frame that is left, the component keeps its animation by name, and nothing throws. (Tasks 3, 10, 13 and 14)
4. **Frames that are not the size of a cell, with a pivot and flips.** A character 16 by 24 pixels in a tile set of 16 pixel tiles stands with its feet on its entity, `FlipX` mirrors it around the pivot and not around the tile's corner, and the editor's preview shows it as the game draws it. (Tasks 5 and 14)
5. **Two things of one name.** An animation renamed to the name of another or to nothing, two clips of one direction after a change in the property grid or a merge. The tree refuses the name with a message, the problems and the build name what the property grid let through, and the game plays the first. (Tasks 2, 4 and 13)

## Risks tried in this step

| # | Risk | Tried first | Fallback |
|---|---|---|---|
| R2 (flipbooks) | The Y order through `StateSortKey` needs two flipbooks of one z to have bit-equal depths | Task 6's tests, under both sort modes, straight and tilted | With `BackToFrontSortMode` under a tilted camera the distance orders them, as the design says; nothing else is promised |
| R7 (flipbooks) | A click on a flipbook in the scene view | Task 7's picking test, then Task 17 by hand | Selection through the entity tree only, as a finding |
| R11 | The property grid edits the `Duration` of a `TileAnimationFrame`, a struct in a list of a copy outside the asset, and `[MemberCollection(ReadOnly = true)]` keeps the frames' own members editable | Task 10, Step 1, as a test | A number box per frame in the bar of Animation mode, over `TileSetGraph.SetAnimation` |
| R12 | A scene that plays on a canvas: a timer in the control, and a redraw per changed frame only | Task 9: the tests move the time; one view test waits for the canvas to tick | The view model runs the timer through the session's dispatcher |
| R13 | The scene view plays, and follows a flipbook edited while its scene is open (R5 for flipbooks) | Task 17, lines 14 and 16 | A finding, and the README says to open the scene again |

## The pieces

What the tasks build, and who knows whom. The names are fixed here so that every task uses the same ones.

| Piece | Project | What it is |
|---|---|---|
| `FlipbookDirection`, `FlipbookDirections`, `FlipbookLoopMode` | runtime | The nine directions, their helpers, the three loop modes |
| `FlipbookCell`, `FlipbookFrame`, `FlipbookClip`, `FlipbookAnimation`, `Flipbook` | runtime | The data, shared by the asset and the content |
| `FlipbookComponent`, `FlipbookEventArgs`, `FlipbookProcessor` | runtime | What plays, and the processor that moves it each frame |
| `RenderFlipbook`, `FlipbookRenderProcessor`, `FlipbookRenderFeature`, `FlipbookQuads`, `FlipbookSortKey`, `TileBatchRun` | runtime | Drawing: the render object, its processor, the feature, the quads of a frame, the Y key, and the batch run both features share |
| `FlipbookAsset`, `FlipbookAssetFactory`, `FlipbookAssetCompiler`, `FlipbookAssetChecks` | Assets | The asset, what a template makes, its build and its warnings |
| `CanvasSprite.Replace`, `CanvasLabel`, `CanvasMark.Corner`, `ICanvasClock` | Editor | What a canvas gains: a sprite that hides what is under it, text, a corner mark, and time |
| `TileFrameItemViewModel`, the Animation mode of `TileSetEditorViewModel`, `TileSetGraph.SetAnimation` | Editor | Animated tiles in the tile set editor |
| `TileSetPictures.AnimationOf`, `TileMapScene` as a clock, `TesseraEditorSettings.PlayAnimations` | Editor | Animated tiles played in the map editor |
| `FlipbookViewModel`, `FlipbookGraph`, `FlipbookEditorViewModel` and its items, `FlipbookFrameComposer`, `FlipbookPreviewScene`, `PendingSheetViewModel` | Editor | The flipbook editor |
| `FlipbookEntityFactory`, `AddFlipbookAssetPolicy` | Editor | The scene editor's pieces |
| `PictureView`, `FlipbookEditorView` | Editor.Avalonia | A control that shows one picture, and the view of the flipbook editor |

---

### Task 0: The container, the branch and the plan

**Files:**
- Create: `docs/plan-step3.md` (a copy of this plan), `/mnt/project-files/tilemap/2026-11-2x-tessera-step3-ledger.md`

**Interfaces:**
- Produces: `dotnet` 10 on the `PATH`; the worktree `/home/user/stride-master` on the branch `feature/colorrise-tessera-flipbooks`; Stride's packages at `4.5.4-dev` in `/home/user/stride-master/bin/packages`; step 2's tests green; the Avalonia Game Studio built, for Task 17.

- [x] **Step 1: Set up the container as Task 0 of [plan-step2.md](plan-step2.md) says** (its steps 1 to 4, with the notes of what was done): the .NET 10 SDK in `~/.dotnet`; the worktree, this time `git worktree add -b feature/colorrise-tessera-flipbooks /home/user/stride-master origin/feature/colorrise-tessera-editor`; the author's two `git config` lines; `git lfs pull` and the restore of the one file it stages outside LFS; the three builds in the background with logs (the engine and the editor, the asset compiler, Game Studio).
- [x] **Step 2: See step 2's tests pass** before anything changes.

Run: `dotnet test extensions/ColorRise.Tessera/ColorRise.Tessera.slnx`, then `extensions/ColorRise.Tessera/tests/PackagesCheck/run.sh`
Expected: PASS, 537 tests (262, 239 and 36), none skipped, then `TESSERA-PACKAGES-CHECK OK`.

- [x] **Step 3: Copy this plan** to `extensions/ColorRise.Tessera/docs/plan-step3.md`, start the ledger with the three answers of Nicolas, commit (`docs(tessera): the plan of step 3`) and push with `git push -u origin feature/colorrise-tessera-flipbooks`.
- [x] **Step 4: Gate.** If the engine or Game Studio cannot be built in the container, stop and tell Nicolas what failed, with the end of the log.

**Done (2026-11-25).** .NET SDK 10.0.404. The engine and editor build took 4 min 6 s, the asset compiler 1 min 7 s, Game Studio 35 s, all in the container and without an error. The baseline: 262, 239 and 36 tests pass, none skipped, and `TESSERA-PACKAGES-CHECK OK` (23 s). The ledger is `/mnt/project-files/tilemap/2026-11-25-tessera-step3-ledger.md`.

**Done differently.** The plan was committed and pushed while the builds ran, to learn early that the push works, as in step 2.

### Task 1: Six deferred fixes of step 2's review (decision 1)

The six Nicolas picked. Each is a fix with a test that fails first, in the file that owns the code.

**Files:**
- Modify: `src/ColorRise.Tessera.Editor/TileSetEditor/TileSetEditorViewModel.cs` (M5), `TileSetEditor/PendingImageSourceViewModel.cs` (M6), `TileMapEditor/TileMapTools.cs` or `TilePainting.cs` (M1), `TileMapEditor/TileMapEditorViewModel.cs` (M3), `src/ColorRise.Tessera.Editor.Avalonia/Controls/PictureBitmaps.cs` (M9), `src/ColorRise.Tessera.Assets/TileMapAsset.cs` (the empty chunk item)
- Test: `TileSetEditorTilesTests.cs`, `TileSetEditorSourcesTests.cs`, `TileMapToolsTests.cs`, `TileMapEditorTests.cs`, `TileCanvasTests.cs`, `TileMapCompilerTests.cs`

**Interfaces:**
- Produces:

```csharp
// TileSetEditorViewModel: the definitions by tile, made again when the asset's Tiles change (in Follow)
private IReadOnlyDictionary<TileId, TileDefinition> Definitions { get; }

// PendingImageSourceViewModel
public const int OutlineLimit = 16384;      // above it the grid is counted, and its tiles are not outlined

// PictureBitmaps
internal static int LiveBitmaps { get; }    // bitmaps made and not disposed
internal static void Sweep();               // disposes the bitmaps of pictures that are gone; also run after each 64 bitmaps made
```

- [x] **Step 1: Write the failing tests**

```csharp
// M5, TileSetEditorTilesTests: the marks come from the look-up, which must follow the asset
[Fact] public async Task The_solid_marks_follow_a_change_made_outside_the_editor()
    // SetSolid through TileSetGraph in a transaction of the test, then an undo: Scene.Collect has the hatch, then has none

// M6, TileSetEditorSourcesTests
[Fact] public async Task A_grid_of_too_many_tiles_is_counted_and_not_outlined()
    // a 512 by 512 image proposed with a tile size of 2 by 2: Columns 256, Rows 256, the pending scene's frame has no shape,
    // and Problem is null; with 16 by 16 the frame has 1024 outlines

// M1, TileMapToolsTests
[Fact] public async Task A_fill_does_not_cross_a_chunk_that_cannot_be_read()
    // a frame of 48 by 16 cells, three chunks in a row, the middle one with a text that is not a chunk: a fill at (2, 2)
    // changes cells of the chunk (0, 0) only; none of the chunk (2, 0)

// M3, TileMapEditorTests
[Fact] public async Task A_tile_set_whose_pixels_per_unit_is_not_above_zero_is_named()
    // PixelsPerUnit 0 in the tile set asset: Problems has "The tile set 'Town' has a pixels per unit of 0: its tiles show as missing boxes."

// M9, TileCanvasTests
[AvaloniaFact] public void The_bitmap_of_a_picture_that_is_gone_is_disposed()
    // a picture made and drawn in a method that is not inlined, then GC.Collect, GC.WaitForPendingFinalizers and Sweep:
    // LiveBitmaps is back to its value before

// the empty chunk item, TileMapCompilerTests
[Fact] public void An_empty_chunk_item_fails_the_build_with_its_layer()
    // Layers: one layer "Ground" whose Chunks has a null item: the build fails with a line that has "Ground" and
    // "chunk 1 is empty", and no exception leaves the compiler
```

- [x] **Step 2: Run them to see them fail.** Run each test project with a filter on the six names. Expected: FAIL, each for its reason (the look-up test passes already if the marks are right today: then it is the net of the refactoring, say so in the ledger).
- [x] **Step 3: Write the fixes.** M5: `IsSolid` and every look-up of a visible tile read `Definitions`. M9: an entry keeps a weak reference to its picture; `Sweep` disposes the bitmaps whose picture was collected. M1: the fill reads an unreadable chunk's cells as a wall. M6: above `OutlineLimit` slices, the columns and rows come from arithmetic, not from slicing.
- [x] **Step 4: Run the tests of the six files.** Expected: PASS, with step 2's.
- [x] **Step 5: Commit and push**: `fix(tessera): six deferred minors of step 2's review`

**Done (2026-11-25).** Red: the look-up test passed already (the net of M5's refactoring, as foreseen); the map editor's problems were empty (M3); the fill wrote 2,0 into the far chunk (M1); the 512 by 512 grid of 2 pixel tiles gave 65,536 outlines (M6); the empty chunk item threw in the command ("Exception in command"); `LiveBitmaps` and `Sweep` did not compile (M9). Green: the six pass, and the whole solution passes, 263, 243 and 37.

**Done differently.** `Definitions` has a private setter: `Follow()` makes a new dictionary at each change of the asset, the first definition of a tile winning as in `TileSetAsset.GetDefinition`. `TilePainting.Fill` takes the wall as an optional `isWall` function, which the fill tool gives as "the document does not paint the chunk of this cell" (`TileMapDocument.IsUnreadable`, so a chunk given twice is a wall too). M6 counts the columns and rows always by arithmetic (the same rule as `TileSlicer.Grid`), and cuts the grid only for its outlines, up to `OutlineLimit` tiles. The empty chunk item is named "Layer 'Ground': the chunk 1 is empty." (counted from 1).

### Task 2: The data of a flipbook, and which frame a time shows

No GPU, no game: the types, the look-ups and the arithmetic.

**Files:**
- Create: `src/ColorRise.Tessera/FlipbookDirection.cs` (with `FlipbookDirections`), `FlipbookLoopMode.cs`, `FlipbookCell.cs`, `FlipbookFrame.cs`, `FlipbookClip.cs`, `FlipbookAnimation.cs`, `Flipbook.cs`
- Modify: `src/ColorRise.Tessera/Module.cs`, `src/ColorRise.Tessera/Tile.cs`
- Test: `tests/ColorRise.Tessera.Tests/FlipbookTests.cs`, `FlipbookClipTests.cs`, `ContentTests.cs`, `TileAnimationTests.cs`

**Interfaces:**
- Consumes: `TileId`, `TileFlip`, `TileSet` (step 1); the attributes of `TileMap` for a content type; `ContentTests.A_tile_set_comes_back_as_it_was_saved`, the model of a round trip.
- Produces:

```csharp
namespace ColorRise.Tessera;

public enum FlipbookDirection { None, Down, Left, Right, Up, DownLeft, DownRight, UpLeft, UpRight }

public static class FlipbookDirections
{
    /// The direction of a movement in the map's plane, y up. Zero gives None. Without diagonals, the larger part wins,
    /// and x wins a tie; with them, a movement within 22.5 degrees of an axis is that axis.
    public static FlipbookDirection FromVector(Vector2 movement, bool diagonals = false);
    /// Left for Right, UpLeft for UpRight, DownLeft for DownRight, and back; the others are themselves.
    public static FlipbookDirection Mirrored(this FlipbookDirection direction);
}

public enum FlipbookLoopMode { Loop, Once, PingPong }

[DataContract("FlipbookCell")]
public sealed class FlipbookCell
{
    [DataMember(10)] public TileId Tile { get; set; }
    [DataMember(20)] public Int2 Offset { get; set; }     // pixels, x right and y up, from where the frame's first cell is
    [DataMember(30)] public TileFlip Flip { get; set; }
}

[DataContract("FlipbookFrame")]
public sealed class FlipbookFrame
{
    [DataMember(10)] public float Duration { get; set; } = 0.1f;     // seconds
    [DataMember(20)] public List<FlipbookCell> Cells { get; } = [];
}

[DataContract("FlipbookClip")]
public sealed class FlipbookClip
{
    [DataMember(10)] public FlipbookDirection Direction { get; set; }
    [DataMember(20)] public List<FlipbookFrame> Frames { get; } = [];
    /// How long one pass lasts: the durations above 0 added up; for PingPong, there and back, each end frame once.
    public double GetPassLength(FlipbookLoopMode mode);
    /// The index in Frames of the frame shown at a time: -1 without a frame; the first frame when no frame lasts or the time is not finite.
    public int FrameAt(double time, FlipbookLoopMode mode);
}

[DataContract("FlipbookAnimation")]
public sealed class FlipbookAnimation
{
    [DataMember(10)] public string Name { get; set; } = "";
    [DataMember(20)] public FlipbookLoopMode LoopMode { get; set; }
    [DataMember(30)] public List<FlipbookClip> Clips { get; } = [];
    /// The clip of a direction; else, for a diagonal, of its horizontal part, then of its vertical part; else the clip None;
    /// else the first clip; null without a clip. Of two clips of one direction, the first.
    public FlipbookClip? FindClip(FlipbookDirection direction);
}

[DataContract]   // with the content and reference serializer attributes of TileSet
public sealed class Flipbook
{
    public TileSet? TileSet { get; set; }
    public Vector2 Pivot { get; set; } = new(0.5f, 0);
    public List<FlipbookAnimation> Animations { get; } = [];
    /// The first animation of a name, compared as it is written; null for a null name or a name it does not have.
    public FlipbookAnimation? FindAnimation(string? name);
}

// Tile.cs: the arithmetic of FrameAt, for the editors, which have the frames of a definition and no Tile
public static TileId FrameAt(IReadOnlyList<TileAnimationFrame> frames, TileId own, double time);
```

`FrameAt` in `Once`: a time at or after the pass length shows the last frame that lasts; a negative time shows the first. In `Loop` and `PingPong` a negative time counts back from the end of the pass, as `Tile.FrameAt` does. `Module.cs` gains `[assembly: AssetFileExtension(".sdflipbook", typeof(Flipbook))]`. Each member the property grid shows has a `<userdoc>`.

- [x] **Step 1: Write the failing tests**

```csharp
public sealed class FlipbookClipTests
{
    // a clip of three frames of 0.1, 0.2 and 0.3 s
    [Theory]
    [InlineData(FlipbookLoopMode.Loop, 0.0, 0)] [InlineData(FlipbookLoopMode.Loop, 0.1, 1)] [InlineData(FlipbookLoopMode.Loop, 0.29, 1)]
    [InlineData(FlipbookLoopMode.Loop, 0.3, 2)] [InlineData(FlipbookLoopMode.Loop, 0.6, 0)] [InlineData(FlipbookLoopMode.Loop, -0.05, 2)]
    [InlineData(FlipbookLoopMode.Once, 0.59, 2)] [InlineData(FlipbookLoopMode.Once, 0.6, 2)] [InlineData(FlipbookLoopMode.Once, 99.0, 2)]
    [InlineData(FlipbookLoopMode.Once, -1.0, 0)]
    [InlineData(FlipbookLoopMode.PingPong, 0.59, 2)] [InlineData(FlipbookLoopMode.PingPong, 0.6, 1)] [InlineData(FlipbookLoopMode.PingPong, 0.79, 1)]
    [InlineData(FlipbookLoopMode.PingPong, 0.8, 0)]
    public void The_frame_of_a_time(FlipbookLoopMode mode, double time, int expected)

    [Fact] public void The_pass_of_a_ping_pong_shows_its_end_frames_once()     // 0.6 for Loop and Once, 0.8 for PingPong; 0.3 for a ping-pong of two frames 0.1 and 0.2
    // Review Focus 2
    [Fact] public void A_frame_that_does_not_last_is_never_shown()             // durations 0.1, 0, -1, 0.1: the times 0.1 and 0.15 show the index 3
    [Fact] public void A_clip_whose_frames_do_not_last_shows_its_first()       // durations 0 and 0: FrameAt is 0 in the three modes, and GetPassLength is 0
    [Fact] public void A_clip_without_a_frame_shows_nothing()                  // FrameAt is -1
    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void A_time_that_is_not_finite_shows_the_first_frame(double time)
}

public sealed class FlipbookTests
{
    [Fact] public void A_direction_finds_its_clip()
    [Fact] public void A_diagonal_falls_back_to_its_horizontal_part_then_its_vertical_part()   // clips Left and Up: UpLeft gives Left; clips Up and Down: UpLeft gives Up
    [Fact] public void A_direction_without_a_clip_falls_back_to_None_then_to_the_first()
    [Fact] public void An_animation_without_a_clip_has_none()
    [Fact] public void Of_two_clips_of_one_direction_the_first_is_found()                     // Review Focus 5
    [Fact] public void An_animation_is_found_by_its_name_as_written()                          // "walk" is found, "Walk" and null are not; of two "walk", the first
    [Theory] // (1, 0) Right; (-1, 0.5) Left; (0.5, 1) Up; (0, -1) Down; (0, 0) None; (1, 1) Right without diagonals and UpRight with; (1, 0.3) Right with diagonals
    public void A_movement_gives_its_direction(float x, float y, bool diagonals, FlipbookDirection expected)
    [Fact] public void Mirrored_swaps_left_and_right()                                         // the nine values
}

// ContentTests
[Fact] public void A_flipbook_comes_back_as_it_was_saved()
    // a flipbook over a tile set saved before it, pivot (0.25, 0.5), "walk" PingPong with clips Down and Left, a frame of 0.15 s
    // with two cells (1:0,0 at (0, 0); 1:1,0 at (0, 16) flipped Horizontal): every value comes back, and TileSet is the loaded set

// TileAnimationTests
[Fact] public void The_frames_of_a_definition_give_the_same_tile_as_the_tile()
    // Tile.FrameAt(frames, own, t) equals the instance's FrameAt(t) at 0, 0.3, 0.8 and -0.1, and gives own for no frame
```

- [x] **Step 2: Run them to see them fail.** Run: `dotnet test extensions/ColorRise.Tessera/tests/ColorRise.Tessera.Tests --filter "FullyQualifiedName~Flipbook|FullyQualifiedName~ContentTests|FullyQualifiedName~TileAnimationTests"`. Expected: FAIL to compile.
- [x] **Step 3: Write the types.** `Tile.FrameAt(double)` calls the new static one.
- [x] **Step 4: Run the tests.** Expected: PASS.
- [x] **Step 5: Commit and push**: `feat(tessera): the data of a flipbook, and the frame a time shows`

**Done (2026-11-25).** Red: the test project did not compile. Green: the 59 tests of the filter pass, and the runtime project's 305.

**Done differently.** The arithmetic of both `Tile.FrameAt` and `FlipbookClip.FrameAt` is one internal class, `FrameTimeline`. It counts a time within a microsecond of the end of a frame as past it: a duration is a float, and the plan's rows (0.1, 0.3 and 0.6 s) land on the ends of frames whose float durations are a little more than those doubles, so a plain comparison showed the frame before. Step 1's tile tests use times on exact binary fractions and pass as before. A `Once` before 0 shows its first frame that lasts. `FlipbookDirections` has two internal helpers, `HorizontalPart` and `VerticalPart`, for the fallback of a diagonal.

### Task 3: The flipbook component, and what plays

**Files:**
- Create: `src/ColorRise.Tessera/FlipbookComponent.cs`, `FlipbookEventArgs.cs`, `FlipbookProcessor.cs`
- Test: `tests/ColorRise.Tessera.Tests/FlipbookComponentTests.cs`, `ContentTests.cs`

**Interfaces:**
- Consumes: Task 2. Model for a component with a logic processor and a render processor: the engine's `ModelComponent` (`[DefaultEntityComponentProcessor]` and `[DefaultEntityComponentRenderer]`). The render processor of the attribute comes in Task 5: until then the component has the first attribute only.
- Produces:

```csharp
[DataContract("FlipbookComponent")]
[Display("Flipbook", Expand = ExpandRule.Once)]
[ComponentCategory("2D")]
[DefaultEntityComponentProcessor(typeof(FlipbookProcessor))]
public sealed class FlipbookComponent : ActivableEntityComponent
{
    [DataMember(10)] public Flipbook? Flipbook { get; set; }
    [DataMember(20)] public string? Animation { get; set; }          // another name starts at the time 0; the same name changes nothing
    [DataMember(30)] public FlipbookDirection Direction { get; set; }   // a change keeps the time
    [DataMember(40)] [Display("Play on start")] public bool PlayOnStart { get; set; } = true;
    [DataMember(50)] public float Speed { get; set; } = 1;           // 0 or less stops the time
    [DataMember(60)] [Display("Flip X")] public bool FlipX { get; set; }
    [DataMember(70)] public Color4 Color { get; set; } = Color4.White;
    [DataMember(80)] public TileSampler Sampler { get; set; }
    [DataMember(90)] [Display("Sort by Y")] public bool SortByY { get; set; } = true;
    [DataMember(100)] [Display("Render group")] public RenderGroup RenderGroup { get; set; }

    [DataMemberIgnore] public bool IsPlaying { get; }
    [DataMemberIgnore] public double Time { get; set; }              // seconds since the animation started, at its speed
    [DataMemberIgnore] public FlipbookClip? CurrentClip { get; }     // of Animation and Direction, with the fallbacks; null when there is none
    [DataMemberIgnore] public int CurrentFrameIndex { get; }         // in CurrentClip.Frames; -1 when nothing is shown
    [DataMemberIgnore] public FlipbookFrame? CurrentFrame { get; }

    public void Play(string animation, bool restart = false);
    public void Queue(string animation);
    public void Pause();
    public void Resume();
    public void Stop();
    /// Moves the playback by a time, in seconds, times Speed, and raises the events. The processor calls it each frame.
    public void Advance(double seconds);

    public event EventHandler<FlipbookEventArgs>? FrameChanged;      // the frame shown is another one (clip or index)
    public event EventHandler<FlipbookEventArgs>? AnimationEnded;    // a Once reached its end, or a queued animation took the place of this one
}

public sealed class FlipbookEventArgs(string animation, FlipbookDirection direction, int frameIndex) : EventArgs
{
    public string Animation { get; }; public FlipbookDirection Direction { get; }; public int FrameIndex { get; }
}

/// Starts each flipbook component (playing when PlayOnStart) and advances the enabled ones by the game's time.
public sealed class FlipbookProcessor : EntityProcessor<FlipbookComponent>
```

The rules, beyond decision 4:

- The state is `Animation`, `Time`, `IsPlaying` and the queue. `CurrentClip` and `CurrentFrameIndex` are computed from them and from `Flipbook` each time they are read: nothing keeps an index or an object of the flipbook between two frames.
- `Play` and `Stop` empty the queue. `Animation` set to another name empties it too.
- `Advance` first adds the time (when playing, `seconds` above 0 and `Speed` above 0), then ends what ends: a `Once` at or past its pass stops there (`IsPlaying` false, `Time` the pass length) and raises `AnimationEnded` once; with something queued, the current animation ends at its pass (for a `Loop` or a `PingPong`, at the first pass boundary the time crossed, or at once when the pass length is 0 or there is no clip), `AnimationEnded` is raised for it, and the queued one starts with the time that was left over. Several queued animations can end in one call: each is taken at most once per call. Last, `FrameChanged` is raised when the clip or the index differs from the last one reported.
- `FlipbookEventArgs.Direction` is the direction of the clip shown, which a fallback can make differ from the component's.
- The processor calls `Advance(time.Elapsed.TotalSeconds)` on enabled components. When a component is added, it starts: `IsPlaying = PlayOnStart`, `Time = 0`.

- [x] **Step 1: Write the failing tests.** A helper makes a flipbook with `idle` (Loop, one clip None, frames 0.5 and 0.5), `walk` (Loop, clips Down and Left, three frames of 0.1 each) and `attack` (Once, clip None, two frames of 0.1), and a component on it that a test advances itself.

```csharp
[Fact] public void Play_shows_the_frames_in_time()                       // Play("walk"); Advance(0.15): index 1; Advance(0.1): 2; Advance(0.1): 0
[Fact] public void Speed_scales_the_time()                               // Speed 2, Advance(0.1): Time 0.2, index 2; Speed 0: the time stays
[Fact] public void A_change_of_direction_keeps_the_time()                // Advance(0.15), Direction = Left: Time is 0.15, CurrentClip is Left's, index 1
[Fact] public void Play_of_the_playing_animation_changes_nothing_and_restart_starts_again()
[Fact] public void Pause_holds_the_frame_and_resume_goes_on()
[Fact] public void Stop_goes_back_to_the_first_frame()
[Fact] public void A_once_animation_stops_on_its_last_frame_and_tells_once()   // attack: Advance(1): IsPlaying false, index 1, AnimationEnded once; Advance again: no more event
[Fact] public void Play_of_an_ended_animation_starts_it_again()
[Fact] public void A_queued_animation_follows_a_once()                   // Play("attack"), Queue("idle"), Advance(0.25): Animation is "idle", Time is 0.05, AnimationEnded named "attack"
[Fact] public void A_queued_animation_follows_the_current_pass_of_a_loop()   // Play("walk"), Advance(0.1), Queue("idle"), Advance(0.1): still walk; Advance(0.15): idle, Time 0.05
[Fact] public void Two_queued_animations_can_end_in_one_step()           // attack, then attack queued, then idle queued, Advance(0.5): idle
[Fact] public void Play_empties_the_queue()
[Fact] public void FrameChanged_tells_each_new_frame_once()              // the indices reported over five Advance(0.05) of walk: 0, 1, 2
[Fact] public void The_animation_set_at_start_plays_when_the_processor_adds_the_component()
    // an EntityManager-free check: the processor's start on a component with Animation "walk" and PlayOnStart false: index 0, IsPlaying false
// Review Focus 2
[Fact] public void A_name_the_flipbook_does_not_have_shows_nothing()     // Play("wlak"), Advance(1): CurrentClip null, index -1, no event, no throw
[Fact] public void A_component_without_a_flipbook_advances_without_a_throw()
[Fact] public void A_queue_behind_an_unknown_name_starts_at_once()
[Fact] public void An_animation_without_a_clip_or_a_frame_shows_nothing_and_ends_at_once_for_the_queue()
// Review Focus 3
[Fact] public void A_flipbook_replaced_while_playing_keeps_the_animation_by_name()
    // walk at Time 0.25 (index 2); Flipbook = another object whose walk has one frame: index 0, Time kept, no throw;
    // Flipbook = one without walk: index -1

// ContentTests
[Fact] public void A_flipbook_read_again_keeps_its_object_and_a_playing_component_shows_its_new_frames()
    // as A_map_read_again_keeps_its_object_and_tells: the flipbook saved with other frames and Content.Reload:
    // the same object, and a component on it at Time 0.25 shows the frame the new clip has there, without a throw
```

- [x] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~FlipbookComponentTests`. Expected: FAIL to compile.
- [x] **Step 3: Write the component, the event arguments and the processor.**
- [x] **Step 4: Run the tests.** Expected: PASS.
- [x] **Step 5: Commit and push**: `feat(tessera): the flipbook component: play, queue, pause and events`

**Done (2026-11-25).** Red: the test project did not compile (`FlipbookComponent` not found). Green: the 25 tests of the filter `FlipbookComponentTests|ContentTests` pass, and the runtime project's 325.

**Done differently.** The processor's start is an internal static `FlipbookProcessor.Start(component)`, which the EntityManager-free test calls; it resets the frame last reported, so the first `Advance` tells the first frame. The processor copies its components before it advances them, since a handler of an event may add or remove one. A queue goes on only while the animation plays or after a `Once` ended: a paused animation keeps its queue waiting, even when its name is unknown. A queued animation set in its place keeps the rest of the queue (the setter of `Animation`, which empties it, is not used there). `Content.Reload` of a flipbook replaces its lists in the same object without a copy of their items: the test passed with no special handling.

### Task 4: The flipbook asset and its build

**Files:**
- Create: `src/ColorRise.Tessera.Assets/FlipbookAsset.cs`, `FlipbookAssetFactory.cs`, `FlipbookAssetCompiler.cs`, `FlipbookAssetChecks.cs`
- Test: `tests/ColorRise.Tessera.Tests/Assets/FlipbookAssetYamlTests.cs`, `FlipbookCompilerTests.cs`, `FlipbookAssetChecksTests.cs`, `Golden/hero.sdflipbook`

**Interfaces:**
- Consumes: Task 2; `TileMapAssetCompiler` (the model: a runtime dependency on `TileSet`, a command that saves the content, warnings in `Prepare`); `TileMapAssetYamlTests` and `AssetBuild` (the models of the tests); `TileIdYamlSerializer`, which already writes a `TileId` as `1:3,4` wherever one is a member.
- Produces:

```csharp
[DataContract("FlipbookAsset")]
[AssetDescription(FileExtension)]
[AssetContentType(typeof(Flipbook))]
[AssetFormatVersion("ColorRise.Tessera", "1.0.0.0")]
[Display(100, "Flipbook")]
public sealed class FlipbookAsset : Asset
{
    public const string FileExtension = ".sdflipbook";
    [DataMember(10)] [Display("Tile set")] public TileSet? TileSet { get; set; }
    [DataMember(20)] public Vector2 Pivot { get; set; } = new(0.5f, 0);
    [DataMember(30)] public List<FlipbookAnimation> Animations { get; } = [];
    /// The content: the tile set's reference, the pivot, and copies of the animations.
    public Flipbook ToFlipbook();
}

/// What a template makes: one animation, "idle", with one clip None and no frame.
public sealed class FlipbookAssetFactory : AssetFactory<FlipbookAsset> { public const string FirstAnimationName = "idle"; }

[AssetCompiler(typeof(FlipbookAsset), typeof(AssetCompilationContext))]
public sealed class FlipbookAssetCompiler : AssetCompilerBase     // GetRuntimeTypes: TileSet

internal static class FlipbookAssetChecks
{
    /// What is wrong in a flipbook that the asset alone shows, one line each, with the names.
    public static IEnumerable<string> Of(FlipbookAsset asset);
}
```

The lines of `FlipbookAssetChecks.Of`, each a warning of the build and a line of the editor's problems:

- `The flipbook has no tile set: it draws nothing.`
- `An animation has no name: game code cannot play it.`
- `The name 'walk' is given to 2 animations: the first is played.`
- `The animation 'walk' has no clip.`
- `The animation 'walk' has 2 clips for Left: the first is played.`
- `The clip Left of 'walk' has no frame.`
- `The frame 3 of the clip Left of 'walk' has no cell.` (frames are counted from 1)

- [x] **Step 1: Write the failing tests**

```csharp
public sealed class FlipbookAssetYamlTests
{
    [Fact] public void The_golden_file_loads_and_saves_to_the_same_bytes()      // Golden/hero.sdflipbook, written by this test's first run and read by a person
    [Fact] public void A_cell_is_written_with_its_tile_as_text()                // the saved text has "Tile: 1:0,0" and "Flip: Horizontal"
    [Fact] public void A_new_flipbook_has_one_animation_idle_with_a_clip_None()
}

public sealed class FlipbookAssetChecksTests
{
    [Fact] public void A_flipbook_that_is_right_has_no_line()
    [Theory] public void Each_thing_wrong_has_its_line(...)                     // the seven lines above, word for word
}

public sealed class FlipbookCompilerTests
{
    [Fact] public void A_flipbook_builds_to_content_that_names_its_tile_set()   // AssetBuild: the loaded Flipbook has the pivot, the animations, and a TileSet with the tiles of the set built with it
    [Fact] public void The_warnings_do_not_fail_the_build()                     // two clips for Left and a clip without a frame: the build succeeds, with both lines
    // Review Focus 1
    [Fact] public void A_flipbook_without_a_tile_set_builds_with_a_warning()
    [Fact] public void A_cell_whose_tile_the_set_does_not_have_is_kept()        // 1:9,9: the build succeeds and the loaded frame has the cell
    [Fact] public void A_second_build_runs_no_command()                         // as the map's test of the same name
    [Fact] public void A_change_of_a_frame_builds_the_flipbook_again()          // a duration changed: one command runs, and the tile set's does not
}
```

- [x] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~Flipbook`. Expected: FAIL to compile.
- [x] **Step 3: Write the asset, the factory, the checks and the compiler.**
- [x] **Step 4: Run the tests, and read the golden file.** Expected: PASS. Copy the golden file's text into the ledger: it is what a person sees in a diff.
- [x] **Step 5: Commit and push**: `feat(tessera): the flipbook asset and its build`

**Done (2026-11-25).** Red: the test project did not compile (`FlipbookAsset` not found). Green: the 22 tests of the three classes pass, the 83 of the filter `Flipbook`, and the runtime project's 347. The golden file `Golden/hero.sdflipbook` was written by the first run and read: one line per value, the tile set as `id:Tiles/Hero`, a cell as `Tile: 1:0,0`, `Offset` and `Flip`; its text is in the ledger.

**Done differently.** `FlipbookAssetChecks` is public (a ruling of the pre-flight: the editor of Task 13 reads it, and the Assets project shows its internals to the runtime tests only), and `Of` gives an `IReadOnlyList<string>`, as `TileAssetChecks.Of` does. Every item of the four lists keeps an id in the file, as the frames of a tile's animation do in a tile set: Game Studio's undo, the property grid and a merge work on items by id. Past the names, only what game code plays is checked: the first animation of each name and the first clip of each direction in it, so that each line names one thing; an animation without a name is told once and its clips are not checked. `ToFlipbook` copies the animations, clips, frames and cells, and leaves out the empty items a merge can leave in a list (`~`), so that `Flipbook.FindAnimation` and the component never meet a null. The compiler's tests use the tile set of `TileSetCompilerTests.RedAndBlue`, now internal, as an item that is not a root: the flipbook brings it in the build. Two tests beyond the plan: the content's objects are its own, and the empty items.

### Task 5: Drawing a flipbook

**Files:**
- Create: `src/ColorRise.Tessera/Rendering/RenderFlipbook.cs`, `FlipbookRenderProcessor.cs`, `FlipbookRenderFeature.cs`, `FlipbookQuads.cs`, `TileBatchRun.cs`
- Modify: `Rendering/TileRenderFeature.cs` (its nested batch run and its rasterizer helper move to `TileBatchRun`), `FlipbookComponent.cs` (the second attribute), `tests/.../Graphics/GraphicsHarness.cs` (the harness's compositor gets the second feature, with the selector of the first)
- Test: `tests/ColorRise.Tessera.Tests/Graphics/FlipbookDrawTests.cs`, `FlipbookQuadsTests.cs`, `Assets/TileRenderFeatureYamlTests.cs`

**Interfaces:**
- Consumes: Tasks 2 and 3; `TileBatch`, `TileQuad`, `TileQuads.Uvs`, `TileTransparentRenderStageSelector` (it puts any render object in its stage, so both features use it); the engine's `SpriteRenderProcessor`, the model of a render processor with one render object per component.
- Produces:

```csharp
namespace ColorRise.Tessera.Rendering;

public sealed class RenderFlipbook : RenderObject { public FlipbookComponent Component { get; } }     // Source is the component

/// Keeps one RenderFlipbook in the visibility group for each enabled flipbook component that shows a frame.
public sealed class FlipbookRenderProcessor : EntityProcessor<FlipbookComponent, RenderFlipbook>, IEntityComponentRenderProcessor

[DataContract("FlipbookRenderFeature", DefaultMemberMode = DataMemberMode.Never)]
[Display("Flipbooks")]
public sealed class FlipbookRenderFeature : RootRenderFeature        // SupportedRenderObjectType: RenderFlipbook
{
    internal int DrawnFlipbooks { get; }
}

internal static class FlipbookQuads
{
    /// The quad of one cell of a frame in the world, and its tile; false when the set lacks the tile, the tile has no
    /// texture, or the set's pixels per unit is not above 0.
    public static bool TryCell(Flipbook flipbook, FlipbookFrame frame, int cell, in Matrix world, bool flipX, out TileQuad quad, out Tile tile);
    /// The box of the cells of a frame that can be drawn, in the world; null when none can.
    public static BoundingBoxExt? Bounds(Flipbook flipbook, FlipbookFrame frame, in Matrix world, bool flipX);
}

/// One batch for each run of quads of the same colour, sampler and alpha, in a stage or in the picking stage.
internal struct TileBatchRun    // what TileRenderFeature.BatchRun is today, with Rasterizer(RenderOutputDescription) beside it
```

Where a frame is drawn, in the entity's plane z = 0, y up, with `p` the set's pixels per unit and `(w0, h0)` the size in pixels of the first cell's tile (the set's `TileSize` when the set lacks that tile):

- the first cell's bottom-left corner is at `-(Pivot.X * w0, Pivot.Y * h0) / p`, so the pivot is on the entity;
- a cell of a tile `(w, h)` with the offset `(ox, oy)` has its bottom-left corner `(ox, oy) / p` from there, and is `(w, h) / p` wide and high;
- `FlipX` negates the x of the four corners, which mirrors the frame around the vertical line through the pivot; the texture coordinates are those of `TileQuads.Uvs` for the cell's flips, unchanged;
- then the entity's world matrix.

The colour is the component's, the sampler the component's, the alpha what the set says, as for a chunk. The render processor sets the render group, the bounds and (Task 6) the sort key in `Draw`, adds the render object when the component is enabled and `Bounds` is not null, and removes it otherwise.

- [x] **Step 1: Write the failing tests.** The scenes use `TestTiles.Flat` (tiles of 4 pixels, one unit wide) and a new `TestTiles.Tall(device, colors)`: tiles 4 pixels wide and 6 high over a set whose tile size is 4 by 4 and pixels per unit 4. The view is 4 units high in 64 pixels: 16 pixels a unit.

```csharp
public sealed class FlipbookQuadsTests            // no GPU: the tiles have a region and a stand-in texture is not needed for Bounds
{
    [Fact] public void The_pivot_of_the_first_cell_is_on_the_entity()         // pivot (0.5, 0), a tile one unit wide: corners x from -0.5 to 0.5, y from 0 to 1
    [Fact] public void A_tall_tile_stands_on_its_feet()                       // Review Focus 4: 4 by 6 pixels at 4 a unit: y from 0 to 1.5
    [Fact] public void A_second_cell_is_placed_by_its_offset_in_pixels()      // offset (0, 4): its bottom is at y 1
    [Fact] public void FlipX_mirrors_around_the_pivot()                       // pivot (0, 0): x from -1 to 0, and the left corners carry the right texture coordinates
    [Fact] public void The_bounds_cover_the_cells_that_can_be_drawn()
}

[Collection(GraphicsCollection.Name)]
public sealed class FlipbookDrawTests
{
    [Fact] public void A_flipbook_draws_its_frame_with_its_pivot_on_the_entity()
        // entity at (0, 0), a red tile: the pixel just above the centre of the frame is red, the one just under it is the background
    [Fact] public void The_frame_of_the_time_is_drawn()                       // frames R, G, B of 0.1 s; the times 0, 0.15, 0.25 through Time, PlayOnStart false: R, G, B
    [Fact] public void The_game_plays_a_flipbook_that_plays_on_start()        // after four frames of the harness Time is above 0
    [Fact] public void FlipX_draws_the_frame_mirrored()                       // the quarters tile: its left and right colours swap
    [Fact] public void A_cell_flip_turns_the_tile()                           // Horizontal on the cell gives the same picture as FlipX with the pivot (0.5, 0)
    [Fact] public void A_frame_of_two_cells_draws_both()                      // head over body: two colours, one above the other
    [Fact] public void The_colour_of_the_component_tints_the_frame()
    [Fact] public void A_disabled_component_is_not_drawn()
    // Review Focus 1
    [Fact] public void A_cell_whose_tile_is_gone_draws_nothing_and_the_others_are_drawn()
    [Fact] public void A_flipbook_without_a_tile_set_draws_nothing()          // no throw over three frames
}

// TileRenderFeatureYamlTests
[Fact] public void A_compositor_asset_keeps_only_the_selectors_of_the_flipbook_feature()   // as the tile feature's test: finding 17
```

- [x] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~FlipbookQuadsTests|FullyQualifiedName~FlipbookDrawTests|FullyQualifiedName~TileRenderFeatureYamlTests`. Expected: FAIL to compile.
- [x] **Step 3: Move the batch run out of `TileRenderFeature`**, and run step 1 and 2's graphics tests (`FullyQualifiedName~Graphics`). Expected: PASS, as before the move.
- [x] **Step 4: Write the quads, the render object, the render processor and the feature**, and give the harness's compositor the feature.
- [x] **Step 5: Run the tests.** Expected: PASS. Save the frames of the first, the mirrored and the two-cell tests to `/mnt/project-files/tilemap/step3/frames/`.
- [x] **Step 6: Commit and push**: `feat(tessera): draw flipbooks with a render feature of their own`

**Done (2026-11-25).** Red: the test project did not compile (`FlipbookRenderFeature` and `FlipbookQuads` not found). The move of the batch run: the 57 graphics tests of steps 1 and 2 pass (the new tests set aside while they ran). Green: the 19 tests of the filter pass, and the runtime project's 365. The frames `flipbook-pivot.png`, `flipbook-flipx.png` and `flipbook-two-cells.png` are in `/mnt/project-files/tilemap/step3/frames/`.

**Done differently.** `FlipbookQuadsTests` runs without a GPU, where a texture that stands in has no size: it checks the corners, and that the mirrored frame carries the texture coordinates of the other side is checked by the draw tests (`FlipX_draws_the_frame_mirrored` and `A_cell_flip_turns_the_tile`). Mirrored, a quad keeps its corners in their places on screen (the bottom-left corner is the one at the left) and its left corners take the texture coordinates of the right ones; the quad is the same on screen as with the x of its corners negated. `Bounds` counts the cells `TryCell` would draw, so a tile without a texture is out of the bounds too; the quads tests give their tiles a texture that stands in. The render object keeps what the processor saw before the frame (the flipbook, the frame, the world, the mirror, the colour and the sampler), so that the feature reads no component while it draws. `FlipbookDrawTests` has a pivot test on a tall tile (its four edges), and its disabled test reads `DrawnFlipbooks` (1, then 0). Two quads tests beyond the plan: the entity's world, and the cells that cannot be drawn. The feature does not draw in the picking stage yet (Task 7).

### Task 6: Order: between the layers by z, among flipbooks by Y (risk R2)

**Files:**
- Create: `src/ColorRise.Tessera/Rendering/FlipbookSortKey.cs`
- Modify: `Rendering/FlipbookRenderProcessor.cs`
- Test: `tests/ColorRise.Tessera.Tests/FlipbookSortKeyTests.cs`, `Graphics/FlipbookOrderTests.cs`

**Interfaces:**
- Consumes: Task 5; `HarnessOptions.TransparentSortMode`, `TileMapComponent.GetLayerZ`, and the tilted camera of `AxisSortTests`.
- Produces:

```csharp
internal static class FlipbookSortKey
{
    public const float StepsPerUnit = 256;
    public const float Range = 32768;
    /// The state sort key of a flipbook at a world y: smaller for a higher y, so that it is drawn first.
    /// (uint)Math.Clamp((Range - y) * StepsPerUnit, 0, 16777215) << 8; 0 for a y that is not a number.
    public static uint FromY(float y);
}
```

The render processor sets `StateSortKey` to `FromY` of the y of the entity's world position when `SortByY`, and to 0 otherwise.

- [x] **Step 1: Write the failing tests.** The two flipbooks of the order tests are one unit wide and high, of flat colours, and overlap by half: `high` at (0, 0.5) and `low` at (0, 0), so the lower half of `high` and the upper half of `low` share pixels. Each test adds them in both orders (a theory with `lowFirst`), so that the order of adding cannot pass for the order of Y.

```csharp
public sealed class FlipbookSortKeyTests
{
    [Fact] public void A_higher_y_gives_a_smaller_key()               // FromY(1) < FromY(0) < FromY(-1)
    [Fact] public void A_step_is_a_256th_of_a_unit()                  // FromY(0) - FromY(1 / 256f) is 1 << 8
    [Fact] public void The_low_byte_is_empty()                        // sort modes drop it
    [Theory] [InlineData(40000f)] [InlineData(-40000f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void A_y_out_of_range_is_clamped(float y)                  // 0, 16777215 << 8, 0, 0
}

[Collection(GraphicsCollection.Name)]
public sealed class FlipbookOrderTests
{
    [Theory] [InlineData(true)] [InlineData(false)]
    public void Of_two_flipbooks_of_one_z_the_lower_is_drawn_over(bool lowFirst)            // the default sort, the camera straight: the shared pixels are low's
    [Theory] [InlineData(true)] [InlineData(false)]
    public void The_order_by_y_holds_under_a_tilted_camera_with_the_axis_sort(bool lowFirst) // TileAxisSortMode, the camera of AxisSortTests
    [Theory] [InlineData(true)] [InlineData(false)]
    public void A_flipbook_in_front_wins_whatever_its_y(bool lowFirst)                       // high at z 0.05: the shared pixels are high's
    [Fact] public void Without_sort_by_y_a_flipbook_is_drawn_under_those_that_sort()         // high with SortByY false, added last: the shared pixels are still low's
    [Fact] public void A_flipbook_at_the_z_of_a_layer_is_drawn_over_that_layer()             // a map of two layers; the flipbook at GetLayerZ("Layer0"): over Layer0's tile, under Layer1's
    [Theory] [InlineData(false)] [InlineData(true)]
    public void A_flipbook_between_two_layers_keeps_its_place(bool axisSort)                 // R2's test with a flipbook in the sprite's place; straight camera for the default sort, tilted for the axis sort
    [Fact] public void A_sprite_at_the_z_of_flipbooks_is_drawn_under_them()                  // a SpriteComponent and a flipbook at one z, overlapping: the flipbook's colour
    [Theory] [InlineData(0, 1, 2)] [InlineData(2, 1, 0)] [InlineData(1, 2, 0)]
    public void Three_flipbooks_of_one_z_are_drawn_from_the_highest_to_the_lowest(int first, int second, int third)
        // section 11's test of R2: three at y 1, 0.5 and 0, each overlapping the next, added in the order given, under the straight
        // camera with the default sort and under the tilted one with the axis sort: each shared band has the colour of the lower one
}
```

- [x] **Step 2: Run them to see them fail.** Expected: FAIL to compile, then, with the key left at 0, the two `lowFirst: true` rows of the first theory fail.
- [x] **Step 3: Write the key, and set it in the render processor.**
- [x] **Step 4: Run the tests.** Expected: PASS. If the rows under the tilted camera with the axis sort fail, print the two depths the sort mode computes: they must be bit-equal for one z (R2). If they are not, make the bounds of a flat frame keep the entity's z exactly, and say so in the ledger.
- [x] **Step 5: Write it up.** `README.md`: the order of flipbooks (decision 6), with what each sort mode gives. `docs/design.md`, section 16: 256 steps a unit.
- [x] **Step 6: Commit and push**: `feat(tessera): order flipbooks of one depth by their y`

**Done (2026-11-25).** Red: the test project did not compile; then, with the key left at 0, six rows failed: both `lowFirst: true` rows of the two first theories, two orders of the three flipbooks, the sprite and *Sort by Y* off. Green: the 26 tests of the two classes pass, the rows under the tilted camera with the axis sort among them (R2 holds: no fallback), and the runtime project's 391.

**Done differently.** The layer test is a theory over the order of adding (the flipbook first or the map first): added after the map, it passed with the key at 0 by the order alone. Each order test draws one frame per point it reads, the camera looking at that point, so that a tilted camera needs no projection in the test. `FromY` computes in double, so that a y on a 256th of a unit gives its exact step.

### Task 7: Compositors and a click in the scene view (risk R7)

**Files:**
- Modify: `src/ColorRise.Tessera/Rendering/FlipbookRenderFeature.cs` (the picking stage), `src/ColorRise.Tessera.Editor/Rendering/TileRenderFeatureProvider.cs`, `src/ColorRise.Tessera.Editor/Scene/TilePickingService.cs`
- Test: `tests/ColorRise.Tessera.Tests/Graphics/FlipbookPickingTests.cs`, `tests/ColorRise.Tessera.Editor.Tests/TilePickingServiceTests.cs`, `SceneEditorTests.cs`
- Modify: `docs/plugin-findings.md` (finding 18, and finding 16 now names two features)

**Interfaces:**
- Consumes: Task 5; `TileRenderFeature.PickingStageName`, `TileBatch.BeginPicking`, `HarnessOptions.Picking` and `Frame.PickedId` (step 2, Task 16).
- Produces: `TileRenderFeatureProvider.CreateRenderFeatures` gives a `TileRenderFeature` and a `FlipbookRenderFeature`, each with a `TileTransparentRenderStageSelector` on the transparent stage. `TilePickingService.AddPickingSelector(compositor)` gives each of the two features the compositor has a selector for the picking stage, once, and is true when there is a picking stage and at least one of the features.

- [x] **Step 1: Write the failing tests**

```csharp
[Collection(GraphicsCollection.Name)]
public sealed class FlipbookPickingTests
{
    [Fact] public void A_frame_gives_the_id_of_its_component()                 // inside the frame: RuntimeIdHelper.ToRuntimeId(component); beside it: 0
    [Theory] [InlineData(true)] [InlineData(false)]
    public void A_flipbook_in_front_of_a_map_wins_the_click(bool flipbookFirst) // over the map's tile, the flipbook at a nearer z: its id; the map's id beside the frame
    [Fact] public void A_flipbook_behind_a_tile_gives_the_map()
}

// TilePickingServiceTests
[Fact] public void Both_features_get_their_selector_once()
[Fact] public void A_compositor_with_the_flipbook_feature_alone_gets_its_selector()

// SceneEditorTests
[Fact] public void The_provider_gives_both_features_for_the_transparent_stage()
```

- [x] **Step 2: Run them to see them fail.** Expected: the picking tests read 0 inside the frame; the two others FAIL on the count of features.
- [x] **Step 3: Write the picking draw, the provider's second feature and the service's loop.**
- [x] **Step 4: Run the tests.** Expected: PASS, with step 2's picking tests.
- [x] **Step 5: Write finding 18**: a root render feature draws one exact type of render object (`RenderSystem.AddRenderObject`), so a plugin with two kinds needs two features, and a game's compositor two entries; what would close it: a look-up that accepts a base type. Finding 16's row gains the second feature.
- [x] **Step 6: Commit and push**: `feat(tessera): flipbooks in compositors and under a click in the scene view`

**Done (2026-11-25).** Red: the picking tests read 0 inside the frame (the frame drawn in its colour, blue), and the three editor tests failed on the count of features. Green: the 10 picking tests (with step 2's), the 10 of the service and the scene editor, and the whole solution: 395, 245 and 37.

**Done differently.** The harness's picking stage gets a selector on the flipbook feature as well, as the service gives it. `AddPickingSelector` takes the first feature of each of the two types the compositor has. `SceneEditorTests.The_provider_gives_the_tile_feature_for_the_transparent_stage` became `The_provider_gives_both_features_for_the_transparent_stage`.

### Task 8: A flipbook in Game Studio: its view model, template, images, entity factory and drop

**Files:**
- Create: `src/ColorRise.Tessera.Editor/ViewModels/FlipbookViewModel.cs`, `Scene/FlipbookEntityFactory.cs`, `Scene/AddFlipbookAssetPolicy.cs`, `src/ColorRise.Tessera.Assets/Templates/Assets/Flipbook.sdtpl`, `.sdtpl/Flipbook.png`, `Resources/FlipbookThumbnail.png`, `Resources/FlipbookComponent.png`
- Modify: `src/ColorRise.Tessera.Assets/EditorImages.cs`, `Resources/draw-icons.py`, `ColorRise.Tessera.Assets.sdpkg` (the template), `tests/ColorRise.Tessera.Editor.Tests/TesseraTest.cs` (`FlipbookAt`)
- Test: `tests/ColorRise.Tessera.Editor.Tests/TemplateTests.cs`, `SceneEditorTests.cs`

**Interfaces:**
- Consumes: Task 4; step 2's `TileMapViewModel`, `TileMapEntityFactory`, `AddTileMapAssetPolicy`, `TileMap.sdtpl` and their tests, the models of each piece.
- Produces:

```csharp
[AssetViewModel<FlipbookAsset>]
public sealed class FlipbookViewModel : AssetViewModel<FlipbookAsset>

[Display(50, "Flipbook", "2D")]
public sealed class FlipbookEntityFactory : EntityFactory          // an entity "Flipbook" with a FlipbookComponent

/// A flipbook asset dropped in a scene: an entity whose component names the flipbook and its first animation.
public sealed class AddFlipbookAssetPolicy : CreateComponentPolicyBase<FlipbookAsset, FlipbookViewModel>
```

The template: `Name: Flipbook`, `Group: Sprite`, `DefaultOutputName: Flipbook`, `AssetTypeName: FlipbookAsset`, `FactoryTypeName: FlipbookAssetFactory`, `Description: Animations of a character or an object, made of the tiles of a tile set`, a new `Id`, `Icon: .sdtpl/Flipbook.png`. The script draws three more images in the manner of the others: three small frames of a figure in a row.

- [x] **Step 1: Write the failing tests**

```csharp
// TemplateTests
[Fact] public async Task A_flipbook_has_a_view_model_of_its_own()
[Fact] public async Task The_flipbook_template_makes_a_flipbook_with_the_animation_idle()
[Fact] public void The_package_file_lists_the_three_templates()                 // replaces The_package_file_lists_both_templates
[Fact] public void The_images_are_declared_and_embedded()                       // gains the flipbook's thumbnail and the component's icon

// SceneEditorTests
[Fact] public void The_flipbook_factory_is_in_the_2D_category_after_Tile_map()
[Fact] public async Task Create_entity_makes_an_entity_with_a_flipbook_component()
[Fact] public async Task A_dropped_flipbook_becomes_a_component_that_names_it_and_its_first_animation()
[Fact] public async Task A_dropped_flipbook_without_an_animation_has_no_animation_set()
```

- [x] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TemplateTests|FullyQualifiedName~SceneEditorTests`. Expected: FAIL to compile.
- [x] **Step 3: Write the pieces, and draw the images.**
- [x] **Step 4: Run the tests.** Expected: PASS.
- [x] **Step 5: Commit and push**: `feat(tessera): flipbooks in Add asset and in the scene editor`

**Done (2026-11-25).** Red: the editor test project did not compile (`FlipbookViewModel` not found). Green: the 16 tests of the filter pass, and the editor project's 251. The script drew the three images; the five images of steps 1 and 2 came out byte for byte the same.

**Done differently.** The 16 pixel icon of the component shows the three figures of the frames side by side, dark in a light edge, without the frames, which do not fit at that size; the thumbnail and the template's image have the three frames. The drop policy takes the first animation that is not an empty item. The two drop tests share a helper that opens the session with the asset given.

### Task 9: A canvas that knows the time, writes text and replaces what is under a sprite (risk R12)

What the three editors of this step need from the canvas of step 2, before any of them is written.

**Files:**
- Create: `src/ColorRise.Tessera.Editor/Canvas/ICanvasClock.cs`
- Modify: `src/ColorRise.Tessera.Editor/Canvas/CanvasFrame.cs`, `Palette/TileSourceScene.cs`, `src/ColorRise.Tessera.Editor.Avalonia/Controls/TileCanvas.cs`
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileSourceSceneTests.cs` (new), `tests/ColorRise.Tessera.Editor.Avalonia.Tests/TileCanvasTests.cs`

**Interfaces:**
- Consumes: `ICanvasScene`, `CanvasFrame`, `CanvasSprite`, `CanvasShape`, `TileSourceScene`, `TileSlot`, `TileCanvas` and `CanvasProbe` (step 2).
- Produces:

```csharp
namespace ColorRise.Tessera.Editor.Canvas;

/// Replace: the canvas paints its background over the destination before the sprite, so a transparent pixel of the
/// sprite shows the background and not what was drawn under it.
public readonly record struct CanvasSprite(TilePicture Picture, Rectangle Source, RectangleF Destination, float Opacity = 1, bool Replace = false);

public enum CanvasMark { Outline, Fill, Hatch, Cross, Corner }      // Corner: a filled triangle in the top-left corner, its sides a third of the smaller side of the bounds

/// A short text at the top-left inside its bounds, at a fixed size on screen (11 pixels), on a dark backing.
/// Not drawn when the bounds are less than 14 pixels high on screen.
public readonly record struct CanvasLabel(string Text, RectangleF Bounds, Color Color);

public sealed class CanvasFrame { public List<CanvasLabel> Labels { get; } }     // drawn last, over the shapes; Clear() empties it too

/// A scene that time changes. The canvas that shows it gives it the time while it runs.
public interface ICanvasClock
{
    bool IsRunning { get; }
    event EventHandler? IsRunningChanged;
    /// Moves the scene's time. The scene raises Changed only when what it shows is different.
    void Advance(double seconds);
}

// TileSourceScene : ICanvasScene, ICanvasInput, ICanvasClock
/// Whether a tile has frames. The animated tiles of the layout are found again when the layout or this is set, and by Refresh().
public Func<TileId, bool>? IsAnimated { get; set; }
/// The slot of the frame an animated tile shows at a time; null shows the tile itself.
public Func<TileId, double, TileSlot?>? FrameOf { get; set; }
/// The text written on a tile; null for none.
public Func<TileId, string?>? LabelOf { get; set; }
/// What KeyPressed asks; without it a key is not used, as before.
public Func<CanvasKey, CanvasModifiers, bool>? KeyHandler { get; set; }
/// True: the animated tiles shown play. False: the source is shown as it is. False by default.
public bool Plays { get; set; }
public double Time { get; }
public void Refresh();

// TileCanvas
internal bool IsTicking { get; }                 // the timer runs: attached, and the scene is a clock that runs
internal void Tick(TimeSpan elapsed);            // what the timer calls with the real time since its last tick; gives the scene at most 0.25 s
```

The rules:

- `TileSourceScene.IsRunning` is `Plays` and at least one animated tile in the layout. An animated tile always has its `Corner` mark, playing or not.
- While it plays, each animated tile whose `FrameOf` gives a slot with a picture is one more sprite, `Replace: true`, of the slot's region into the bounds of the animated tile (a frame of another size is fitted to them). A slot without a picture shows the tile itself for its time.
- `Advance` compares, for each animated tile, the frame before and after: one `Changed` when at least one differs, none otherwise.
- The palette of step 2 sets none of this and stays still.
- `TileCanvas` has a `DispatcherTimer` of a 30th of a second. It runs while the canvas is in the visual tree and its scene is an `ICanvasClock` whose `IsRunning` is true; it follows `IsRunningChanged` and a change of `Scene`. A tick does not ask for a redraw: the scene's `Changed` does, as before.

- [x] **Step 1: Write the failing tests**

```csharp
// TileSourceSceneTests: a layout of TestTown's grid source through TilePictureService, hooks set by the test
public sealed class TileSourceSceneTests
{
    [Fact] public async Task An_animated_tile_has_a_corner_mark()                   // Collect: one Corner shape, on the bounds of that tile, playing or not
    [Fact] public async Task A_playing_scene_shows_the_frame_of_the_time_over_the_tile()
        // FrameOf gives the slot of 1:1,0 from 0.2 s on: after Advance(0.25), the frame has a sprite with Replace, the region of 1:1,0 and the bounds of 1:0,0
    [Fact] public async Task Advance_asks_a_redraw_only_when_a_frame_changed()      // Advance(0.05): no Changed; Advance(0.2): one
    [Fact] public async Task A_scene_that_does_not_play_shows_the_source_as_it_is() // Plays false: no sprite with Replace, IsRunning false
    [Fact] public async Task The_scene_runs_while_it_plays_and_has_an_animated_tile() // IsRunningChanged once for each: Plays set, IsAnimated set to none, Refresh after the hook's answer changed
    [Fact] public async Task A_frame_without_a_picture_shows_the_tile_itself()
    [Fact] public async Task A_tile_carries_the_text_of_LabelOf()                   // Labels has "2, 4" on the bounds of the tile
    [Fact] public void A_key_goes_to_the_handler_and_is_not_used_without_one()
}

// TileCanvasTests
[AvaloniaFact] public void A_replacing_sprite_hides_what_is_under_it()
    // an opaque red sprite, then over it a picture whose left half is transparent and right half blue, with Replace:
    // a pixel of the left half is the canvas's background, one of the right half is blue; without Replace the left is red
[AvaloniaFact] public void A_corner_mark_is_a_triangle_in_the_top_left()            // at zoom 8: the mark's colour one pixel in from the top-left corner, not at the centre
[AvaloniaFact] public void A_label_is_written_in_the_corner_of_its_bounds()
    // at zoom 8: the backing's colour at the top-left of the bounds, and at least one pixel of the text's colour inside the backing;
    // at zoom 0.5 (bounds 8 pixels high): neither
[AvaloniaFact] public void A_tick_gives_the_scene_the_time_and_at_most_a_quarter_of_a_second()   // Tick(0.1 s): 0.1; Tick(3 s): 0.25
[AvaloniaFact] public void The_timer_runs_only_for_a_clock_that_runs()
    // IsTicking: false for a scene that is no clock, true once a clock says it runs, false when it stops or the canvas leaves the window
[AvaloniaFact] public async Task The_canvas_ticks_by_itself()                       // CanvasProbe.WaitUntilAsync(() => scene.Advanced > 0): the one test that waits
```

- [x] **Step 2: Run them to see them fail.** Filters: `FullyQualifiedName~TileSourceSceneTests` and `FullyQualifiedName~TileCanvasTests`. Expected: FAIL to compile.
- [x] **Step 3: Write the types, the scene's hooks and the canvas's drawing and timer.**
- [x] **Step 4: Run the tests.** Expected: PASS, with step 2's `TilePaletteTests` and `TileCanvasTests` unchanged. If `The_canvas_ticks_by_itself` cannot pass under the headless platform (no timer runs there), keep `Tick` and `IsTicking`, drop that test with a line in the ledger, and see the tick by hand in Task 17. If a timer in a control proves wrong for another reason, R12's fallback: the view model runs it through the session's dispatcher.
- [x] **Step 5: Commit and push**: `feat(tessera): a canvas that plays, writes text and replaces what is under a sprite`

**Done (2026-11-25).** Red: neither test project compiled (`IsAnimated`, `CanvasLabel`, `ICanvasClock` not found). Green: the 8 scene tests and the palette's 11, and the canvas's 22 (6 new). The timer runs under the headless platform: `The_canvas_ticks_by_itself` passes, with its wait. Editor project 259, views project 43.

**Done differently.** A replacing frame of a sheet source is drawn instead of the tile's own sprite, not over it (the same picture, one sprite less). `Advance` moves the time whether the scene plays or not; only a playing scene compares its frames. The labels are drawn after the aliased shapes, with the smooth edges of text, in the font the canvas inherits.

### Task 10: Animation mode of the tile set editor (risk R11)

The view model. No control.

**Files:**
- Create: `src/ColorRise.Tessera.Editor/TileSetEditor/TileFrameItemViewModel.cs`
- Modify: `TileSetEditor/TileSetEditorMode.cs`, `TileSetEditorViewModel.cs`, `TileSetGraph.cs`, `TileDataViewModel.cs`, `src/ColorRise.Tessera.Assets/TileDefinition.cs` (the attribute of `Animation`)
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileSetEditorAnimationTests.cs` (new)

**Interfaces:**
- Consumes: Task 9; Task 2's `Tile.FrameAt(frames, own, time)`; Task 1's `Definitions`; `TileSetEditorViewModel` (`Mode`, `Scene`, `Pictures`, `SelectedSource`, `SelectedTiles`, `Follow()`, `ShowProperties()`, `WriteTileData()` and its step `Tile data`), `TileSetGraph.SetDefinition`, `TileDataViewModel` and its `ShouldConstructMember`, `TileAnimationFrame` (step 1: a struct of `Tile` and `Duration`).
- Produces:

```csharp
public enum TileSetEditorMode { Select, Solid, Animation }

/// One frame of the animated tile, for the bar.
public sealed class TileFrameItemViewModel : DispatcherViewModel
{
    public int Index { get; }                // from 0
    public string Label { get; }             // "1", "2", ...
    public TileId Tile { get; }
    public float Duration { get; }
    public TilePicture? Picture { get; }     // null when the set no longer has the tile
    public Rectangle Region { get; }
    public bool IsMissing { get; }
}

// TileSetEditorViewModel
public TileId? AnimatedTile { get; }                                            // null out of Animation mode
public IReadOnlyObservableList<TileFrameItemViewModel> Frames { get; }          // of AnimatedTile
public TileFrameItemViewModel? SelectedFrame { get; set; }
public ICommandBase RemoveFrameCommand { get; }
public ICommandBase MoveFrameLeftCommand { get; }
public ICommandBase MoveFrameRightCommand { get; }
public ICommandBase ClearFramesCommand { get; }
public ICommandBase DoneAnimationCommand { get; }                               // lets go of the animated tile
public void MoveFrame(int from, int to);

// TileSetGraph
/// Writes the frames of a tile and keeps its other data. A definition left with nothing is removed; a tile without one gets one.
public static void SetAnimation(TileSetViewModel tileSet, TileId tile, IReadOnlyList<TileAnimationFrame> frames);

// TileDataViewModel: one more parameter
internal TileDataViewModel(IViewModelServiceProvider serviceProvider, NodeContainer container, TileId tile, TileDefinition? definition, bool showAnimation = false);
```

The rules, beyond decision 8:

- The scene always plays in this editor (`Scene.Plays = true`), in the three modes. Its hooks read `Definitions`: `IsAnimated` is "has at least one frame", `FrameOf` is `Pictures.Find(Tile.FrameAt(frames, tile, time))`. `Follow()` calls `Scene.Refresh()`.
- Entering the mode with exactly one tile selected animates that tile at once. Otherwise the first click does. In the mode the scene's pick mode is `One`, and its selection is the animated tile.
- A click on a tile while a tile is animated adds a frame (`Add frame`) after `SelectedFrame`, or at the end without one, and selects the new frame. Its duration is that of the frame before it, or 0.2 s. The animated tile can be its own frame.
- `RemoveFrameCommand` (`Remove frame`) then selects the frame that took the place, or the last. `MoveFrame` and the two move commands are `Move frame`. `ClearFramesCommand` is `Clear frames`. The four are off without what they need.
- `Scene.KeyHandler`: in this mode Delete removes the selected frame and Escape lets go of the animated tile; both say they used the key. In the other modes no key is used.
- `LabelOf` gives, for the animated tile's frames, their numbers: `"1"`, `"2, 4"` for a tile that is the frames 2 and 4, and `"1, 3, 5…"` beyond three.
- The grid, in this mode, shows one `TileDataViewModel` of the animated tile with `showAnimation: true`: all its data, and `Animation` as a list whose items show `Duration` only (`TileAnimationFrame.Tile` is hidden through `ShouldConstructMember`). `TileDefinition.Animation` gets `[MemberCollection(ReadOnly = true)]` *(check: `Stride.Core.Annotations.MemberCollectionAttribute`, and that it changes nothing in the file)*, so the grid cannot add or remove a frame. A duration set there is written as step 2 writes tile data (`Tile data`). Without an animated tile the grid is empty.
- Leaving the mode lets go of the animated tile, and the grid shows the selection again. Changing `SelectedSource` keeps the animated tile.
- After every change of the asset (`Follow()`): the frames are made again; `SelectedFrame` stays at its index, or the last frame, or null; an animated tile the pictures no longer have is let go.
- `Problems` gains, for each animated tile of the set with a frame the set does not have: `The animation of the tile 1:3,0 uses the tile 1:9,9, which the tile set does not have.`

- [x] **Step 1: Try R11 first, as a test.** Before anything else of this task, with only `showAnimation` written:

```csharp
// TileSetEditorAnimationTests
[Fact] public async Task The_grid_edits_the_duration_of_a_frame()
    // a tile with two frames of 0.2 s; the TileDataViewModel of that tile with showAnimation; through its root node, the way
    // The_property_grid_edits_the_data_of_a_tile does: Animation[1].Duration set to 0.5. Then: ToDefinition() has 0.2 and 0.5,
    // the asset has them after the write, the step is "Tile data", an undo brings 0.2 back.
[Fact] public async Task The_grid_shows_the_durations_and_not_the_tiles_of_the_frames()
    // the node presenters of the provider: Animation has two items, each with Duration and no Tile, and cannot add an item
```

If the first cannot pass (a struct in a list of a copy cannot be edited through its node): stop the grid's part here. The fallback of R11: `TileFrameItemViewModel.Duration` becomes settable and writes through `SetAnimation` (step name `Frame duration`, one more fixed name), Task 11's bar gets a number box per frame, `showAnimation` goes away, and the note under this task says what was seen. The rest of the task does not change.

- [x] **Step 2: Write the other failing tests**

```csharp
[Fact] public async Task The_first_click_picks_the_tile_to_animate_and_adds_no_frame()       // AnimatedTile set, Frames empty, no step
[Fact] public async Task Entering_the_mode_with_one_tile_selected_animates_it()
[Fact] public async Task A_click_adds_a_frame_at_the_end_that_lasts_as_the_one_before()      // 0.2 for the first; after a duration of 0.5, the next is 0.5; Steps are "Add frame"
[Fact] public async Task A_click_adds_after_the_selected_frame_and_selects_the_new_one()
[Fact] public async Task A_tile_can_be_a_frame_twice_and_its_own_frame()
[Fact] public async Task A_frame_can_come_from_another_source()                              // SelectedSource changed: AnimatedTile kept, the click adds a tile of the sheet source
[Fact] public async Task Remove_move_and_clear_are_one_step_each()                           // "Remove frame", "Move frame", "Clear frames"; each undone gives the frames back
[Fact] public async Task Clear_removes_a_definition_left_with_nothing_and_keeps_a_solid_one()
[Fact] public async Task Delete_removes_the_selected_frame_and_Escape_lets_go()              // through Scene.KeyPressed
[Fact] public async Task The_frames_carry_their_numbers_and_an_animated_tile_its_mark()      // Scene.Collect: labels "1" and "2, 3"; the Corner in Select mode too
[Fact] public async Task An_animated_tile_plays_in_place()                                   // Scene.Advance(0.25): the Replace sprite of the second frame on the bounds of the animated tile
[Fact] public async Task Leaving_the_mode_lets_go_and_the_grid_shows_the_selection_again()
[Fact] public async Task SetAnimation_keeps_the_other_data_and_the_order_of_the_definitions()
// Review Focus 3
[Fact] public async Task An_undo_that_removes_the_selected_frame_moves_or_clears_the_selection()
[Fact] public async Task A_removed_source_lets_go_of_its_animated_tile()                     // RemoveSource in a transaction of the test: AnimatedTile null, Frames empty, no throw
[Fact] public async Task The_frames_follow_a_change_made_outside_the_editor()
// a frame whose tile is gone
[Fact] public async Task A_frame_whose_tile_is_gone_is_marked_and_named_and_kept()           // IsMissing, Picture null, the line of Problems, the asset still has the frame
```

- [x] **Step 3: Run them to see them fail.** Filter: `FullyQualifiedName~TileSetEditorAnimationTests`. Expected: FAIL to compile.
- [x] **Step 4: Write `SetAnimation`, the mode, the frames and the commands.**
- [x] **Step 5: Run the tests.** Expected: PASS, with `TileSetEditorTilesTests` and `TileSetEditorSourcesTests` of step 2.
- [x] **Step 6: Commit and push**: `feat(tessera): Animation mode of the tile set editor`

**Done (2026-11-25).** R11 holds: with only `showAnimation` and the attribute written, both spike tests passed at once after a red that did not compile. The grid edits `Duration` inside a frame of the copy, the listener hears it, `ToDefinition()` has it, and `[MemberCollection(ReadOnly = true)]` takes away the grid's add, remove and move of frames; the tile set golden file of step 1 is unchanged (runtime tests 395). Red: the 17 other tests did not compile (`AnimatedTile`, `TileSetEditorMode.Animation` not found). Green: 19 of the filter, editor project 278, views project 43.

**Done differently.** The spike test writes the copy as the editor writes tile data (`TileSetGraph.SetDefinition` in a step `Tile data`), since the mode did not exist yet; the editor's own write of a duration set in the grid is checked by `A_click_adds_a_frame_at_the_end_that_lasts_as_the_one_before`. `SetAnimation` writes only the frames that differ (the frames shared at the start and the end stay), so an added or removed frame is one item of the list and the selection never sees a list emptied on the way. An animated tile is also let go as soon as its source is gone from the asset, before the pictures are made again (they are made on a later turn). `Problems` has one line per animated tile and missing frame tile, and none for the tiles of a source whose image cannot be read, which has its own line. The frames are made again only when a frame or its picture changed. In Animation mode, `SelectedTiles` keeps the selection of Select mode, which the scene and the grid show again when the mode ends.

### Task 11: The view of Animation mode, and a control that shows one picture

**Files:**
- Create: `src/ColorRise.Tessera.Editor.Avalonia/Controls/PictureView.cs`
- Modify: `src/ColorRise.Tessera.Editor.Avalonia/Views/TileSetEditorView.axaml`, `TileSetEditorView.axaml.cs`, `src/ColorRise.Tessera.Editor/TesseraCommands.cs`
- Test: `tests/ColorRise.Tessera.Editor.Avalonia.Tests/PictureViewTests.cs` (new), `TileSetEditorViewTests.cs`, `tests/ColorRise.Tessera.Editor.Tests/TesseraCommandsTests.cs`

**Interfaces:**
- Consumes: Tasks 9 and 10; `PictureBitmaps`, `TileComposer.MissingColor`, the tool bar and the two mode buttons of `TileSetEditorView.axaml`, the commands `Tessera.TileSet.Mode.Select` and `Tessera.TileSet.Mode.Solid` (the models).
- Produces:

```csharp
/// Draws a part of a picture, fitted in the control with hard pixels and its proportions kept.
public sealed class PictureView : Control
{
    public static readonly StyledProperty<TilePicture?> PictureProperty;
    public static readonly StyledProperty<Rectangle> RegionProperty;       // empty: the whole picture
    public static readonly StyledProperty<bool> IsMissingProperty;         // a box of TileComposer.MissingColor with a cross, where there is no picture
}
```

The command `Tessera.TileSet.Mode.Animation`, "Animation mode", in the scope of the tile set editor, checked while the mode is on: 18 commands.

The view: a third toggle button `Animation` beside `Select` and `Solid`. Under the canvas, in this mode only, a bar: the frames in a row (a `ListBox` laid out horizontally, its selected item bound to `SelectedFrame`), each with its picture in a `PictureView` of 48 pixels, its number and its duration (`0.2 s`); then `<`, `>`, `x`, `Clear`, `Done`. With no animated tile the bar says `Click the tile to animate.`; with one and no frame, `Click the tiles of its frames, in order.` Each button gives the focus back to the canvas.

- [x] **Step 1: Write the failing tests**

```csharp
// PictureViewTests
[AvaloniaFact] public void A_region_is_drawn_with_hard_pixels_and_its_proportions()
    // a region 2 pixels wide and 3 high in a control of 60 by 60: 40 by 60 in the middle, each pixel 20 wide, the sides the window's colour
[AvaloniaFact] public void A_picture_that_changed_is_drawn_again()         // a pixel set and Touch(): the new colour
[AvaloniaFact] public void A_missing_picture_is_a_marked_box()

// TileSetEditorViewTests
[AvaloniaFact] public async Task The_animation_button_enters_the_mode_and_shows_the_bar()
[AvaloniaFact] public async Task Clicks_on_the_canvas_pick_the_tile_then_add_frames_to_the_bar()   // three clicks: the bar's list has two items
[AvaloniaFact] public async Task An_animated_tile_plays_on_the_canvas()
    // frames of two flat tiles of 0.2 s: the pixel at the middle of the animated tile is TestTown.Flat of the first frame,
    // and of the second after the canvas's Tick(0.25 s)
[AvaloniaFact] public async Task The_bar_removes_and_moves_the_selected_frame_and_the_canvas_has_the_focus_again()
[AvaloniaFact] public async Task Delete_on_the_canvas_removes_the_selected_frame()

// TesseraCommandsTests
// Every_command_has_its_id_a_scope_and_no_default_gesture: 18 commands
// The_tile_set_commands_reach_the_tile_set_editor: gains Tessera.TileSet.Mode.Animation and its checked state
```

- [x] **Step 2: Run them to see them fail.** Expected: FAIL to compile (`PictureView`), then the view tests fail on the button they do not find.
- [x] **Step 3: Write the control, the bar and the command.**
- [x] **Step 4: Run the tests of the three files.** Expected: PASS.
- [x] **Step 5: Commit and push**: `feat(tessera): the bar of frames of Animation mode`

**Done (2026-11-25).** Red: the views project did not compile (`PictureView` not found), and three command tests failed on the id they did not find. Green: the 16 tests of `PictureViewTests` and `TileSetEditorViewTests`, the command tests; views project 51, editor project 278. A render of the mode with five frames, one of them missing, is in `/mnt/project-files/tilemap/step3/headless-tileset-animation-bar.png`.

**Done differently.** `TilePicture` gains a `Touched` event raised by `Touch()`: `PictureView` follows it while it is shown, so a picture written in place is drawn again without a scene to say so. The bar's sentence is a property of the view model, `AnimationHint` (null once the animated tile has a frame), rather than two bindings on the list's count. The list of frames binds Delete to `Remove frame`, as the list of sources does, for when it has the focus. `An_animated_tile_plays_on_the_canvas` uses frames of 2 s and nine ticks of a quarter of a second: the canvas's own timer runs on real time during the test, and 0.2 s frames could change before the first read.

### Task 12: The map editor plays animated tiles (decision 2)

**Files:**
- Modify: `src/ColorRise.Tessera.Editor/Pictures/TileSetPictures.cs`, `TileMapEditor/TileMapScene.cs`, `TileMapEditor/TileMapEditorViewModel.cs`, `TesseraEditorSettings.cs`, `TesseraCommands.cs`, `src/ColorRise.Tessera.Editor.Avalonia/Views/TileMapEditorView.axaml`
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileMapSceneTests.cs`, `TileMapEditorTests.cs`, `TesseraCommandsTests.cs`, `tests/ColorRise.Tessera.Editor.Avalonia.Tests/TileMapEditorViewTests.cs`

**Interfaces:**
- Consumes: Task 9's `ICanvasClock`; Task 2's `Tile.FrameAt(frames, own, time)`; `TileMapScene`'s pictures of a chunk and a layer (its `Composed` entries, `PictureBudget`, `PictureBytes`), `TileComposer.Draw`, `TilePicture.Touch()`; the grid toggle of the map editor (`TesseraEditorSettings.ShowGrid`, `Tessera.Map.ToggleGrid` and its button), the model of a toggle with a setting.
- Produces:

```csharp
// TileSetPictures
/// The frames of an animated tile; null for a tile without frames. Follows the tile set: Changed is raised when the frames of a tile change.
public IReadOnlyList<TileAnimationFrame>? AnimationOf(TileId tile);

// TileMapScene : ICanvasScene, ICanvasClock, IDisposable
public bool PlayAnimations { get; set; }        // false: an animated tile shows its first frame
public double Time { get; }

// TileMapEditorViewModel
public bool PlayAnimations { get; set; }        // starts as the setting says, and writes it

// TesseraEditorSettings
public static SettingsKey<bool> PlayAnimations { get; }     // "Tessera/PlayAnimations", true, shown as "Tessera/Play the animated tiles of a map"
```

The command `Tessera.Map.ToggleAnimations`, "Play the animated tiles", in the scope of the map editor, checked while they play: 19 commands. A toggle button in the map editor's tool bar, beside the grid's.

The rules:

- When a picture of a chunk and a layer is composed, a cell whose tile is animated draws the tile `Tile.FrameAt(frames, tile, PlayAnimations ? Time : 0)`, with the cell's flips, and the picture remembers that it has animated cells and which frames it drew. A frame the set does not have is the marked box, for its time.
- `Advance` moves `Time`. For each picture given to the canvas by the last `Collect` that has animated cells and whose frames are no longer those it drew, the picture is composed again **into the same `TilePicture`**, then `Touch()`: no new picture, no new bitmap, `PictureBytes` unchanged. One `Changed` when at least one picture was drawn again, none otherwise.
- `Collect` does the same check for a picture that was out of view, before it gives it.
- `IsRunning` is `PlayAnimations` and at least one picture of the last `Collect` with animated cells. A map without animated tiles never ticks.
- A picture without animated cells is never touched by the time.

- [x] **Step 1: Write the failing tests.** The tile set of these tests is `TestTown.TileSet()` with the tile `1:0,0` animated: itself for 0.2 s, then `1:1,0` for 0.2 s.

```csharp
// TileMapSceneTests
[Fact] public async Task An_animated_tile_shows_its_first_frame_when_the_map_does_not_play()
    // frames 1:1,0 then 1:2,0: the cell's pixel is Flat(1, 0), not the tile's own Flat(0, 0), and Advance(1) changes nothing
[Fact] public async Task An_animated_tile_plays_in_its_chunk()                   // the cell's pixel: Flat(0, 0) at 0, Flat(1, 0) after Advance(0.3); Changed once
[Fact] public async Task A_tick_that_changes_no_frame_asks_no_redraw()           // Advance(0.05): no Changed, the picture's Version is the same
[Fact] public async Task A_chunk_that_plays_keeps_its_picture()                  // the same TilePicture, a higher Version, PictureBytes unchanged
[Fact] public async Task A_chunk_without_an_animated_tile_is_left_alone()        // its Version after Advance(1)
[Fact] public async Task The_scene_runs_only_while_a_visible_chunk_has_an_animated_tile()
    // IsRunning after a Collect of the chunk with the tile, and of a part of the map without it; IsRunningChanged each time
[Fact] public async Task A_chunk_that_was_out_of_view_shows_the_frame_of_the_time_when_it_comes_back()
[Fact] public async Task A_flipped_cell_plays_flipped()                          // the quarters tile as second frame, the cell flipped left to right
[Fact] public async Task Frames_changed_in_the_tile_set_show_in_the_map()        // TileSetGraph.SetAnimation in a transaction of the test, then an undo
[Fact] public async Task A_frame_whose_tile_is_gone_is_a_marked_box_for_its_time()

// TileMapEditorTests
[Fact] public async Task The_play_toggle_follows_its_setting_and_writes_it()

// TesseraCommandsTests
// 19 commands; A_map_command_cannot_run_without_a_map_editor gains the new id
[Fact] public async Task The_play_command_toggles_the_focused_map_editor_and_is_checked()

// TileMapEditorViewTests
[AvaloniaFact] public async Task The_map_plays_on_the_canvas_and_the_button_stops_it()
    // the pixel of the animated cell after the canvas's Tick(0.25 s) is the second frame's; the button pressed: the first frame's, and the canvas no longer ticks
```

- [x] **Step 2: Run them to see them fail.** Expected: FAIL to compile.
- [x] **Step 3: Write `AnimationOf`, the scene's clock, the setting, the toggle and the command.**
- [x] **Step 4: Run the tests of the four files and `LargeMapTests`.** Expected: PASS. `A_large_map_is_composed_once` must still pass as it is: a map without animated tiles composes nothing more.
- [x] **Step 5: Gate: the tile set and the map.** Run the whole solution. Expected: PASS, none skipped. Write the counts of the three projects in the ledger.
- [x] **Step 6: Commit and push**: `feat(tessera): the map editor plays animated tiles`

**Done** (9517bd265). As planned. The scene keeps, per picture, the frame drawn for each animated (slot, tile), and the
list of pictures with animated tiles given by the last `Collect`; `Advance` and the `PlayAnimations` setter draw those again
into themselves when a frame differs, `Collect` does it for a picture that comes back. `AnimationOf` copies the frames of
the first definition of a tile when the pictures are made. The ghost of a stamp shows the first frame. Gate: runtime 395,
editor 290, views 52, none skipped.

### Task 13: The flipbook editor: its graph, its tree, its strip and the palette

The view model without pictures of frames and without a preview (Task 14), without Sheet and Mirror (Task 15). No control.

**Files:**
- Create: `src/ColorRise.Tessera.Editor/FlipbookEditor/FlipbookGraph.cs`, `FlipbookEditorViewModel.cs`, `FlipbookAnimationItemViewModel.cs`, `FlipbookClipItemViewModel.cs`, `FlipbookFrameItemViewModel.cs`
- Modify: `src/ColorRise.Tessera/FlipbookFrame.cs` (the attribute of `Cells`), `tests/ColorRise.Tessera.Editor.Tests/TesseraTest.cs` (`EditFlipbookAsync`), `TestTown.cs` (`Flipbook`)
- Test: `tests/ColorRise.Tessera.Editor.Tests/FlipbookGraphTests.cs`, `FlipbookEditorTests.cs`

**Interfaces:**
- Consumes: Tasks 2, 4 and 8 (`FlipbookAsset`, `FlipbookAssetChecks.Of`, `FlipbookViewModel`, `TesseraSession.FlipbookAt`); `TilePaletteViewModel` (`Picked`, `PickedChanged`), `TileStamp` (its row 0 is the bottom row), `TilePictureService.Of(session).Get(...)`, `TileSetPictures`. Models in step 2: `TileMapGraph` (a nested list written through the graph), `TileMapEditorViewModel` (a tile set picked with `Dialogs.OpenAssetPickerAsync(Session, [typeof(TileSetAsset)])`, a question with `Dialogs.MessageBoxAsync`, its `Message` and `Problems`), `TileLayerItemViewModel` (an item the grid shows, renamed in a list: `IAssetPropertyProviderViewModel`, `IIsEditableViewModel`, a `Name` whose setter asks the editor).
- Produces:

```csharp
namespace ColorRise.Tessera.Editor.FlipbookEditor;

/// Every change of a flipbook asset, through its graph. The caller opens the transaction.
internal static class FlipbookGraph
{
    public static void SetTileSet(FlipbookViewModel flipbook, TileSetViewModel? tileSet);
    public static FlipbookAnimation AddAnimation(FlipbookViewModel flipbook, string name, bool withClip = true);     // the clip is None
    public static void RemoveAnimation(FlipbookViewModel flipbook, FlipbookAnimation animation);
    public static void RenameAnimation(FlipbookViewModel flipbook, FlipbookAnimation animation, string name);
    public static FlipbookClip AddClip(FlipbookViewModel flipbook, FlipbookAnimation animation, FlipbookDirection direction);
    public static void RemoveClip(FlipbookViewModel flipbook, FlipbookAnimation animation, FlipbookClip clip);
    public static void AddFrames(FlipbookViewModel flipbook, FlipbookClip clip, int index, IReadOnlyList<FlipbookFrame> frames);
    public static void RemoveFrames(FlipbookViewModel flipbook, FlipbookClip clip, IReadOnlyCollection<int> indices);
    /// Moves the frames at the indices, in their order, to before the frame that is at target now (Frames.Count: to the end).
    public static void MoveFrames(FlipbookViewModel flipbook, FlipbookClip clip, IReadOnlyCollection<int> indices, int target);
    public static void ReplaceFrames(FlipbookViewModel flipbook, FlipbookClip clip, IReadOnlyList<FlipbookFrame> frames);
}

[AssetEditorViewModel<FlipbookViewModel>]
public sealed class FlipbookEditorViewModel : AssetEditorViewModel<FlipbookViewModel>
{
    public IReadOnlyObservableList<FlipbookAnimationItemViewModel> Animations { get; }
    public object? SelectedItem { get; set; }                               // what the tree selects: an animation item or a clip item
    public FlipbookAnimationItemViewModel? SelectedAnimation { get; }       // the item, or the animation of the clip item
    public FlipbookClipItemViewModel? SelectedClip { get; }                 // the item, or the first clip of the animation item
    public IReadOnlyList<FlipbookDirection> FreeDirections { get; }         // those SelectedAnimation has no clip for
    public IReadOnlyObservableList<FlipbookFrameItemViewModel> Frames { get; }      // of SelectedClip
    public IReadOnlyList<FlipbookFrameItemViewModel> SelectedFrames { get; set; }
    public TileSetPictures? Pictures { get; }                               // null without a tile set, or when it cannot be found
    public TilePaletteViewModel? Palette { get; }                           // null with Pictures
    public IReadOnlyList<string> Problems { get; }
    public string? Message { get; }

    public ICommandBase PickTileSetCommand { get; }
    public ICommandBase AddAnimationCommand { get; }
    public ICommandBase AddClipCommand { get; }                             // its parameter is a FlipbookDirection
    public ICommandBase RemoveSelectedCommand { get; }                      // the clip item or the animation item
    public ICommandBase RemoveFramesCommand { get; }
    public ICommandBase MoveFramesLeftCommand { get; }
    public ICommandBase MoveFramesRightCommand { get; }
    public bool Rename(FlipbookAnimationItemViewModel animation, string name);
    public void AddFrames(TileStamp stamp);                                 // what a pick in the palette calls
    public void MoveSelectedFrames(int target);                             // what a drag in the strip calls
}

public sealed class FlipbookAnimationItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel, IIsEditableViewModel
{
    public FlipbookAnimation Animation { get; }
    public string Name { get; set; }                                        // the setter asks the editor's Rename; a refused name goes back
    public bool IsEditing { get; set; }
    public IReadOnlyObservableList<FlipbookClipItemViewModel> Clips { get; }
}

public sealed class FlipbookClipItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel
{
    public FlipbookAnimationItemViewModel Animation { get; }
    public FlipbookClip Clip { get; }
    public FlipbookDirection Direction { get; }
    public string Label { get; }                                            // "Any direction" for None, else "Down", "Up left", ...
    public int FrameCount { get; }
}

public sealed class FlipbookFrameItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel
{
    public FlipbookFrame Frame { get; }
    public int Index { get; }
    public string Label { get; }                                            // "1", "2", ...
    public float Duration { get; }
    public TileId? Tile { get; }                                            // of the first cell
    public bool HasMissing { get; }                                         // a cell names a tile the set does not have
}
```

The rules, beyond decision 9:

- **The tile set.** `PickTileSetCommand` asks for a tile set asset and writes it (`Set tile set`). It is on with or without one already set: frames keep their tiles when the set changes, and those the new set lacks are marked. `Pictures` and `Palette` (slot 1) are made again when the reference changes.
- **Animations and clips.** `AddAnimationCommand` (`Add animation`) names the new one `animation`, then `animation 2`, `animation 3`, the first name no animation has, and selects it. `AddClipCommand` (`Add clip`) is on for a direction of `FreeDirections`, and selects the new clip. `RemoveSelectedCommand` removes the clip item (`Remove clip`) or the animation item with its clips (`Remove animation`); it asks first (`Remove the clip Left of 'walk' and its 4 frames?`, `Remove the animation 'walk' and its 12 frames?`) when there is at least one frame, and then selects the neighbour that is left.
- **A name.** `Rename` trims the name. Empty: `Message` is `An animation needs a name.` and it is false. The name of another animation, compared as written: `Another animation is named 'walk'.` and false. Its own name: true, no step. Else one step, `Rename animation`. Scenes and scripts name an animation by its text: a rename does not follow into them (README, Task 18).
- **Frames from the palette.** A pick of the palette calls `AddFrames`: one frame for each cell of the stamp that has a tile, the stamp's top row first, each row left to right; each frame has one cell with that tile, no offset and the cell's flip. They go after the last selected frame, or at the end, in one step (`Add frames`), and become the selection. Each lasts as the frame before the place they go to, or 0.1 s. Without a selected clip: `Message` is `Select or add an animation first.` and nothing is added.
- **The strip.** `RemoveFramesCommand` (`Remove frames`) removes the selected frames and selects nothing. The two move commands and `MoveSelectedFrames` (`Move frames`) move the selected frames as one block that keeps its order; left and right move it by one place and are off at the ends. The moved frames stay selected.
- **The property grid** shows the most precise thing selected: the selected frames; without them the item the tree selects. A frame shows `Duration` and its `Cells`, each with `Offset` and `Flip` (`FlipbookCell.Tile` is hidden; `FlipbookFrame.Cells` gets `[MemberCollection(ReadOnly = true)]`, so the grid adds and removes no cell). A clip shows `Direction`. An animation shows `LoopMode`: `Name`, `Clips` and `Frames` are hidden, the tree and the strip own them. Nothing selected shows nothing: the flipbook's own members (`Tile set`, `Pivot`) are in the grid of the asset view, as for every asset. Setting `SelectedItem` empties `SelectedFrames`.
- **Problems**: the lines of `FlipbookAssetChecks.Of`; `The tile set of the flipbook cannot be found.` for a reference to an asset the session does not have; and for each cell whose tile the set lacks, `The frame 2 of the clip Left of 'walk' uses the tile 1:9,9, which the tile set does not have.`
- **Following the asset.** After every change of the asset the items are made again. A selected item whose animation, clip or frame is still in the asset stays selected; frames that are gone leave the selection; a clip or an animation that is gone gives the selection to the first clip of the first animation, or to nothing.

- [x] **Step 1: Write the failing tests.** `TestTown.Flipbook(tileSetLocation)` is a flipbook asset on `TestTown.TileSet()` with `walk` (Loop; the clips Down of three frames `1:0,0`, `1:1,0`, `1:2,0` and Left of two) and `idle` (one clip None of one frame). `TesseraSession.EditFlipbookAsync(location)` opens its editor as `EditMapAsync` does.

```csharp
public sealed class FlipbookGraphTests
{
    [Fact] public async Task Each_change_is_undone_and_redone()
        // in turn, each in a transaction: AddAnimation, AddClip, AddFrames, MoveFrames, RemoveFrames, ReplaceFrames, RenameAnimation,
        // RemoveClip, RemoveAnimation, SetTileSet: the asset after an undo equals the asset before (its yaml), and after a redo the asset after
    [Fact] public async Task Moved_frames_keep_their_order_and_land_before_the_target()   // five frames a b c d e: [1, 3] to 0 gives b d a c e; [0, 1] to 5 gives c d e a b
    [Fact] public async Task Removed_frames_are_named_by_their_indices_in_any_order()     // [3, 0] of a b c d gives b c
    [Fact] public async Task An_animation_can_be_added_without_a_clip()
}

public sealed class FlipbookEditorTests
{
    [Fact] public async Task The_editor_opens_with_the_tree_of_the_asset_and_the_first_clip_shown()
        // Animations: walk (Down, Left) and idle (Any direction); SelectedClip is walk's Down; Frames has three items labelled 1, 2, 3
    [Fact] public async Task A_flipbook_without_a_tile_set_has_no_palette_until_one_is_picked()
        // Palette null; the dialog service gives the tile set: Palette is set, the step is "Set tile set"; an undo: Palette null again
    [Fact] public async Task Add_animation_gives_a_free_name_and_one_clip_for_any_direction()   // "animation", then "animation 2"
    [Fact] public async Task Add_direction_offers_only_the_directions_the_animation_lacks()     // walk: seven, without Down and Left
    [Fact] public async Task Selecting_an_animation_shows_its_first_clip()
    [Fact] public async Task Removing_a_clip_or_an_animation_with_frames_asks_first()           // answered No: nothing changes; Yes: one step, the neighbour is selected
    [Fact] public async Task An_empty_clip_is_removed_without_a_question()
    // Review Focus 5
    [Fact] public async Task A_rename_is_one_step_and_an_empty_or_taken_name_is_refused_with_a_message()
        // "run": step "Rename animation"; "  ": the first message, the name stays; "idle": the second; "walk" on walk: no step
    [Fact] public async Task Two_clips_of_one_direction_made_in_the_grid_are_named_in_the_problems()
        // the Direction of Left's node set to Down: "The animation 'walk' has 2 clips for Down: the first is played."
    [Fact] public async Task A_click_in_the_palette_adds_a_frame_after_the_selected_frames()    // frame 1 selected, a click on 1:3,0: frames are 0 3 1 2 by their tiles' columns
    [Fact] public async Task A_drag_in_the_palette_adds_its_tiles_row_by_row_from_the_top()     // a block of three by two: the top row's three, then the bottom row's
    [Fact] public async Task A_new_frame_lasts_as_the_frame_before_it()                         // 0.1 s in an empty clip; 0.3 after a frame of 0.3
    [Fact] public async Task A_pick_without_a_clip_adds_nothing_and_says_why()
    [Fact] public async Task Frames_are_removed_and_moved_as_a_block_in_one_step()              // "Remove frames", "Move frames"; left is off for a block that starts at 0
    [Fact] public async Task The_grid_shows_the_frames_then_the_clip_or_the_animation()
        // the providers and their members: frames show Duration and Cells without Tile; the clip Direction; the animation LoopMode only
    [Fact] public async Task A_duration_set_in_the_grid_shows_in_the_strip()
    // Review Focus 1
    [Fact] public async Task A_frame_whose_tile_is_gone_is_marked_named_and_kept()              // HasMissing, the line of Problems, the file after a save still has the cell
    [Fact] public async Task A_tile_set_that_cannot_be_found_is_named_and_the_editor_opens()
    // Review Focus 3
    [Fact] public async Task An_undo_that_removes_the_selected_frames_empties_the_selection()
    [Fact] public async Task An_undo_that_removes_the_selected_animation_selects_the_first_clip_that_is_left()
    [Fact] public async Task The_tree_follows_a_change_made_outside_the_editor()
}
```

- [x] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~FlipbookGraphTests|FullyQualifiedName~FlipbookEditorTests`. Expected: FAIL to compile.
- [x] **Step 3: Write the graph.** Run `FlipbookGraphTests`. Expected: PASS.
- [x] **Step 4: Write the editor and its items.** Run `FlipbookEditorTests`. Expected: PASS.
- [x] **Step 5: Commit and push**: `feat(tessera): the flipbook editor's tree, strip and palette`

**Done differently** (c8d3174b7). `TestTown.Flipbook(tileSet, tileSetLocation)` takes the tile set, as `Map` does, to name
it by id (null: no tile set). The editor follows the asset once a transaction, an undo or a redo is over (`Done`, `Undone`,
`Redone`), not at each change of a node: a move takes its frames out before it puts them back, and they stay selected.
Items are kept while their animation, clip or frame is in the asset, so the tree keeps its selection and the grid is not
made again for a change of a duration. `SelectedFrames` keeps the order of the strip. Removing selects the clip after the
one removed, else the one before, else its animation; the animation after, else the one before. When the first animation
has no clip, the selection that follows a removed one is that animation. `HasMissing` is false without pictures (the
problems name the tile set). Tests: graph 4, editor 21; solution runtime 395, editor 315, views 52.

### Task 14: The pictures of the frames, and the preview

**Files:**
- Create: `src/ColorRise.Tessera.Editor/FlipbookEditor/FlipbookFrameComposer.cs` (with `FlipbookFramePicture`), `FlipbookPreviewScene.cs`
- Modify: `FlipbookEditor/FlipbookEditorViewModel.cs`, `FlipbookFrameItemViewModel.cs`
- Test: `tests/ColorRise.Tessera.Editor.Tests/FlipbookFrameComposerTests.cs`, `FlipbookPreviewTests.cs`

**Interfaces:**
- Consumes: Task 13; Task 9's `ICanvasClock`; Task 2's `FlipbookClip.FrameAt(time, mode)` and `GetPassLength(mode)`; `TileComposer.Draw` and `DrawMissing`, `TileSetPictures.Find`, `CanvasView`; `TileComposerTests`, the model of a test on pictures made by hand.
- Produces:

```csharp
/// A frame as the game draws it, in pixels. Bounds says where the picture is around the pivot, x right and y up:
/// its X and Y are the left and the bottom.
public sealed record FlipbookFramePicture(TilePicture Picture, Rectangle Bounds, bool HasMissing);

internal static class FlipbookFrameComposer
{
    /// Null for a frame without a cell. A cell whose tile the set lacks is a marked box of the set's tile size.
    /// find gives the slot of a tile, or null: the editor passes its TileSetPictures.Find, a test a function over slots it made.
    public static FlipbookFramePicture? Compose(FlipbookFrame frame, Vector2 pivot, Func<TileId, TileSlot?> find, Int2 tileSize);
}

public sealed class FlipbookPreviewScene : ICanvasScene, ICanvasClock
{
    public FlipbookClip? Clip { get; set; }                                 // another clip starts at the time 0
    public FlipbookLoopMode LoopMode { get; set; }
    public Func<FlipbookFrame, FlipbookFramePicture?>? PictureOf { get; set; }
    public bool IsPlaying { get; set; }
    public float Speed { get; set; } = 1;
    public bool Loop { get; set; }                                          // start again at the end, whatever LoopMode
    public double Time { get; set; }
    public int FrameIndex { get; }                                          // in Clip.Frames; -1 when nothing is shown
    public event EventHandler? FrameIndexChanged;
    public void Refresh();                                                  // the clip's frames or their pictures changed
}

// FlipbookEditorViewModel
public FlipbookPreviewScene Preview { get; }
public CanvasView PreviewView { get; }
public bool IsPlaying { get; set; }                                         // true when the editor opens
public IReadOnlyList<float> PreviewSpeeds { get; }                          // 0.25, 0.5, 1, 2
public float PreviewSpeed { get; set; }                                     // 1
public bool LoopPreview { get; set; }
public ICommandBase PlayPauseCommand { get; }

// FlipbookFrameItemViewModel
public TilePicture? Picture { get; }                                        // the frame's composed picture
public bool IsCurrent { get; }                                              // the preview shows this frame
```

Where a frame is, in pixels, as Task 5 places it in the world: with `(w0, h0)` the size of the first cell's tile (the set's tile size when the set lacks it), the first cell's bottom-left corner is at `-(Pivot.X * w0, Pivot.Y * h0)`, rounded to the nearest pixel; a cell is at its offset from there, and as large as its tile; each cell is drawn with its flips. The picture covers all the cells. The canvas has y down: the scene puts a picture whose bounds are `(x, y, w, h)` at `(x, -(y + h), w, h)`.

The rules:

- The scene shows one sprite, the picture of the frame at `Time`, and a `Cross` mark of 3 pixels around the origin, where the pivot is. Its `Bounds` are those of all the frames of the clip and the origin together, so the view does not jump from frame to frame; its `GridSize` is zero.
- `Advance` moves the time by the seconds times `Speed`. With `Loop`, a `Once` animation is shown as a `Loop`. Without it, a `Once` stops on its last frame and `IsPlaying` becomes false; play again starts at 0. `Changed` and `FrameIndexChanged` are raised only when the frame is another one. `IsRunning` is `IsPlaying` and a clip with a frame.
- The editor composes the picture of a frame once and keeps it until the flipbook's graph or the tile set's pictures change. `Preview.Clip` follows `SelectedClip`, and `LoopMode` the animation's.
- Exactly one frame selected in the strip pauses the preview on that frame. `IsCurrent` follows the preview.

- [x] **Step 1: Write the failing tests**

```csharp
// FlipbookFrameComposerTests: no session; tiles of 16 by 16 and of 16 by 24 as TileSlots over pictures made by the test
[Fact] public void The_pivot_is_the_origin_of_the_bounds()                    // pivot (0.5, 0), a tile of 16 by 16: Bounds (-8, 0, 16, 16)
[Fact] public void A_tall_frame_stands_on_the_pivot()                         // Review Focus 4: 16 by 24: Bounds (-8, 0, 16, 24), its top row of pixels at the top
[Fact] public void A_pivot_at_the_corner_puts_the_frame_beside_it()           // pivot (0, 0): Bounds (0, 0, 16, 16); pivot (1, 1): (-16, -16, 16, 16)
[Fact] public void A_flipped_cell_is_drawn_flipped()                          // Horizontal: the left and the right pixels swap
[Fact] public void A_second_cell_is_placed_by_its_offset()                    // offset (0, 16): Bounds 32 high, the second cell's pixels above the first's
[Fact] public void A_missing_tile_is_a_marked_box_and_the_picture_says_so()   // Review Focus 1
[Fact] public void A_frame_without_a_cell_has_no_picture()

// FlipbookPreviewTests: the editor of TestTown.Flipbook
[Fact] public async Task The_preview_shows_the_frame_of_its_time_around_the_pivot()
    // Collect: one sprite at (-8, -16, 16, 16) in canvas units, with the pixels of 1:0,0, and a Cross around the origin
[Fact] public async Task The_bounds_cover_every_frame_of_the_clip()
[Fact] public async Task Advance_moves_the_frame_at_the_speed_and_asks_a_redraw_only_then()
    // frames of 0.1 s, speed 2: Advance(0.03) changes nothing, a second Advance(0.03) shows the frame 2, with one Changed
[Fact] public async Task A_once_animation_stops_on_its_last_frame_and_loop_starts_it_again()
[Fact] public async Task A_ping_pong_plays_there_and_back()                   // 1 2 3 2 1 over Advance steps of 0.1
[Fact] public async Task One_selected_frame_pauses_the_preview_on_it()
[Fact] public async Task The_strip_marks_the_frame_the_preview_shows()        // IsCurrent
[Fact] public async Task The_frames_have_their_pictures()                     // each item's Picture has the pixels of its tile
[Fact] public async Task A_new_image_of_the_tile_set_makes_the_pictures_again()
// Review Focus 3
[Fact] public async Task An_undo_that_removes_the_frame_shown_shows_the_first_that_is_left()   // while playing, at the time of the last frame: FrameIndex 0 or the last index left, no throw
[Fact] public async Task A_removed_animation_under_the_preview_shows_the_next_clip_or_nothing()
// Review Focus 4
[Fact] public async Task The_preview_shows_a_flipped_tall_frame_as_the_game_draws_it()
    // a 16 by 24 tile whose bottom-right pixel is red, the cell flipped left to right, pivot (0.5, 0):
    // the sprite is at (-8, -24, 16, 24), and the picture's bottom-left pixel is red
```

- [x] **Step 2: Run them to see them fail.** Expected: FAIL to compile.
- [x] **Step 3: Write the composer.** Run `FlipbookFrameComposerTests`. Expected: PASS.
- [x] **Step 4: Write the scene, and the editor's part.** Run `FlipbookPreviewTests`. Expected: PASS, with `FlipbookEditorTests`.
- [x] **Step 5: Commit and push**: `feat(tessera): the pictures of a flipbook's frames and its preview`

**Done differently** (856eff470). The composer and the scene were written before the first green run, as the test project
builds them together. `Bounds` of the preview take in the cross of the pivot (3 pixels around the origin), not only the
origin, so a clip without frames still fits. The preview moves its time only while it plays. `HasMissing` of a frame item
now comes from its composed picture. One frame selected sets the preview's time to the start of that frame (the
durations before it). Tests: composer 7, preview 12; solution runtime 395, editor 334, views 52.

### Task 15: Sheet and Mirror

**Files:**
- Create: `src/ColorRise.Tessera.Editor/FlipbookEditor/PendingSheetViewModel.cs` (with `SheetRowViewModel` and `SheetFrameOrder`)
- Modify: `FlipbookEditor/FlipbookEditorViewModel.cs`
- Test: `tests/ColorRise.Tessera.Editor.Tests/FlipbookSheetTests.cs`, `FlipbookMirrorTests.cs`

**Interfaces:**
- Consumes: Task 13 (`FlipbookGraph`, the palette's picks); Task 2's `FlipbookDirections.Mirrored`; `PendingImageSourceViewModel`, the model of a bar that proposes, says what is wrong and is confirmed.
- Produces:

```csharp
public enum SheetFrameOrder { InOrder, ThereAndBack }

public sealed class SheetRowViewModel : DispatcherViewModel
{
    public int Row { get; }                                 // from 1, from the top of the block
    public FlipbookDirection? Direction { get; set; }       // null: the row is skipped
}

/// What Sheet proposes: a block of the palette cut into one clip a row.
public sealed class PendingSheetViewModel : DispatcherViewModel
{
    public TileStamp? Block { get; }
    public int Columns { get; }
    public int Rows { get; }
    public IReadOnlyList<SheetRowViewModel> RowItems { get; }
    public SheetFrameOrder Order { get; set; }              // InOrder
    public float Duration { get; set; } = 0.15f;
    public string AnimationName { get; set; }
    public string? Problem { get; }                         // why Make is off
    public string? Note { get; }                            // what Make replaces
    public ICommandBase ConfirmCommand { get; }             // "Make"
    public ICommandBase CancelCommand { get; }
}

// FlipbookEditorViewModel
public PendingSheetViewModel? PendingSheet { get; }
public ICommandBase SheetCommand { get; }                   // on with a palette; opens the bar
public ICommandBase MirrorCommand { get; }
```

The rules of Sheet:

- The bar opens with the name of the selected animation, or `walk` without one, and with the palette's pick as its block when that pick has more than one cell. While it is open, a pick in the palette sets the block and adds no frame.
- The rows' directions when a block is set: one row is `None`; else the rows are Down, Left, Right, Up from the top, and the rows after the fourth are skipped.
- `Problem`, in this order: `Drag over the frames in the palette.` (no block), `The animation needs a name.`, `The duration must be above 0.`, `Every row is skipped.`, `Two rows have the direction Left.`
- `Note`: `Replaces the clips Down, Left of 'walk'.` when the animation of that name has frames in clips of the rows' directions.
- `Make` is one step, `Sheet`. The animation of that name is used, or a new one is added without a clip. For each row with a direction, the clip of that direction is made if the animation lacks it, and its frames become the row's: one frame for each cell of the row that has a tile, left to right, each with one cell and the bar's duration. With `ThereAndBack`, a row of more than two frames `1 … n` is followed by `n - 1 … 2`, so three columns play 1, 2, 3, 2. A row without a tile makes no clip. The other clips of the animation stay. Then the bar closes and the animation is selected.
- `Cancel` closes the bar and changes nothing.

The rules of Mirror:

- On when `SelectedClip` has a direction that `Mirrored()` changes. The target is the clip of the mirrored direction in the same animation, made if it lacks it. When the target has frames it asks first: `Replace the clip Left of 'walk'?`
- One step, `Mirror clip`. The target's frames are copies of the selected clip's: the same durations, and each cell with the same tile, its `Flip` with `Horizontal` toggled, and its offset mirrored around the middle of the first cell: for a cell `w` pixels wide at the offset `x`, with the first cell `w0` wide, `x' = w0 - (x + w)`; `y` is kept. The widths are those of the tiles in the set's pictures, or the set's tile size for a tile it lacks.
- With the pivot's X at 0.5 the mirrored clip is what `FlipX` shows. Then the target clip is selected.

- [x] **Step 1: Write the failing tests**

```csharp
// FlipbookSheetTests: TestTown's grid is four tiles wide; the block of these tests is its three first columns over its two rows,
// or a taller image written by the test for the four rows
[Fact] public async Task The_bar_opens_with_the_block_picked_and_the_name_of_the_selected_animation()
[Fact] public async Task A_pick_while_the_bar_is_open_sets_the_block_and_adds_no_frame()
[Fact] public async Task Four_rows_are_down_left_right_up_and_the_rows_after_are_skipped()      // a block of 3 by 5
[Fact] public async Task One_row_is_a_clip_for_any_direction()
[Fact] public async Task Make_writes_the_clips_in_one_step()
    // Steps is ["Sheet"]; 'walk' has the four clips; Down's frames are the top row's tiles left to right, each 0.15 s; the bar is closed
[Fact] public async Task There_and_back_plays_1_2_3_2()                                        // and a row of two frames stays 1, 2
[Fact] public async Task Make_replaces_the_clips_of_its_directions_and_keeps_the_others()      // walk's Down replaced, an UpLeft clip kept; Note said so before
[Fact] public async Task A_new_name_makes_a_new_animation_with_only_the_rows_clips()
[Fact] public async Task A_hole_in_a_row_is_skipped_and_a_row_of_holes_makes_no_clip()
[Theory] [InlineData("no block")] [InlineData("no name")] [InlineData("duration 0")] [InlineData("all skipped")] [InlineData("two rows of one direction")]
public async Task What_cannot_be_made_is_said_and_Make_is_off(string what)                    // the exact line of Problem
[Fact] public async Task Cancel_changes_nothing_and_the_palette_adds_frames_again()
[Fact] public async Task An_undo_takes_the_whole_sheet_back()

// FlipbookMirrorTests
[Theory] [InlineData(FlipbookDirection.None)] [InlineData(FlipbookDirection.Up)] [InlineData(FlipbookDirection.Down)]
public async Task Mirror_is_off_for_a_direction_that_is_its_own_mirror(FlipbookDirection direction)
[Fact] public async Task Mirror_makes_the_other_side_with_each_cell_flipped()
    // Right to Left: the same tiles and durations; a cell None becomes Horizontal, a cell Horizontal becomes None; step "Mirror clip"; Left is selected
[Fact] public async Task Mirror_moves_the_offsets_of_the_other_cells()                         // first cell 16 wide, a second cell 8 wide at (2, 16): its offset becomes (6, 16)
[Fact] public async Task Mirror_asks_before_it_replaces_a_clip_with_frames()                   // No: unchanged, no step; Yes: replaced
[Fact] public async Task The_mirrored_frames_are_copies()                                      // a duration changed on one side leaves the other
[Fact] public async Task A_diagonal_mirrors_to_the_other_diagonal()                            // UpRight makes UpLeft
```

- [x] **Step 2: Run them to see them fail.** Expected: FAIL to compile.
- [x] **Step 3: Write the bar's view model, and the two commands.**
- [x] **Step 4: Run the tests.** Expected: PASS, with `FlipbookEditorTests`.
- [x] **Step 5: Commit and push**: `feat(tessera): Sheet and Mirror in the flipbook editor`

**Done differently** (cca87cb67). "Every row is skipped." is also the problem when no row with a direction has a tile, as a
row of holes makes no clip. `Note` speaks of rows that write a clip, says "the clip Down" for one clip, and is null while
there is a problem. The open bar closes when the palette is made again for another tile set, as its block names tiles of
the old one; Sheet does nothing while the bar is open. Mirror takes 16 pixels as the width of a tile when the flipbook has
no tile set, and skips empty frames and cells. One more test, `Sheet_is_off_without_a_palette`. Tests: sheet 17, mirror 8,
editor 21; solution runtime 395, editor 359, views 52.

### Task 16: The view of the flipbook editor, and its commands

**Files:**
- Create: `src/ColorRise.Tessera.Editor.Avalonia/Views/FlipbookEditorView.axaml`, `FlipbookEditorView.axaml.cs`
- Modify: `src/ColorRise.Tessera.Editor/TesseraCommands.cs`
- Test: `tests/ColorRise.Tessera.Editor.Avalonia.Tests/FlipbookEditorViewTests.cs`, `tests/ColorRise.Tessera.Editor.Tests/TesseraCommandsTests.cs`

**Interfaces:**
- Consumes: Tasks 13, 14 and 15; `TileCanvas`, `TilePalette`, `PictureView`; `TileMapEditorView` (the model: `[AssetEditorView<...>]`, a canvas, a palette, a list whose names are typed in place, the problems at the bottom) and the bar of a pending thing in `TileSetEditorView`.
- Produces: `[AssetEditorView<FlipbookEditorViewModel>] public sealed partial class FlipbookEditorView : UserControl, IAssetEditorView`, and four commands in the scope of the flipbook editor: `Tessera.Flipbook.PlayPause` ("Play or pause the preview"), `Tessera.Flipbook.AddAnimation` ("Add an animation"), `Tessera.Flipbook.Sheet` ("Make clips from a sheet"), `Tessera.Flipbook.Mirror` ("Mirror the clip"): 23 commands.

The view, as section 9 draws it:

- **Left**: a tree of the animations and their clips, its selection bound to `SelectedItem`. Over it `+` (add an animation), `+ Direction` (a menu of `FreeDirections`), `-` (remove), `Sheet`, `Mirror`. A double click or F2 on an animation edits its name in place: Enter gives it to `Rename`, Escape leaves it.
- **Centre, top**: a `TileCanvas` on `Preview` and `PreviewView`, fitted when the clip changes. Under it: play or pause, the speed (25%, 50%, 100%, 200%), `Loop`.
- **Centre, bottom**: the Sheet bar when `PendingSheet` is set (the block's size, a direction or `Skip` per row, the order, the duration, the name, `Make`, `Cancel`, the problem or the note), then the strip: a `ListBox` laid out horizontally that selects several, each frame with its picture in a `PictureView` of 64 pixels, its number and its duration, the current frame outlined; then `<`, `>`, `x`. Delete in the strip removes the selected frames. A frame dragged with the pointer along the strip moves the selected frames to where it is let go (`MoveSelectedFrames`): the pointer's own events, no drag and drop of the platform.
- **Right**: the `TilePalette` on `Palette`, under a line with the tile set's name and `Change`; without a tile set, a button `Pick a tile set`.
- **Bottom**: `Message`, and the problems, as the two other editors show them.
- Each button gives the focus back to the preview's canvas.

- [x] **Step 1: Write the failing tests**

```csharp
// FlipbookEditorViewTests: the editor of TestTown.Flipbook, shown in a window of 1000 by 700
[AvaloniaFact] public async Task The_editor_opens_from_the_session_with_its_view()
[AvaloniaFact] public async Task The_tree_names_the_animations_and_their_clips()              // "walk", "Down", "Left", "idle", "Any direction"
[AvaloniaFact] public async Task The_preview_draws_the_frame_and_plays_when_the_canvas_ticks()
    // the pixel at the middle of the frame in the preview canvas is TestTown.Flat(0, 0), and Flat(1, 0) after its Tick(0.1 s)
[AvaloniaFact] public async Task The_strip_has_a_picture_for_each_frame_and_a_click_selects_one()   // three items; the first item's middle pixel is Flat(0, 0)
[AvaloniaFact] public async Task A_click_in_the_palette_adds_a_frame_to_the_strip()
[AvaloniaFact] public async Task The_buttons_of_the_strip_move_and_remove_the_selected_frames()
[AvaloniaFact] public async Task A_drag_along_the_strip_moves_a_frame()                       // press on the item 1, move over the item 3, release: the tiles' order
[AvaloniaFact] public async Task A_name_typed_in_the_tree_renames_and_a_taken_name_shows_the_message()
[AvaloniaFact] public async Task The_sheet_button_opens_its_bar_and_Make_fills_the_tree()
[AvaloniaFact] public async Task A_flipbook_without_a_tile_set_shows_the_pick_button()
[AvaloniaFact] public async Task A_problem_shows_at_the_bottom()

// TesseraCommandsTests
// Every_command_has_its_id_a_scope_and_no_default_gesture: 23 commands
[Fact] public async Task The_flipbook_commands_reach_the_focused_flipbook_editor()            // PlayPause toggles IsPlaying; AddAnimation adds one; Sheet opens the bar; Mirror is off for a clip Down
[Fact] public async Task A_flipbook_command_cannot_run_without_a_flipbook_editor()
```

- [x] **Step 2: Run them to see them fail.** Expected: the view tests fail on the view the session does not find; the command tests on the ids.
- [x] **Step 3: Write the view and the commands.**
- [x] **Step 4: Run the tests of the two files.** Expected: PASS. If the headless pointer cannot drive `A_drag_along_the_strip_moves_a_frame`, test the view's method the pointer calls instead, and check the drag by hand in Task 17.
- [x] **Step 5: Gate: the editors.** Run the whole solution. Expected: PASS, none skipped. Write the counts in the ledger.
- [x] **Step 6: Commit and push**: `feat(tessera): the view of the flipbook editor`

**Done differently** (fef6ff4bb). TestTown's tile 1:0,0 has four quarters, so the preview and strip tests read the red
quarter, not `Flat(0, 0)`; the preview test uses frames of 2 s and nine ticks of a quarter of a second, as Task 12's did,
since the canvas's own timer runs on real time. The test window routes Delete and F2 through an `EditCommandRouter`, as
Game Studio's window does. A row of the Sheet bar picks from `SheetRowViewModel.Choices` (Skip, then the directions as the
tree names them) through `Choice`, a small addition to the view model with its own test. The menu of `+ Direction` is made
when the button is clicked, and that button keeps the focus while it is open. A press on a selected frame among several
keeps them selected, so that they drag together; a click without a drag then selects it alone. The commands Sheet and
Mirror can run only when the editor's own command is on. The drag along the strip is driven by the headless pointer. A
headless picture of the view is `step3/headless-flipbook-editor.png`. Tests: view 11, commands 9; solution runtime 395,
editor 362, views 63.

### Task 17: Game Studio by hand (risks R7 and R13)

Nothing before this task opens Game Studio. The check game of step 2 gains what this step needs, and the list is gone through under Xvfb as [studio-check.md](studio-check.md) says.

**Files:**
- Create: `tests/StudioCheck/draw-hero.sh`, `StudioCheck.Game/Resources/hero.png`, `StudioCheck.Game/Walker.cs`, and the assets made in Game Studio: a tile set `Hero`, a flipbook `Hero`
- Modify: `tests/StudioCheck/draw-town.sh` and `town.png` (a third row), `README.md` of the check game, its scene and its compositor (saved by Game Studio), `docs/studio-check.md` (a second part, "Step 3", with what was seen), `tests/PackagesCheck/` (a flipbook)

**Interfaces:**
- Consumes: everything before; `tests/StudioCheck/README.md` (how to open it: Xvfb at 1600 by 1000, Lavapipe, xdotool), `docs/studio-check.md` (the shape of the list).
- Produces: the pictures `/mnt/project-files/tilemap/step3/studio-NN-*.png`, the list with what was seen, and the fixes.

What the check game gains:

- `hero.png`, 48 by 96 pixels, drawn by `draw-hero.sh` with ImageMagick: three columns by four rows of figures 16 pixels wide and 24 high. The rows, from the top, face down, left, right and up, each with a body of its own colour and a face on the side it looks at; the columns are the left foot forward, both feet, the right foot forward. A picture of the game then tells the direction and the frame.
- `town.png` gains a third row: two more waters, with their waves moved, and two spare tiles. The ids of the tiles already there do not change.
- `Walker`, a `SyncScript` on the entity of the flipbook: it walks around a square of 2 units a side, 2 seconds a side (down, right, up, left), sets `Direction` with `FlipbookDirections.FromVector` and plays `walk`. No key is read.

- [ ] **Step 1: Draw the two images, write the script,** and build the check game from the command line. Expected: the build succeeds.
- [ ] **Step 2: Open it in Game Studio and go through the list.** Each line gets "seen", "not as planned: ..." or "could not be checked: ..." in `docs/studio-check.md`.
  1. The plugin loads with no error about Tessera in the log. *Add asset* has `Flipbook` in the group `Sprite`, with its icon, and it makes a flipbook with the animation `idle`.
  2. **Animation mode.** In the tile set `TileSet`: the button; a click on the water, then on the three waters: the bar has three frames with their pictures, and the water plays in place on the canvas (two pictures a moment apart). A duration changed in the property grid changes the speed (R11 by hand). `<`, `>`, `x`, Delete and `Clear` do what they say, undo and redo work from the Edit menu, and the corner mark stays in Select mode.
  3. **The map editor plays.** The water of the map plays (two pictures); the button of the tool bar stops it on its first frame; the setting is in Game Studio's settings under Tessera; a stamp painted while it plays lands where it should. With a map without animated tiles in view, Game Studio's CPU is what it was (`top`, before and after): write both numbers.
  4. **A second tile size.** A tile set `Hero` made with *Add asset*, `+ Image` with `hero.png` and the grid bar set to 16 by 24: 12 tiles, each a whole figure.
  5. **The flipbook editor opens** on a new flipbook: `Pick a tile set` takes `Hero`, and the palette shows the sheet.
  6. **Sheet.** A drag over the whole sheet in the palette: the bar says 3 by 4 and Down, Left, Right, Up. `There and back`, the name `walk`, `Make`: the tree has `walk` with four clips of four frames, and the preview plays each as it is selected. One undo takes it all back, and a redo gives it again.
  7. **The strip.** A click in the palette adds a frame after the selected one. `<`, `>`, `x` and Delete work, with several frames selected too. A frame dragged along the strip lands where it is let go. A duration, an offset and a flip set in the property grid show in the strip and the preview.
  8. **The preview.** Play and pause, the four speeds, `Loop` on an animation set to `Once`. The cross is at the figure's feet, and a `Pivot` changed in the asset's properties moves the figure around the cross.
  9. **Mirror.** On the clip Right it asks to replace Left, and the preview then shows the right figure turned to the left. Undo brings the sheet's own Left back.
  10. **Names.** F2 renames an animation; the name of another shows the message and is refused; `+`, `+ Direction` and `-` with its question work.
  11. **Commands.** The command palette lists the six commands of this step, and a shortcut given to `Play or pause the preview` in the Keyboard shortcuts page works in the flipbook editor.
  12. **The file.** Save. The `.sdflipbook` reads as section 7 writes it; copy its head into `docs/studio-check.md`. Change one duration, save: `git diff` has one changed line.
  13. **The compositor.** *Add package features* on the check game's compositor, which has the tile feature: it adds `FlipbookRenderFeature` with its selector, and no second tile feature. Saved, the file has only the selectors of the new feature (finding 17). Closed and opened again, the scene still draws.
  14. **The scene editor (R7, R13).** `Create entity > 2D > Flipbook` makes an entity with the component, and *Add component* on an entity lists `Flipbook` in `2D`. The flipbook asset dropped in the scene makes one that shows its `idle`. Set to `walk`, **it plays in the scene view**, and so does the water of the map (two pictures each). `Direction` and `Flip X` changed in the property grid show at once. A click on the figure selects its entity, in a transparent corner of its frame too; a click beside it selects the map.
  15. **Order.** The figure at a z between the two layers is over the ground and under the bridge. A second figure at the same z, overlapping: the lower one is drawn over the higher, and moving one up or down swaps them (two pictures).
  16. **A flipbook edited while its scene is open (R13).** With the scene open, a frame's tile changed in the flipbook editor, then a save: the scene view shows the new frame without the scene being opened again. If it does not: a finding, and the README says to open the scene again.
  17. **The game.** With `Walker` on the figure: `dotnet run` under Xvfb, four pictures two seconds apart show the four directions, the water is not the same in two of them, and the walker passes behind the standing figure on the top side of its square and in front of it on the bottom side.
  18. The WPF Game Studio is not checked, as in step 2: say so.
- [ ] **Step 3: Fix what the list found** in the task that owns it, each fix with a test that would have caught it where a test can, and each gap of the plugin model as a row of `docs/plugin-findings.md`. A line "not as planned" that is not fixed is in the report with its reason.
- [ ] **Step 4: Give the packages check a flipbook.** The game `run.sh` writes outside the checkout gains a flipbook asset on its tile set and an entity that shows it beside the map; its build compiles the flipbook with the asset compiler, and the game reads one more pixel of its one frame, inside the flipbook's frame, before it prints its line. Run `tests/PackagesCheck/run.sh`. Expected: `TESSERA-PACKAGES-CHECK OK`.
- [ ] **Step 5: Commit and push**: `test(tessera): flipbooks and animated tiles in the check game, and what was seen`

### Task 18: The write-up and the gates

**Files:**
- Modify: `README.md`, `docs/plugin-findings.md`, `docs/design.md`, `docs/plan-step3.md`
- Create: `/mnt/project-files/tilemap/2026-11-2x-tessera-step3-report.md` *(the date of the day it is written)*, and the brief of step 4 beside it

- [ ] **Step 1: The README.** "What works today" gains: animated tiles made in the tile set editor and played by the two editors; the flipbook asset and its editor (the tree, the strip, the palette, Sheet, Mirror, the preview); the component, with a short example of game code (`Play`, `Queue`, `Direction` from `FlipbookDirections.FromVector`, the two events); the scene editor's pieces; a compositor now needs the two features, and *Add package features* adds the one it lacks; the order of flipbooks (decision 6) and what each sort mode gives; the warnings of the build. It says plainly: a renamed animation is not renamed in scenes and scripts; frames of several cells are drawn but made by hand in the file; the palette adds frames by a click or a drag inside it, not by a drop on the strip. "Not yet" keeps the sample, the list of animation names in the component's grid, painting in the scene view, real thumbnails, the WPF Game Studio (not checked).
- [ ] **Step 2: The findings.** Finding 18 (two features for two kinds of render objects), and every gap this step met, in the format of the file: at least what R11, R12 and R13 showed, where a fallback was taken or a public way was missing.
- [ ] **Step 3: The design.** A short dated note where this plan changed or settled it: section 7 (the small types are the runtime's; the fallback of a diagonal), section 8 (two render features; the Y key and its 256 steps a unit; what `Play`, `Queue` and `Stop` do), section 9 (Animation mode as built; the flipbook editor's palette, Sheet, Mirror, preview; no drop from the palette yet), section 11 (R2 and R7 for flipbooks, R11 to R13: what held and which fallback), section 16 (what was open and is now decided). The decisions D and C of this plan are copied to the design's lists.
- [ ] **Step 4: The plan.** Every box of `docs/plan-step3.md` is ticked or has a reason, and each task has its "Done differently" note, even when it says "nothing".
- [ ] **Step 5: The gates.** The whole of the three test projects, once, and `tests/PackagesCheck/run.sh`. Expected: every test passes, step 2's 537 and this step's, none skipped, and `TESSERA-PACKAGES-CHECK OK`. Then `git diff --stat feature/colorrise-tessera-editor..HEAD -- . ':!extensions/ColorRise.Tessera'` from the repository's root. Expected: empty.
- [ ] **Step 6: The review.** One fresh reviewer of the whole branch, as for steps 1 and 2, given: the design, this plan with its notes, `docs/studio-check.md`, the diff from `feature/colorrise-tessera-editor`, and the five lines of Review Focus. Fix what it finds that is a defect, each with a test that fails first; list the minors that wait, with a number each, in the report, after the five of step 2 that still wait.
- [ ] **Step 7: The report**, for Nicolas: what was built, the numbers (tests by project), which risks held and which fallbacks were taken, the findings, what waits, the pictures by name. Then a brief of step 4 (the sample game with Kenney's art: section 12, item 4, and section 14) in the shape of this step's brief. Update the project's memory (`tilemap-plugin`).
- [ ] **Step 8: Commit and push**: `docs(tessera): step 3 written up: README, findings, design notes`
