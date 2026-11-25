# ColorRise.Tessera, step 2: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task, in one session, with one fresh reviewer of the whole branch at the end (Nicolas's choice for Tessera, 2026-11-24). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A tile set and a tile map are made and edited in the cross-platform Game Studio, and a map painted there shows in a scene and in a game.

**Architecture:** The view models, the tools and everything that decides what is drawn are in `ColorRise.Tessera.Editor` and know no toolkit: they give a view pictures (arrays of pixels) and shapes, and take pointer and key events in canvas units. `ColorRise.Tessera.Editor.Avalonia` holds one custom-drawn control (the tile canvas), the palette built on it, and the two editor views. A map editor keeps the decoded cells beside the asset and writes each stroke to the asset's graph as one transaction; the graph stays the only truth, so undo, redo and changes made elsewhere come back through it. In scenes, a provider gives compositors the render feature, a sort mode orders flat things under a tilted camera, and an editor game service makes a map answer to a click.

**Tech Stack:** .NET 10, C# (latest), Stride 4.5.4-dev packages, Avalonia 12.1.1, xunit 2.9.3 with the editor test kit `Stride.Assets.Editor.Testing` for view models, `Avalonia.Headless.XUnit` on xunit.v3 with Skia for views, Lavapipe (software Vulkan) for graphics tests, Xvfb and xdotool for Game Studio by hand.

**Spec:** [design.md](design.md), approved by Nicolas on 2026-11-24. "The design" below means this file, and "section N" is a section of it. This step is sections 9 (but the flipbook editor), 10, 11 (R4 to R7), 12 (item 2) and 14. Step 1's plan is [plan.md](plan.md): its "Done differently" notes say what the code really is.

## Scope

Step 2 of the design's four (section 12), on `feature/colorrise-tessera-editor`, from `feature/colorrise-tessera` at `32df70ef0`: "The canvas and palette controls, the tile set editor and the tile map editor, templates, icons, entity factory, add-asset policy. At the end, a map painted in Game Studio shows in a scene and in a game."

Not in this step:

- The animation mode of the tile set editor, flipbooks and their editor (step 3). The tile set editor's toolbar has Select and Solid only.
- The sample game and Kenney's art (step 4). The by-hand check uses a small check game with an image made by a script.
- Views for the WPF Game Studio, painting in the scene editor, real thumbnails and previews, Tiled import, autotiling (design, sections 6 and 15).
- The minors of step 1's final review that Nicolas did not pick (see below).

## What step 2 decides

The brief of this step lists three things to decide. The first two questions went to Nicolas on cards, and he answered both on 2026-11-24 (23:49, Paris).

**Decided by Nicolas (D)**

1. **A sprite between two layers under a tilted camera** (finding 8): a sort mode of Tessera's own. Task 1 makes `TileAxisSortMode`, which orders the transparent stage back to front along one axis (Z by default) instead of by the distance to the camera's plane. A game sets it once on the transparent stage of its compositor. It is right for everything flat in that stage, with any camera, in games and in Game Studio's scene view (which draws with the game's compositor), and it keeps soft edges. Its cost: one setting in the compositor, and every transparent object of that stage sorts along the axis. Not chosen: an alpha cutoff with tiles that write depth (it needs a shader of Tessera's own, section 15 lists it under "Later"), and accepting what step 1 does.
2. **Which deferred minors of step 1's review go in**: M3, M4 and the small cleanups. Task 2 has M3 (a source id or a slot given twice after a merge) and M4 (`PixelsPerUnit` of 0 or less) as warnings of the build and lines in the editors, then the cleanups M11 (`TileChunk.Cells` can be written from outside), M12 (two comments of `TileMapSerializer` that do not match the code) and M13 (public members without a summary). The others wait (M1, M5, M6, M7, M10, M14: step 1's report has them).

**My calls, to check (C)**

3. **The render feature in compositors.** `TileRenderFeatureProvider` is in the `Editor` package, as the design's package table says and as SpriteStudio, UI and Particles do. It gives one `TileRenderFeature` for the transparent stage. A provider cannot set a stage's sort mode, and a `ForwardRenderer` needs an opaque stage (finding 6): the README says both.
4. **R5, the scene view follows an edited map.** Nothing is built: the editor rebuilds a changed asset and gives the component a new `TileMap`, which step 1's processor already handles. Checked by hand in Task 17. That the fast reload is a closed list of engine types (`EditorContentLoader.FastReloadTypes`) goes to the findings.
5. **R6, single keys.** As the design says: the tools and flips are commands with a gesture scope and no default shortcut, and the map canvas reads B, P, F, E, I, R, X, Y, Z and Shift+Z while it has the focus. A view test checks that the keys reach the tools, and do not while a layer's name is typed.
6. **R7, a click on a map in the scene view.** The scene editor makes its picking stage for meshes and sprites only, by type. An editor game service of Tessera adds a selector for that stage to the compositor's `TileRenderFeature`, and the feature draws each chunk there with the id of its component, as the sprite feature does. Fallback, as in the design: selection through the entity tree only, written as a finding.
7. **The canvas draws pictures of chunks from the start** (section 16 left it open): 16 by 16 cells of one layer in one picture, made on the CPU in the neutral project, coarser when zoomed out, in a cache with a size limit. One path for every zoom, and the view draws at most one image per visible chunk and layer. R4 measures it.
8. **Adding an image shows its grid in the editor before confirming**, in a bar of the editor and not in a dialog window: the image with the proposed grid, the numbers to change, then Add or Cancel. Nothing is in the asset before Add.
9. **Paste makes the copied cells the stamp**, as in Tiled: the user then clicks where they go. Copy and paste work between two maps of the session when both have the tile set.
10. **The eye of a layer is the asset's `Visible`**: the game hides the layer too, and it is an undo step. Lock and dimming are the editor's own, kept in the editor settings.
11. **Small things section 16 left open:** the templates are in the group `Sprite` of *Add asset*, with the default names `TileSet` and `TileMap`; a new map has one layer, `Ground`; the entity factory is `[Display(40, "Tile map", "2D")]`, after Sprite (10) and Background (30); the icons are drawn by a script, as Skein's.
12. **A check game for the by-hand run**, `tests/StudioCheck/`, laid out as the sample will be (a game library on packages, a Linux head, a solution that also holds the extension's projects). It is not the sample of step 4.

## Global Constraints

- Every file of this work is inside `extensions/ColorRise.Tessera/`. Nothing else in the repository changes, and nothing in the engine.
- Stride is reached through packages only, at `$(TesseraStrideVersion)` (`4.5.4-dev`). No `ProjectReference` to an engine project. The `src/` projects use no engine internal, by reflection or any other way. A test may, with a finding that says why.
- `ColorRise.Tessera.Editor` references no Avalonia package and no `*.Avalonia` assembly: the WPF Game Studio must still load it. Everything that names an Avalonia type is in `ColorRise.Tessera.Editor.Avalonia`.
- A gap in the plugin model is a row in `docs/plugin-findings.md`, in the format of the file: where, what was tried, what happened, the workaround, what would close the gap in Stride. Where Tessera meets a point Skein wrote about, the third table says whether it still holds.
- All projects: `net10.0`, `LangVersion` latest, `Nullable` enable, `ImplicitUsings` enable, version `0.1.0-dev`, MIT, authors `Color-Rise`.
- Code: explicit access modifiers, classes sealed by default, the `field` keyword in place of a new private backing field, no licence header in source files, plain English in comments and documentation, `<summary>` on public types and members. A comment says what the code is, not what it was.
- Commits: conventional titles with the scope `tessera` (`feat(tessera): ...`, `test(tessera): ...`, `docs(tessera): ...`, `build(tessera): ...`). Author `Nicolas Musset <musset.nicolas@gmail.com>` (run the two `git config` commands before the first commit). No `Co-Authored-By` line, no link to Claude, no model name anywhere.
- Branch `feature/colorrise-tessera-editor`, from `origin/feature/colorrise-tessera`. No other branch name, and no `-tmp` branch. Push it after each task: the container does not last.
- Nothing stays only in the conversation (Nicolas, 2026-11-24). In the commit of each task: the task's boxes ticked in `docs/plan-step2.md`, the result of its gate, a "Done differently" note under the task where what was done differs, and a row in `docs/plugin-findings.md` for each gap met. After each task, one line in the project's memory (`tilemap-plugin`) says which task is done.
- An editor changes an asset only through the asset's graph, inside a transaction of the session's undo service with a name. The names of this step: `Stamp`, `Rectangle`, `Fill`, `Erase`, `Move`, `Cut`, `Delete`, `Add layer`, `Remove layer`, `Rename layer`, `Move layer`, `Layer visibility`, `Add tile set`, `Remove tile set`, `Add image`, `Add sprite sheet`, `Remove source`, `Bind sprites`, `Rebind sprite`, `Solid`, `Tile data`.
- The id of a command starts with `Tessera.` and never changes once it is pushed (the user's shortcuts name it). No command of Tessera has a default shortcut.
- Values from the design: a chunk is 16 by 16 cells. Cells count Y up, and a chunk's text has its top row first. Flips are applied diagonal first, then horizontal, then vertical. The tools and their keys: Stamp (B), Rectangle (P), Fill (F), Erase (E), Pick (I), Select (R); X and Y flip the stamp, Z and Shift+Z rotate it. R4's map is 256 by 256 cells with four layers. The entity factory is `Create entity > 2D > Tile map`.
- Values of this plan: one canvas unit is one pixel of a tile image, and Y grows down on the canvas. Zoom is screen pixels per canvas unit, in the steps 0.125, 0.25, 0.5, 1, 2, 3, 4, 6, 8, 12, 16, 32. A picture has 8 bits per channel and straight alpha (`Stride.Core.Mathematics.Color`), its rows from the top.
- Tests: what a view model or a tool does is tested in `ColorRise.Tessera.Editor.Tests`, on the editor test kit, without a control. Only what needs a control is in `ColorRise.Tessera.Editor.Avalonia.Tests`. No gold image: a test reads pixels at known places. The test classes of the two editor projects do not run in parallel.
- After a failed run, run again only what failed. The whole solution is run at the gates named in the tasks.
- The engine build and Game Studio's build are long runs. They run in the container, in the background, with their output in a log file: no check on GitHub builds an extension.
- Screenshots of what is checked by hand go to `/mnt/project-files/tilemap/step2/`, named after what they show.
- A fallback of section 11 that loses a goal of section 6 stops the work: write what was found and ask Nicolas.
- Engine names in this plan come from the fork's `master` at `659bb239e`, which the branch is built on. A name marked *(check)* was not read while planning: read the model file named beside it before relying on it. Skein's files are read with `git show origin/feature/colorrise-skein:extensions/ColorRise.Skein/<path>`.

## Review Focus

Inputs the design implies and that will reach this code, most likely first. Each has its test in the task named.

1. **An image that is missing, renamed, not an image, or changed on disk while an editor is open**, and a sprite sheet whose sprites were renamed or removed. The editors open, the source says what is wrong with the file's name, its tiles show as missing, the other sources work, and nothing throws. A changed image shows after a refresh. (Tasks 5 and 8)
2. **A cell whose tile set or tile is gone.** It is drawn as a marked box, and it is still in the file, bit for bit, after a stroke on another cell of its chunk and after a save. (Tasks 11 and 12)
3. **A chunk's text edited by hand or left by a merge**, which cannot be read. The map opens, the chunk is marked and named in the editor's problems, no stroke writes into it, and the other chunks are edited as usual. (Task 11)
4. **Strokes on the negative side and far from the origin, and a fill with nothing to stop it.** A chunk at a negative position is made at its place in the file's order. A fill on a map without a frame stops at the used bounds of the layer, and on an empty layer at the chunk of the click. (Tasks 11 and 13)
5. **Undo, redo or a change from elsewhere while a stroke is open or a tile is selected.** The open stroke is dropped first, the editor follows the asset, and a selection that names things that are gone is cleared. (Tasks 9, 11 and 13)

## The pieces

What the tasks build, and who knows whom. The names are fixed here so that every task uses the same ones.

| Piece | Project | What it is |
|---|---|---|
| `TilePicture` | Editor | Pixels a view shows: `Width`, `Height`, `Color[] Pixels`, and a `Version` a view compares to make its bitmap again |
| `TilePictureService`, `TileSetPictures`, `TileSourceLayout`, `TileSlot` | Editor | The sources of a tile set asset as pictures: where each tile's pixels are, and where a palette draws it. One per session, shared by every editor |
| `CanvasView` | Editor | The zoom and the pan of a canvas, and the two conversions between screen and canvas |
| `ICanvasScene`, `CanvasFrame`, `CanvasSprite`, `CanvasShape` | Editor | What a canvas draws: a scene fills a frame with sprites (a part of a picture in a rectangle) and shapes, for the visible rectangle |
| `ICanvasInput` | Editor | What a canvas tells: pointer and key events, in canvas units |
| `TileSourceScene`, `TilePaletteViewModel`, `TileStamp` | Editor | One source of a tile set on a canvas, with its selection; the palette of a map editor; a block of cells with its flips |
| `TileSetEditorViewModel` and its items, `TileSetGraph`, `TileMapGraph` | Editor | The tile set editor; every change of a tile set or of a map besides its cells, through the asset's graph |
| `TileMapDocument`, `TileComposer`, `TileMapScene` | Editor | The decoded cells of a map with its strokes; the pixels of a chunk; the map on a canvas |
| `TileToolKind` and the six tools, `TilePainting`, `TileClipboard`, `TileMapEditorViewModel` and its items | Editor | The tile map editor |
| `TileCanvas`, `TilePalette` | Editor.Avalonia | The custom-drawn control, and the palette view built on it |
| `TileSetEditorView`, `TileMapEditorView` | Editor.Avalonia | The views of the two editors |
| `TileRenderFeatureProvider`, `TileMapEntityFactory`, `AddTileMapAssetPolicy`, `TilePickingService` | Editor | The scene editor's pieces |
| `TileAxisSortMode` | runtime | The sort mode of decision 1 |

---

### Task 0: The container, the branch and the plan

**Files:**
- Create: `docs/plan-step2.md` (a copy of this plan)

**Interfaces:**
- Produces: `dotnet` 10 on the `PATH`; the worktree `/home/user/stride-master` on the branch `feature/colorrise-tessera-editor`; Stride's packages at `4.5.4-dev` in `/home/user/stride-master/bin/packages`; step 1's tests green; the Avalonia Game Studio built, for Task 17.

- [x] **Step 1: Install the .NET 10 SDK** in `~/.dotnet` with Microsoft's `dotnet-install.sh` (`--channel 10.0`), and export `PATH=$HOME/.dotnet:$PATH` and `DOTNET_ROOT=$HOME/.dotnet`.

Run: `dotnet --version`
Expected: `10.0.4xx`, at least the version in the repository's `global.json`.

- [x] **Step 2: Make the branch** in a worktree, and set the author.

```bash
cd /home/user/stride
git fetch origin feature/colorrise-tessera feature/colorrise-skein
git worktree add -b feature/colorrise-tessera-editor /home/user/stride-master origin/feature/colorrise-tessera
cd /home/user/stride-master
git config user.name "Nicolas Musset" && git config user.email "musset.nicolas@gmail.com"
git lfs pull
```

- [x] **Step 3: Build the engine, the editor and the asset compiler**, as step 1 did, then Game Studio. These are long runs: start them in the background, one after the other, with their output in log files in the scratch folder. Check the free disk space first (`df -h /home/user`).

```bash
dotnet build sources/Stride.Editor.Avalonia.slnf
dotnet build sources/assets/Stride.AssetCompiler/Stride.AssetCompiler.csproj
dotnet build sources/editor/Stride.GameStudio.Avalonia.Desktop -p:StridePlatforms=Linux -p:StrideGraphicsApis=Vulkan
```

If the first two ask for a platform, add `-p:StridePlatforms=Linux -p:StrideGraphicsApis=Vulkan`.

Expected: the builds succeed, and `ls bin/packages` lists at `4.5.4-dev`: `Stride.Engine`, `Stride.Assets`, `Stride.Core.Assets`, `Stride.AssetCompiler`, `Stride.Assets.Editor`, `Stride.Assets.Editor.Avalonia`, `Stride.Assets.Editor.Testing`.

- [x] **Step 4: See step 1's tests pass** before anything changes.

Run: `dotnet test extensions/ColorRise.Tessera/ColorRise.Tessera.slnx`, then `extensions/ColorRise.Tessera/tests/PackagesCheck/run.sh`
Expected: PASS, 241 tests (235 and 6), none skipped, then `TESSERA-PACKAGES-CHECK OK`.

- [x] **Step 5: Read what the test kit gives today**, in `sources/editor/Stride.Core.Assets.Editor.Testing/` and in "Testing a plugin's view models" of `docs/build/plugins.md`. Three things this plan relies on:
  - `StrideEditorTestSessionOptions.Dispatcher`, which makes Avalonia's UI thread the session's (the view tests of this plan use it instead of Skein's `ScriptView` way);
  - a session that runs the plugins' `OnClose` actions when it is disposed;
  - a test project without an `.sdpkg` that builds without `StrideCompilerSkipBuild`: remove the property from `ColorRise.Tessera.Editor.Avalonia.Tests.csproj` and build it. If the build then runs the asset compiler, put the property back.

  Write the result of each in the third table of `docs/plugin-findings.md`, against the Skein finding it answers.

- [x] **Step 6: Copy this plan** to `extensions/ColorRise.Tessera/docs/plan-step2.md`, commit and push.

```bash
git add extensions/ColorRise.Tessera
git commit -m "docs(tessera): the plan of step 2"
git push -u origin feature/colorrise-tessera-editor
```

- [x] **Step 7: Gate.** If the engine or Game Studio cannot be built in the container, stop and tell Nicolas what failed, with the end of the log.

**Done (2026-11-25).** .NET SDK 10.0.404. The engine and editor build took 3 min 42 s, the asset compiler 1 min, Game Studio 36 s (all in the container, no error). The baseline: 235 and 6 tests pass, none skipped, and `TESSERA-PACKAGES-CHECK OK`. Step 5: all three hold on `659bb239e` (written against Skein's findings 8, 12 and 13 in the third table of `docs/plugin-findings.md`); `StrideCompilerSkipBuild` is removed from the view tests' project, whose build then runs no asset compiler.

**Done differently.** `git lfs pull` fills a file outside LFS in the engine's tree (`sources/editor/Stride.Core.Assets.Editor.Tests/Resources/TestBrokenImage.txt`) and stages it; it was restored before the first commit, and no commit of this step takes it. The plan was committed and pushed before the builds, to learn early that the push works.

### Task 1: A sort mode along an axis (finding 8)

Decision 1. Under a tilted camera, `BackToFrontSortMode` puts a sprite before or after both layers of a map, whatever its z, because it sorts by the distance from the camera's plane to the centre of each bounding box.

**Files:**
- Create: `src/ColorRise.Tessera/Rendering/TileAxisSortMode.cs`
- Modify: `tests/ColorRise.Tessera.Tests/Graphics/HarnessOptions.cs`, `GraphicsHarness.cs` (the sort mode of the transparent stage)
- Test: `tests/ColorRise.Tessera.Tests/Graphics/AxisSortTests.cs`, `tests/ColorRise.Tessera.Tests/Assets/SortModeYamlTests.cs`
- Modify: `README.md`, `docs/plugin-findings.md` (finding 8)

**Interfaces:**
- Consumes: step 1's harness (`GraphicsHarness.Run`, `HarnessScene`, `TestTiles.Flat`, `TestTiles.Map`, `TestTextures.Flat`), `TileMapComponent.GetLayerZ(string)`.
- Produces:

```csharp
namespace ColorRise.Tessera.Rendering;

/// Sorts a stage back to front along one axis of the world, whatever the tilt of the camera: flat things (tile layers,
/// sprites, flipbooks) that lie across the axis keep the order of their positions along it.
[DataContract("TileAxisSortMode")]
[Display("Back to front along an axis")]
public sealed class TileAxisSortMode : SortMode
{
    /// The axis the flat things are stacked along. Z by default: maps and sprites in the XY plane.
    public Vector3 Axis { get; set; } = Vector3.UnitZ;

    public override unsafe void GenerateSortKey(RenderView renderView, RenderViewStage renderViewStage, SortKey* sortKeys);
}

// tests
internal sealed class HarnessOptions { public SortMode? TransparentSortMode { get; init; }   /* null: BackToFrontSortMode */ }
```

- [ ] **Step 1: Write the failing tests**

```csharp
[Collection(GraphicsCollection.Name)]
public sealed class AxisSortTests
{
    private static readonly Color R = new(255, 0, 0, 255);
    private static readonly Color K = new(0, 0, 0, 255);
    private static readonly Color W = new(255, 255, 255, 255);

    // A map of 16 by 16 cells, so one chunk whose centre is (8, 8). Layer0 is black everywhere. Layer1 is red on the
    // columns 0 to 7 and empty on the columns 8 to 15. A white sprite, 2 units wide and high, is centred on (8, y), at a z
    // between the two layers. The camera is tilted 30 degrees around X and looks at the sprite's centre from 10 units
    // away, 4 units high in a frame of 64 pixels: 16 pixels a unit, and the row 32 of the frame crosses the sprite.
    [Theory]
    [InlineData(2f)]    // nearer to the camera's plane than the chunk's centre: the default sort draws the sprite last
    [InlineData(14f)]   // farther: the default sort draws it first
    public void A_sprite_between_two_layers_keeps_its_place_under_a_tilted_camera(float y)
    {
        var frame = Draw(y, new TileAxisSortMode());
        Assert.Equal(R, frame[24, 32]);   // half a unit left of the sprite's centre: Layer1 covers the sprite
        Assert.Equal(W, frame[40, 32]);   // half a unit right: Layer1 is empty there, and the sprite covers Layer0
    }

    [Fact]
    public void The_layers_keep_their_order_from_behind()
        // the same scene, the camera turned 180 degrees around Y and placed behind the map, looking at (8, 8):
        // at a place where both layers have tiles, the pixel is K (Layer0 is now in front)

    [Fact]
    public void An_axis_of_zero_sorts_as_Z()   // Axis = Vector3.Zero: the first test's two pixels, for y = 2
}

public sealed class SortModeYamlTests
{
    [Fact]
    public void A_compositor_asset_keeps_the_sort_mode_of_a_stage()
        // a GraphicsCompositorAsset with one RenderStage("Transparent", "Main") whose SortMode is
        // new TileAxisSortMode { Axis = Vector3.UnitY }, saved and loaded as TileMapAssetYamlTests saves a map:
        // the text has "!TileAxisSortMode", and the loaded stage's SortMode is a TileAxisSortMode with Axis (0, 1, 0)
}
```

`Draw(y, sortMode)` builds the scene with `HarnessOptions { TransparentSortMode = sortMode }`. Model for the tilted camera: `A_tilted_camera_keeps_the_order_of_the_layers` in `TileMapDrawTests.cs`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test extensions/ColorRise.Tessera/tests/ColorRise.Tessera.Tests --filter "FullyQualifiedName~AxisSortTests|FullyQualifiedName~SortModeYamlTests"`
Expected: FAIL to compile, `TileAxisSortMode` does not exist.

- [ ] **Step 3: Write `TileAxisSortMode`**, and give the harness its option. The key has the layout of `BackToFrontSortMode`, so that other features and `StateSortKey` (the Y sort of step 3's flipbooks) keep their meaning:

```csharp
Matrix.Invert(ref renderView.View, out var viewInverse);
var axis = Axis.LengthSquared() > 0 ? Vector3.Normalize(Axis) : Vector3.UnitZ;
// The side the camera looks at: the depth grows away from the camera
var away = Vector3.Dot(axis, viewInverse.Forward) >= 0 ? axis : -axis;
for (var i = 0; i < renderNodes.Count; i++)
{
    var renderObject = renderNodes[i].RenderObject;
    var depth = ~SortModeDistance.ComputeDistance(Vector3.Dot(renderObject.BoundingBox.Center, away));
    sortKeys[i] = new SortKey
    {
        Value = ((ulong)renderNodes[i].RootRenderFeature.SortKey << 56) | ((ulong)depth << 24) | (renderObject.StateSortKey >> 8),
        Index = i,
        StableIndex = renderObject.Index,
    };
}
```

Model: `sources/engine/Stride.Rendering/Rendering/SortModeDistance.cs`.

- [ ] **Step 4: See that the test bites.** Run the theory once with `TransparentSortMode = null`: both rows must fail (W at (24, 32) for y = 2, K at (40, 32) for y = 14). If a row passes with the default sort, the scene does not show the problem: move the sprite further from the row 8 until it does. Then run with the sort mode.

Run: the command of step 2.
Expected: PASS, 5 tests.

- [ ] **Step 5: Write it up.** `README.md`, in the part on compositors: a sprite or a flipbook goes between two layers by its z under any camera once the transparent stage of the game's compositor has the sort mode "Back to front along an axis"; without it, only under a camera that looks straight at the map. Finding 8: the workaround is now this sort mode, and the suggested change keeps its first half (a render feature cannot give the stage the distance to sort by). Where the compositor editor sets it is checked in Task 17.

- [ ] **Step 6: Commit and push**: `feat(tessera): a sort mode that keeps flat things in order under a tilted camera`

### Task 2: Warnings for what a merge or a hand edit leaves (M3, M4), and the small cleanups (M11, M12, M13)

Decision 2. Two commits: the warnings, then the cleanups.

**Files:**
- Create: `src/ColorRise.Tessera.Assets/TileAssetChecks.cs`
- Modify: `src/ColorRise.Tessera.Assets/TileSetAssetCompiler.cs`, `TileMapAssetCompiler.cs`, `TileMapAsset.cs` (`ToTileMap`), `TileSetAsset.cs` (`PixelsPerUnit`)
- Test: `tests/ColorRise.Tessera.Tests/Assets/TileAssetChecksTests.cs`, and new tests in `TileSetCompilerTests.cs` and `TileMapCompilerTests.cs`
- Modify, for the cleanups: `src/ColorRise.Tessera/TileChunk.cs` and the code that reads its cells, `src/ColorRise.Tessera/Serialization/TileMapSerializer.cs` (comments), `Directory.Build.props` (`NoWarn`), and the public members of `src/` that have no summary
- Test, for the cleanups: a new test in `tests/ColorRise.Tessera.Tests/TileLayerTests.cs`

**Interfaces:**
- Produces:

```csharp
namespace ColorRise.Tessera.Assets;

/// What is wrong in an asset that still loads: the lines the build warns with, and the editors show.
public static class TileAssetChecks
{
    /// Two sources with one id (the first is kept), and a PixelsPerUnit that is not above 0 (16 is used).
    public static IReadOnlyList<string> Of(TileSetAsset asset);

    /// A slot given twice (the first is kept), and a slot out of the range 1 to 65535.
    public static IReadOnlyList<string> Of(TileMapAsset asset);
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class TileAssetChecksTests
{
    [Fact]
    public void Two_sources_with_one_id_are_named()
        // sources "Town" (id 1), "Cave" (id 2), "Cave copy" (id 2): one line, which has "Cave", "Cave copy" and "2"

    [Theory]
    [InlineData(0f)] [InlineData(-16f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void A_pixels_per_unit_that_is_not_above_zero_is_named(float value)   // one line, which has "PixelsPerUnit" and "16"

    [Fact] public void A_tile_set_as_the_editor_makes_it_has_no_line()            // two sources with ids 1 and 2, PixelsPerUnit 16
    [Fact] public void A_slot_given_twice_is_named()                              // slots 1, 2, 2: one line, which has "2"
    [Fact] public void A_map_as_the_editor_makes_it_has_no_line()
}

// TileSetCompilerTests
[Fact]
public void Two_sources_with_one_id_build_the_first_and_warn()
    // two grid sources with the id 1, the first over an image of red tiles, the second over an image of blue ones:
    // the build succeeds, a warning has both names, and the tile 1:0,0 of the built set is red (AssetBuild.LoadImage)

[Fact]
public void A_pixels_per_unit_of_zero_builds_with_16_and_warns()   // the built TileSet has PixelsPerUnit 16

// TileMapCompilerTests
[Fact]
public void A_slot_given_twice_builds_with_the_first_and_warns()
    // slots 1 (set A), 1 (set B): the built map's TileSets[1] is A, and a warning has "1"
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileAssetChecksTests|FullyQualifiedName~Two_sources_with_one_id|FullyQualifiedName~A_pixels_per_unit_of_zero|FullyQualifiedName~A_slot_given_twice`. Expected: FAIL to compile, `TileAssetChecks` does not exist.

- [ ] **Step 3: Write the checks and use them.**
  - Both compilers call `TileAssetChecks.Of` in `Prepare` and give each line to `result.Warning`, with the asset's URL in front as their other warnings have.
  - The tile set compiler skips a source whose id an earlier source has, in `Prepare` and in the command, and builds with `PixelsPerUnit` 16 when the asset's is not above 0.
  - `TileMapAsset.ToTileMap` keeps the first tile set of a slot.
  - `TileSetAsset.PixelsPerUnit` gets `[DataMemberRange(0.01, 2)]`, so that the property grid refuses 0.
  - The versions of the two build commands go up by one (the tile set's to 3, the tile map's to 2): a build made before must run again to warn.

- [ ] **Step 4: Run the tests**

Run: `dotnet test extensions/ColorRise.Tessera/tests/ColorRise.Tessera.Tests --filter "FullyQualifiedName~Assets"`
Expected: PASS, with step 1's asset tests.

- [ ] **Step 5: Commit and push**: `fix(tessera): warn about a source id or a slot given twice, and a PixelsPerUnit that is not above 0`

- [ ] **Step 6: The cleanups M11, M12 and M13.**
  - **M11.** `TileChunk.Cells` becomes `public ReadOnlySpan<TileCell> Cells { get; }` over an array that only the chunk holds: nothing outside the runtime can write a cell without `TileLayer.SetCell`, so `Count` and the saved content cannot drift. `Set` and `Clone` use the array; the readers (`TileLayer.GetCell`, `TileMapComponent`, `TileMapSerializer`, `TileRenderFeature`, `TileLayerAsset`) read the span, and none of them keeps it across an `await` or in a field. First the test, which does not compile before the change in a way a test can show, so it pins what stays true:

    ```csharp
    [Fact]
    public void A_chunk_gives_its_cells_to_read_and_counts_them()
    {
        var layer = new TileLayer();
        layer.SetCell(new Int2(17, 2), Cell);
        var chunk = layer.GetChunk(new Int2(1, 0))!;
        Assert.Equal(TileChunk.Size * TileChunk.Size, chunk.Cells.Length);
        Assert.Equal(Cell, chunk.Cells[2 * TileChunk.Size + 1]);
        Assert.Equal(1, chunk.Count);
        Assert.True(typeof(TileChunk).GetProperty(nameof(TileChunk.Cells))!.PropertyType == typeof(ReadOnlySpan<TileCell>));
    }
    ```

  - **M12.** In `TileMapSerializer`, the comment of `Format` says what the code does: the number is the serializer's own, a map of another format throws `InvalidDataException` with "build the game again", and nothing ties it to the version of the build command (so a change of the layout raises both: `Format`, and `TileMapCommand.Version`, which Step 3 above already raised). The comment about a tile set whose content is not there says what happens today; read the code under it and write that, not the other way round. No behaviour changes in this point.
  - **M13.** `CS1591` leaves the `NoWarn` of `Directory.Build.props` (`CS1573` stays, with a comment that fits), and every public type and member of the four `src/` projects that the compiler then names gets a `<summary>` in plain, simple English that says what it is for, not its name again. Where the member is a constructor of a view, of a view model or of a plugin class that the editor makes, `/// <summary>Made by the editor.</summary>` is enough. From here on every task of this plan writes its summaries as it goes: the build has no `CS1591` warning at the end of each task.
  - Run: the whole of `tests/ColorRise.Tessera.Tests` (the change of `Cells` touches the readers), then `dotnet build extensions/ColorRise.Tessera/ColorRise.Tessera.slnx 2>&1 | grep -c CS1591`. Expected: PASS, and `0`.
  - Commit and push: `refactor(tessera): chunk cells read only from outside, comments of the serializer, summaries on public members`

### Task 3: Asset view models, templates and images

**Files:**
- Create: `src/ColorRise.Tessera.Editor/ViewModels/TileSetViewModel.cs`, `TileMapViewModel.cs`
- Create: `src/ColorRise.Tessera.Assets/TileMapAssetFactory.cs`, `EditorImages.cs`
- Create: `src/ColorRise.Tessera.Assets/Templates/Assets/TileSet.sdtpl`, `TileMap.sdtpl`, `.sdtpl/TileSet.png`, `.sdtpl/TileMap.png`
- Create: `src/ColorRise.Tessera.Assets/Resources/draw-icons.py`, `TileSetThumbnail.png`, `TileMapThumbnail.png`, `TileMapComponent.png`
- Modify: `src/ColorRise.Tessera.Assets/ColorRise.Tessera.Assets.csproj`, `ColorRise.Tessera.Assets.sdpkg`
- Modify: `tests/ColorRise.Tessera.Editor.Tests/TesseraTest.cs`, `ColorRise.Tessera.Editor.Tests.csproj` (links `..\ColorRise.Tessera.Tests\Assets\TestImages.cs`), and the Avalonia test project the same way
- Test: `tests/ColorRise.Tessera.Editor.Tests/TemplateTests.cs`

**Interfaces:**
- Produces:

```csharp
namespace ColorRise.Tessera.Editor.ViewModels;

[AssetViewModel<TileSetAsset>]
public sealed class TileSetViewModel : AssetViewModel<TileSetAsset> { public TileSetViewModel(AssetViewModelConstructionParameters parameters); }

[AssetViewModel<TileMapAsset>]
public sealed class TileMapViewModel : AssetViewModel<TileMapAsset> { public TileMapViewModel(AssetViewModelConstructionParameters parameters); }

namespace ColorRise.Tessera.Assets;

/// Makes the map a template creates: one layer, "Ground".
public sealed class TileMapAssetFactory : AssetFactory<TileMapAsset> { public const string FirstLayerName = "Ground"; public override TileMapAsset New(); }
```

The session of the editor tests, in `TesseraTest.cs`, beside `TesseraTest` (which stays, for step 1's tests). Model: Skein's `tests/ColorRise.Skein.Editor.Tests/SkeinTest.cs`.

```csharp
internal sealed class TesseraSession : IAsyncDisposable
{
    public static DispatcherTask<TesseraSession> OpenAsync(params (string Location, Asset Asset)[] assets);
    public static DispatcherTask<TesseraSession> OpenAsync(StrideEditorTestSessionOptions options, params (string Location, Asset Asset)[] assets);
        // adds the plugin and InitializeSession = true to the options; a test gives Services (dialogs), StandInEditors or a Dispatcher
    public EditorTestSession Kit { get; }
    public SessionViewModel Session { get; }
    public IUndoRedoService UndoRedo { get; }
    public List<string?> Steps { get; }                                  // the names of the undo steps made since the session opened
    public string FileOf(string location, string extension);             // the file of an asset of the package
    public UFile WriteImage(string location, int width, int height, Func<int, int, Color> pixel);
        // a PNG at Assets/<location> of the package, made with TestImages.Png; returns its full path
    public TileSetViewModel TileSetAt(string location);
    public TileMapViewModel MapAt(string location);
    public TemplateDescription Template(string name);                    // src/ColorRise.Tessera.Assets/Templates/Assets/<name>.sdtpl, found from the test's folder
    public Task<List<AssetViewModel>> RunTemplateAsync(TemplateDescription template, string folder);   // as "Add asset" does
}
```

Model for `Template` and `RunTemplateAsync`: `ProjectTemplate()` and `RunAsync` in Skein's `SkeinCommandsTests.cs`.

- [ ] **Step 1: Write the failing tests** in `TemplateTests.cs`

```csharp
[Fact]
public async Task A_tile_set_and_a_map_have_view_models_of_their_own()
{
    await using var session = await TesseraSession.OpenAsync(("Tiles/Town", new TileSetAsset()), ("Maps/Town", new TileMapAssetFactory().New()));
    Assert.IsType<TileSetViewModel>(session.Session.AllAssets.Single(a => a.Name == "Town" && a.Asset is TileSetAsset));
    Assert.IsType<TileMapViewModel>(session.Session.AllAssets.Single(a => a.Asset is TileMapAsset));
}

[Fact]
public async Task The_tile_map_template_makes_a_map_with_one_layer()
{
    await using var session = await TesseraSession.OpenAsync();
    var template = session.Template("TileMap");
    Assert.Equal(("Tile map", "Sprite", "TileMap"), (template.Name, template.Group, template.DefaultOutputName));

    var first = Assert.IsType<TileMapViewModel>(Assert.Single(await session.RunTemplateAsync(template, "Maps")));
    Assert.Equal("/TestGame/Maps/TileMap", first.Url);
    Assert.Equal(["Ground"], first.Asset.Layers.Select(layer => layer.Name));
    Assert.Empty(first.Asset.TileSets);

    var second = Assert.Single(await session.RunTemplateAsync(template, "Maps"));
    Assert.Equal("/TestGame/Maps/TileMap_2", second.Url);
}

[Fact]
public async Task The_tile_set_template_makes_an_empty_tile_set()
    // ("Tile set", "Sprite", "TileSet"); the asset has no source, TileSize (16, 16) and PixelsPerUnit 16

[Fact]
public void The_package_file_lists_both_templates()
    // ColorRise.Tessera.Assets.sdpkg, read as text, has "Templates/Assets/TileSet.sdtpl" and "Templates/Assets/TileMap.sdtpl",
    // and both files and the two pictures of .sdtpl/ exist

[Fact]
public void The_images_are_declared_and_embedded()
{
    var assembly = typeof(TileMapAsset).Assembly;
    var thumbnails = assembly.GetCustomAttributes<StaticThumbnailAttribute>().ToDictionary(a => a.AssetType, a => a.ResourceName);
    Assert.Equal("TileSetThumbnail.png", thumbnails[typeof(TileSetAsset)]);
    Assert.Equal("TileMapThumbnail.png", thumbnails[typeof(TileMapAsset)]);
    var icon = Assert.Single(assembly.GetCustomAttributes<TypeImageAttribute>(), a => a.Type == typeof(TileMapComponent));
    foreach (var name in (string[])["TileSetThumbnail.png", "TileMapThumbnail.png", icon.ResourceName])
    {
        Assert.Contains(assembly.GetManifestResourceNames(), resource => resource.EndsWith(name, StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test extensions/ColorRise.Tessera/tests/ColorRise.Tessera.Editor.Tests --filter "FullyQualifiedName~TemplateTests"`
Expected: FAIL to compile, `TesseraSession` does not exist.

- [ ] **Step 3: Write the templates.** The two files, exactly (the tile set needs no factory: a new `TileSetAsset` is the empty one):

```yaml
!TemplateAssetFactory
Id: 0d0fbc70-453c-40f5-b6d3-d181003f308d
AssetTypeName: TileSetAsset
Name: Tile set
Scope: Asset
Description: Tiles cut from images on a grid, or taken from sprite sheets, for tile maps
Group: Sprite
DefaultOutputName: TileSet
Icon: .sdtpl/TileSet.png
```

```yaml
!TemplateAssetFactory
Id: eb9c4c56-59cf-4411-8076-6d5717d33c8d
AssetTypeName: TileMapAsset
Name: Tile map
Scope: Asset
Description: A map of layers painted with the tiles of one or several tile sets
Group: Sprite
DefaultOutputName: TileMap
FactoryTypeName: TileMapAssetFactory
Icon: .sdtpl/TileMap.png
```

In `ColorRise.Tessera.Assets.sdpkg`, after `Dependencies`:

```yaml
TemplateFolders:
    - Path: !dir Templates/Assets
      Group: Assets
      Files:
        - !file Templates/Assets/TileSet.sdtpl
        - !file Templates/Assets/TileMap.sdtpl
```

In the project file, as Skein's `Assets` project: `<None Include="Templates\**" />`, `<None Remove="Resources\*.png" />`, `<EmbeddedResource Include="Resources\*.png" />`. Check that `dotnet pack` puts the templates in the package (`unzip -l` on the `.nupkg`).

- [ ] **Step 4: Draw the images** with `Resources/draw-icons.py` (Python with Pillow, as Skein's script, whose sizes and colours it keeps): a tile set is a block of three by three squares with a gap between them, a tile map is the same grid with some squares left out, like a small map. White on a transparent ground at 128 pixels for the two thumbnails, the same in blue at 48 pixels for the templates, a dark glyph with a light edge at 16 pixels for the component. `EditorImages.cs` declares them (`[assembly: StaticThumbnail(...)]`, `[assembly: TypeImage(typeof(TileMapComponent), "TileMapComponent.png")]`). Commit the script and the pictures.

- [ ] **Step 5: Write the two view models, the factory and `TesseraSession`**, then run the tests.

Run: the command of step 2, then the whole editor test project.
Expected: PASS, 5 new tests, and step 1's editor tests.

- [ ] **Step 6: Commit and push**: `feat(tessera): tile set and tile map templates, images and view models`

### Task 4: The scene editor's pieces: the feature provider, the entity factory, the asset drop

**Files:**
- Create: `src/ColorRise.Tessera.Editor/Rendering/TileRenderFeatureProvider.cs`
- Create: `src/ColorRise.Tessera.Editor/Scene/TileMapEntityFactory.cs`, `AddTileMapAssetPolicy.cs`
- Test: `tests/ColorRise.Tessera.Editor.Tests/SceneEditorTests.cs`
- Modify: `docs/plugin-findings.md`

**Interfaces:**
- Consumes: `TileMapViewModel` (Task 3); step 1's `TileRenderFeature` and `TileTransparentRenderStageSelector { RenderStage? TransparentRenderStage }`.
- Produces:

```csharp
namespace ColorRise.Tessera.Editor.Rendering;

/// Gives the compositors Game Studio makes (previews, thumbnails, a new game's compositor, "Add package features") the
/// feature that draws tile maps, in the transparent stage.
public sealed class TileRenderFeatureProvider : IRenderFeatureProvider
{
    public IEnumerable<RootRenderFeature> CreateRenderFeatures(RenderStage opaqueStage, RenderStage transparentStage);
}

namespace ColorRise.Tessera.Editor.Scene;

[Display(40, "Tile map", "2D")]
public sealed class TileMapEntityFactory : EntityFactory { public override Task<Entity> CreateEntity(IEntityHierarchyItemViewModel parent); }

/// A tile map asset dropped in a scene becomes an entity with a TileMapComponent that names it.
public sealed class AddTileMapAssetPolicy : CreateComponentPolicyBase<TileMapAsset, TileMapViewModel>
{
    protected override EntityComponent CreateComponentFromAsset(IEntityHierarchyItemViewModel parent, TileMapViewModel asset);
}
```

Models: `sources/plugins/SpriteStudio/Stride.SpriteStudio.Editor/SpriteStudioRenderFeatureProvider.cs`; `sources/editor/Stride.Assets.Editor/EntityFactories/TwoDimensionEntityFactory.cs` (the category `2D` is registered there); `AddSpriteSheetAssetPolicy.cs` under `sources/editor/Stride.Assets.Editor/AssetEditors/EntityHierarchyEditor/ViewModels/AddAssetPolicies/`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void The_provider_gives_the_tile_feature_for_the_transparent_stage()
{
    var (opaque, transparent) = (new RenderStage("Opaque", "Main"), new RenderStage("Transparent", "Main"));
    var feature = Assert.IsType<TileRenderFeature>(Assert.Single(new TileRenderFeatureProvider().CreateRenderFeatures(opaque, transparent)));
    var selector = Assert.IsType<TileTransparentRenderStageSelector>(Assert.Single(feature.RenderStageSelectors));
    Assert.Same(transparent, selector.TransparentRenderStage);
}

[Fact]
public void The_editor_finds_the_provider()
    => Assert.Contains(RenderFeatureProviders.Enumerate(), provider => provider is TileRenderFeatureProvider);

[Fact]
public void The_factory_is_in_the_2D_category_after_Background()
{
    var display = typeof(TileMapEntityFactory).GetCustomAttribute<DisplayAttribute>()!;
    Assert.Equal((40, "Tile map", "2D"), (display.Order, display.Name, display.Category));
}

[Fact]
public async Task Create_entity_makes_an_entity_with_a_tile_map_component()
{
    await using var session = await TesseraSession.OpenAsync(("MainScene", new SceneAsset()));
    var editor = new SceneEditorViewModel(Assert.IsType<SceneViewModel>(session.Session.AllAssets.Single(a => a.Name == "MainScene")));
    try
    {
        Assert.True(await editor.Initialize());
        editor.CreateEntityInRootCommand.Execute(new TileMapEntityFactory());
        var entity = Assert.Single(editor.HierarchyRoot.Children.OfType<EntityViewModel>());
        Assert.Equal("Tile map", entity.Name);
        Assert.Null(Assert.Single(entity.AssetSideEntity.Components.OfType<TileMapComponent>()).Map);
    }
    finally
    {
        editor.Destroy();
    }
}

[Fact]
public async Task A_dropped_tile_map_becomes_a_component_that_names_it()
    // a session with a scene and ("Maps/Town", a map); in the scene's editor:
    // var policy = new AddTileMapAssetPolicy(); policy.Accept(typeof(TileMapAsset)) is true (check which type it is asked with);
    // policy.CreateComponentFromAsset(editor.HierarchyRoot, map) is a TileMapComponent whose Map has the attached
    // reference of the map: AttachedReferenceManager.GetAttachedReference(component.Map)?.Id == map.Id
```

Model for the scene editor in a test: `Add_entity_makes_a_dialogue_runner_in_the_scene` in Skein's `SkeinCommandsTests.cs`.

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~SceneEditorTests`. Expected: FAIL to compile.

- [ ] **Step 3: Write the three classes.** The factory names its entity with `ComputeNewName(parent, "Tile map")` and makes it with `CreateEntityWithComponent`. The policy's component has `Map = ContentReferenceHelper.CreateReference<TileMap>(asset)`.

- [ ] **Step 4: Run the tests.** Expected: PASS, 5 tests.

- [ ] **Step 5: Findings.** A row: a provider gives render features but cannot set what a feature needs from a stage, here the sort mode of Task 1; the game sets it by hand. (Whether *Add package features* and a new compositor take the feature is seen in Task 17.)

- [ ] **Step 6: Commit and push**: `feat(tessera): tile maps in the scene editor: render feature provider, entity factory and asset drop`

### Task 5: The pictures of a tile set

The editors draw tiles from the source images, as the sprite sheet editor loads them, with the color key applied (section 9). This task makes those pictures once for the session, with no view.

**Files:**
- Create: `src/ColorRise.Tessera.Editor/Pictures/TilePicture.cs`, `TileSlot.cs`, `TileSourceLayout.cs`, `TileSetPictures.cs`, `TilePictureService.cs`
- Modify: `src/ColorRise.Tessera.Editor/TesseraEditorPlugin.cs` (`InitializeSession` registers the service, and disposes it on close)
- Create: `tests/ColorRise.Tessera.Editor.Tests/TestTown.cs`
- Test: `tests/ColorRise.Tessera.Editor.Tests/TilePictureTests.cs`

**Interfaces:**
- Consumes: `TileSetViewModel`, `TesseraSession` (Task 3); `TileSlicer.Grid`, `TileSlicer.IsEmpty`, `TileSlicer.Sheet`, `NamedRegion` (step 1); the engine's `SpriteEditorImageCache` (`Image? RetrieveImage(UFile?)`, which reads a changed file again) in `Stride.Assets.Editor`.
- Produces, in `ColorRise.Tessera.Editor.Pictures`:

```csharp
/// Pixels a view shows: 8 bits per channel, straight alpha, row by row from the top.
public sealed class TilePicture
{
    public TilePicture(int width, int height);                  // transparent
    public int Width { get; }
    public int Height { get; }
    public Color[] Pixels { get; }
    public Color this[int x, int y] { get; set; }
    public int Version { get; }                                 // a view makes its bitmap again when this is not the one it drew
    public void Touch();                                        // after Pixels were written
}

/// A tile of a source: where its pixels are, and where a palette draws it. A tile whose sprite is gone has no picture.
public sealed record TileSlot(TileId Tile, Rectangle Bounds, TilePicture? Picture, Rectangle Region, string? SpriteName)
{
    public bool IsMissing => Picture is null;
}

/// One source of a tile set as the editors show it. A grid source is its image, each tile in its place. A sheet source is
/// a flow of boxes, one per bound sprite, by number, in rows at most 256 units wide (or as wide as the widest sprite).
public sealed class TileSourceLayout
{
    public TileSetSource Source { get; }
    public bool IsGrid { get; }
    public Int2 Size { get; }                                   // of the layout, in canvas units
    public TilePicture? Image { get; }                          // the image of a grid source; null for a sheet, or when it cannot be read
    public string? Problem { get; }                             // why the source has no picture, with the file's name
    public IReadOnlyList<TileSlot> Slots { get; }               // the tiles, by id; the missing ones of a sheet included
    public IReadOnlyList<string> UnboundSprites { get; }        // the sprites of the sheet that no binding names
    public TileSlot? SlotAt(Vector2 position);
    public TileSlot? SlotOf(TileId tile);
}

/// The sources of one tile set asset, made again when the asset, a sprite sheet it uses, or (on Refresh) a file changes.
public sealed class TileSetPictures
{
    public TileSetViewModel TileSet { get; }
    public IReadOnlyList<TileSourceLayout> Sources { get; }
    public Int2 LargestTile { get; }                            // in pixels; the tile size of the set when it has no tile
    public TileSlot? Find(TileId tile);
    public void Refresh();
    public event EventHandler? Changed;
}

/// The pictures of the session's tile sets, over one image cache.
public sealed class TilePictureService : IDisposable
{
    public static TilePictureService Of(ISessionViewModel session);   // the registered service; model: Skein's YarnAnalysisService.Of
    public TileSetPictures Get(TileSetViewModel tileSet);             // one object per asset
    public TileSetPictures? Get(TileSet? reference);                  // by the reference a slot of a map holds; null when the asset is gone
}
```

`TestTown`, the fixture of the editor tests:

```csharp
internal static class TestTown
{
    // "town.png", 64 by 32: four by two tiles of 16 pixels. The tile (c, r) is flat, of the colour Flat(c, r), but:
    // (0, 0) has four quarters (top-left red, top-right green, bottom-left blue, bottom-right yellow, as step 1's
    // TestTextures.Quarters), and (3, 1) is fully transparent
    public static Color Flat(int column, int row) => new((byte)(40 + 60 * column), (byte)(80 + 100 * row), 200, 255);
    public static Color Pixel(int x, int y);
    public static TileSetAsset TileSet(string image = "town.png");      // one grid source "Town", id 1, TileSize (16, 16)
    // A map whose slot 1 is the tile set, with one layer per array ("Layer0", ...). A layer is rows of tokens, the top row
    // first, from the cell (0, 0) up: "." or "c,r" plus flip letters, for the tile 1:c,r of the slot 1 ("2,1h")
    public static TileMapAsset Map(TileSetAsset tileSet, string tileSetLocation, params string[][] layers);
    public static TileCell Cell(int column, int row, TileFlip flip = TileFlip.None);   // new TileCell(1, TileId.Parse($"1:{column},{row}"), flip)
}
```

- [ ] **Step 1: Write the failing tests.** Each opens a `TesseraSession` with `("Tiles/Town", TestTown.TileSet())`, writes the image with `session.WriteImage("Tiles/town.png", 64, 32, TestTown.Pixel)`, and asks `TilePictureService.Of(session.Session).Get(session.TileSetAt("Tiles/Town"))`.

```csharp
[Fact]
public async Task A_grid_source_is_its_image_with_a_slot_per_tile_that_is_not_empty()
    // Sources has one layout: IsGrid, Size (64, 32), Problem null, 7 slots (the transparent tile is none)
    // the slot of 1:2,1: Bounds == Region == (32, 16, 16, 16), Picture is the layout's Image, Picture[32, 16] == Flat(2, 1)
    // SlotAt((33, 17)) is that slot; SlotAt((50, 20)) is null (the empty tile); SlotAt((-1, 0)) and SlotAt((64, 0)) are null
    // Find(TileId.Parse("1:2,1")) is that slot; LargestTile == (16, 16)

[Fact]
public async Task The_color_key_makes_its_pixels_transparent_and_can_empty_a_tile()
    // ColorKeyEnabled, ColorKeyColor = Flat(1, 0): Image[20, 4].A == 0, and 6 slots

[Theory]
[InlineData(null)]                // no file
[InlineData("not an image")]      // a file with these bytes
public async Task An_image_that_cannot_be_read_is_a_problem_of_its_source_only(string? content)
    // a second grid source "Cave" (id 2) over "cave.png", which is fine: the layout of "Town" has a Problem that
    // contains "town.png", no slot and no Image; the layout of "Cave" has its slots; nothing throws

[Theory]
[InlineData(0, 16)] [InlineData(16, 0)] [InlineData(128, 16)] [InlineData(-4, 16)]
public async Task A_tile_size_that_makes_no_tile_gives_no_slot_and_does_not_throw(int width, int height)

[Fact]
public async Task A_sheet_source_is_a_box_per_bound_sprite_with_the_missing_ones_marked()
    // a SpriteSheetAsset "Tiles/Objects" with the sprites "tree" (0, 0, 16, 32) and "rock" (16, 16, 16, 16) of the image;
    // a sheet source whose bindings are 1 -> "tree" and 2 -> "gone":
    // not IsGrid; the slot of 1:#1 has Region (0, 0, 16, 32), a picture, and Bounds of the size (16, 32);
    // the slot of 1:#2 IsMissing, with SpriteName "gone"; the two Bounds do not overlap and are inside Size;
    // UnboundSprites == ["rock"]

[Fact]
public async Task A_change_of_the_asset_makes_the_pictures_again()
    // the source's TileSize set to (32, 32) through the asset's graph: Changed is raised, and the layout has 2 slots

[Fact]
public async Task A_changed_image_file_shows_after_a_refresh()
    // the file written again with every pixel white (and a later write time): Image[40, 4] is still Flat(2, 0);
    // after Refresh(), Changed was raised and Sources[0].Image[40, 4] is white

[Fact]
public async Task The_service_gives_one_object_per_tile_set_and_finds_it_by_its_reference()
    // Get(viewModel) twice is the same object; Get(AttachedReferenceManager.CreateProxyObject<TileSet>(asset.Id, "Tiles/Town"))
    // is that object; a reference whose id no asset has gives null; Get((TileSet?)null) gives null
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TilePictureTests`. Expected: FAIL to compile.

- [ ] **Step 3: Write the five classes.**
  - Paths: an image's path is relative to the folder of the asset that names it (`UPath.Combine(assetItem.FullPath.GetParent(), image)`), as in `TileSetAssetCompiler.Prepare`, which is the model for the sprites of a sheet too.
  - A picture is made from the cache's `Image` (8-bit RGBA or BGRA) once per image and color key, and made again when the cache gives another `Image` object for the path.
  - Which grid positions are tiles is `TileSlicer.Grid` then `TileSlicer.IsEmpty` with the color key, as the build decides it. Which sprites are bound, missing and unbound is `TileSlicer.Sheet`.
  - `TileSetPictures` listens to `Session.AssetPropertiesChanged` for its tile set and for the sprite sheets its sources name (model: `EditorGameGraphicsCompositorService.AssetPropertyChanged`).
  - Every failure of a file is caught and becomes the `Problem` of the source.

- [ ] **Step 4: Run the tests.** Expected: PASS, 12 tests (two theories).

- [ ] **Step 5: Commit and push**: `feat(tessera): the pictures of a tile set's sources for the editors`

### Task 6: The tile canvas

**Files:**
- Create: `src/ColorRise.Tessera.Editor/Canvas/CanvasView.cs`, `ICanvasScene.cs`, `CanvasFrame.cs`, `ICanvasInput.cs`
- Create: `src/ColorRise.Tessera.Editor.Avalonia/Controls/TileCanvas.cs`, `PictureBitmaps.cs`
- Create: `tests/ColorRise.Tessera.Editor.Avalonia.Tests/TestApplication.cs`, `CanvasProbe.cs`
- Modify: `tests/ColorRise.Tessera.Editor.Avalonia.Tests/ColorRise.Tessera.Editor.Avalonia.Tests.csproj` (the package `Avalonia.Skia` at `$(TesseraAvaloniaVersion)`, unless the editor's package already brings it)
- Test: `tests/ColorRise.Tessera.Editor.Tests/CanvasViewTests.cs`, `tests/ColorRise.Tessera.Editor.Avalonia.Tests/TileCanvasTests.cs`

**Interfaces:**
- Consumes: `TilePicture` (Task 5).
- Produces, in `ColorRise.Tessera.Editor.Canvas`:

```csharp
/// The zoom and the pan of a canvas.
public sealed class CanvasView
{
    public static IReadOnlyList<float> ZoomSteps { get; }       // 0.125, 0.25, 0.5, 1, 2, 3, 4, 6, 8, 12, 16, 32
    public float Zoom { get; set; }                             // screen pixels per canvas unit; 1 at first; kept between the first and last steps
    public Vector2 Origin { get; set; }                         // the canvas point at the top-left corner of the view
    public Vector2 ToCanvas(Vector2 screen);
    public Vector2 ToScreen(Vector2 canvas);
    public void ZoomAt(Vector2 screen, int steps);              // to the next step up (+) or down (-); the canvas point under `screen` stays under it
    public void Pan(Vector2 screenDelta);                       // what was under the pointer follows it
    public RectangleF Visible(Vector2 viewSize);
    public void Fit(RectangleF bounds, Vector2 viewSize);       // the largest step that shows all of bounds, centred
    public event EventHandler? Changed;
}

public readonly record struct CanvasSprite(TilePicture Picture, Rectangle Source, RectangleF Destination, float Opacity = 1);
public enum CanvasMark { Outline, Fill, Hatch, Cross }
public readonly record struct CanvasShape(CanvasMark Mark, RectangleF Bounds, Color Color);

public sealed class CanvasFrame
{
    public List<CanvasSprite> Sprites { get; }                  // drawn first, in order
    public List<CanvasShape> Shapes { get; }                    // drawn over the sprites and the grid, in order
}

/// What a canvas draws. Everything is in canvas units.
public interface ICanvasScene
{
    RectangleF? Bounds { get; }                                 // what "fit" shows; null when there is nothing
    Vector2 GridSize { get; }                                   // the cell of the grid the view draws; zero for none
    void Collect(RectangleF visible, float zoom, CanvasFrame frame);
    event EventHandler? Changed;                                // the view draws again
}

public enum CanvasButton { Left, Right }
[Flags] public enum CanvasModifiers { None = 0, Shift = 1, Control = 2, Alt = 4 }
public enum CanvasKey { B, P, F, E, I, R, X, Y, Z, Escape, Delete, Enter }

/// What a canvas tells. Positions are in canvas units.
public interface ICanvasInput
{
    void PointerPressed(Vector2 position, CanvasButton button, CanvasModifiers modifiers);
    void PointerMoved(Vector2 position, CanvasModifiers modifiers);
    void PointerReleased(Vector2 position, CanvasButton button, CanvasModifiers modifiers);
    void PointerLeft();
    bool KeyPressed(CanvasKey key, CanvasModifiers modifiers);  // true when the key did something
}
```

In `ColorRise.Tessera.Editor.Avalonia.Controls`:

```csharp
/// Draws a scene with hard pixels, zooms around the cursor with the wheel, pans with the middle button or with Space and
/// the left button, and tells the rest of the pointer and the keys to its input.
public sealed class TileCanvas : Control
{
    public static readonly StyledProperty<ICanvasScene?> SceneProperty;
    public static readonly StyledProperty<ICanvasInput?> InputProperty;
    public static readonly StyledProperty<CanvasView> ViewProperty;      // a view of its own when none is given
    public static readonly StyledProperty<bool> ShowGridProperty;        // true
    public static readonly StyledProperty<Color> GridColorProperty;      // Avalonia's Color; white at a quarter of opacity
    public static readonly StyledProperty<bool> FitOnFirstLayoutProperty; // the tile set editor and the palette fit their source once
}
```

For the tests of views:

```csharp
// TestApplication: one application for the assembly, with Skia drawing for real (UseSkia, UseHeadlessDrawing = false),
// the Fluent theme and Stride's editor styles. Model: sources/editor/Stride.Editor.Avalonia.Tests/TestApplication.cs
internal static class CanvasProbe
{
    public static Stride.Core.Mathematics.Color Pixel(Window window, int x, int y);   // renders, then reads the frame; model: RenderAndReadPixel in OffscreenGameViewTests.cs
    public static Window Show(Control content, int width, int height);
}
internal sealed class RecordingInput : ICanvasInput { public List<string> Events { get; } }   // "press 20,40 Left None", "move ...", "key B None"; KeyPressed returns Handles
```

- [ ] **Step 1: Write the failing tests of `CanvasView`**

```csharp
[Fact]
public void Zooming_keeps_the_point_under_the_cursor()
{
    var view = new CanvasView();
    view.ZoomAt(new Vector2(100, 50), 1);
    Assert.Equal(2, view.Zoom);
    Assert.Equal(new Vector2(50, 25), view.Origin);
    Assert.Equal(new Vector2(100, 50), view.ToCanvas(new Vector2(100, 50)));
}

[Fact] public void Zoom_stops_at_the_first_and_the_last_step()      // 20 steps down give 0.125, 40 steps up give 32, and Zoom = 1000 gives 32
[Fact] public void A_zoom_between_two_steps_goes_to_the_next_step() // Zoom = 2.5f, then ZoomAt(default, 1): 3; ZoomAt(default, -1) from 2.5: 2
[Fact] public void Pan_moves_the_origin_by_the_screen_distance_over_the_zoom()   // Zoom 4, Pan((8, -12)): Origin (-2, 3)
[Fact] public void Visible_is_the_view_in_canvas_units()            // Zoom 2, Origin (10, 20), view (200, 100): (10, 20, 100, 50)

[Fact]
public void Fit_shows_all_of_the_bounds_at_the_largest_step_centred()
{
    var view = new CanvasView();
    view.Fit(new RectangleF(0, 0, 64, 32), new Vector2(640, 480));
    Assert.Equal(8, view.Zoom);                                      // 12 would need 768 pixels
    Assert.Equal(new Vector2(-8, -14), view.Origin);
}

[Fact] public void Changed_is_raised_once_per_change()
```

- [ ] **Step 2: Write the failing tests of `TileCanvas`.** Each shows a canvas of 64 by 64 pixels in a window, with a scene made in the test (`TestScene`: a list of sprites and shapes it gives back, a `GridSize`).

```csharp
[AvaloniaFact]
public void Sprites_are_drawn_with_hard_pixels_at_the_zoom()
    // a picture of 2 by 2 (red, green / blue, yellow) as a sprite at (0, 0, 2, 2); Zoom 8, Origin (0, 0); ShowGrid false:
    // the pixels (3, 3), (12, 3), (3, 12), (12, 12) are red, green, blue, yellow; (7, 7) is exactly red and (8, 8) exactly
    // yellow (no blend at the seam); (40, 40) is the background

[AvaloniaFact] public void A_part_of_a_picture_and_an_opacity_are_drawn()   // Source (1, 0, 1, 1) at Opacity 0.5 over the background: a blend of green and the background, within 2
[AvaloniaFact] public void The_grid_is_drawn_when_a_cell_is_at_least_four_pixels()
    // GridSize (2, 2), GridColor opaque white, Zoom 8: the pixel (16, 5) is white and (5, 5) is not; at Zoom 1 no pixel of the row 5 is white
[AvaloniaFact] public void Shapes_are_drawn_over_the_sprites()               // a Fill, an Outline, a Hatch and a Cross: a pixel of each is its colour
[AvaloniaFact] public void A_picture_that_changed_is_drawn_again()           // a pixel written, Touch(), the scene's Changed: the new colour shows

[AvaloniaFact]
public void The_wheel_zooms_around_the_cursor_and_the_input_hears_nothing()
    // a wheel step up at (32, 32): Zoom is the next step, and View.ToCanvas((32, 32)) is what it was; no event recorded

[AvaloniaFact] public void The_middle_button_pans()                          // down at (10, 10), move to (30, 25), up: Origin moved by (-20, -15) over the zoom; no event recorded
[AvaloniaFact] public void Space_and_the_left_button_pan()

[AvaloniaFact]
public void The_pointer_reaches_the_input_in_canvas_units()
    // Zoom 4, Origin (10, 20): left down at (40, 80), move to (44, 80), up; then a right click:
    // "press 20,40 Left None", "move 21,40 None", "release 21,40 Left None", then the Right ones; leaving the control gives "left"

[AvaloniaFact]
public void A_key_reaches_the_input_only_while_the_canvas_has_the_focus()
    // a TextBox beside the canvas; a click on the canvas gives it the focus; B is pressed: "key B None"; Shift+Z: "key Z Shift";
    // the focus goes to the text box and B is typed: nothing more is recorded, and the box has "b"

[AvaloniaFact] public void A_key_the_input_did_not_use_is_not_marked_handled()   // a KeyDown handler on the window sees Handled false for I when the input returns false, true when it returns true
```

- [ ] **Step 3: Run them to see them fail.** Filters: `FullyQualifiedName~CanvasViewTests` in the editor tests, `FullyQualifiedName~TileCanvasTests` in the Avalonia tests. Expected: FAIL to compile.

- [ ] **Step 4: Write `CanvasView` and the interfaces, then `TileCanvas`.**
  - The canvas draws in this order: its background, the sprites, the grid, the shapes. A sprite is a bitmap drawn with `BitmapInterpolationMode.None`. Lines and outlines are one screen pixel wide at every zoom. The grid is drawn only while a cell is at least 4 screen pixels.
  - `PictureBitmaps` keeps one `WriteableBitmap` per `TilePicture`, weakly, and writes it again when the picture's `Version` changed.
  - The canvas can take the focus, and takes it on a pointer press. The key of an event is the key as the keyboard layout names it (`KeyEventArgs.Key`), so that Z is the key marked Z.
  - While Space is down or the middle button is held, the input hears nothing.

- [ ] **Step 5: Run the tests.** Expected: PASS, 7 and 11 tests. If Skia cannot draw without a window in the container, stop and write what failed: the pixel tests of the views depend on it (its fallback is to test the frame a scene gives, without pixels, and to look at screenshots by hand).

- [ ] **Step 6: Commit and push**: `feat(tessera): the tile canvas: zoom, pan, pictures with hard pixels, grid and shapes`

### Task 7: Stamps, the scene of a source, and the palette

**Files:**
- Create: `src/ColorRise.Tessera.Editor/Palette/TileStamp.cs`, `TileSourceScene.cs`, `TilePaletteViewModel.cs`
- Create: `src/ColorRise.Tessera.Editor.Avalonia/Controls/TilePalette.axaml`, `TilePalette.axaml.cs`
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileStampTests.cs`, `TilePaletteTests.cs`, `tests/ColorRise.Tessera.Editor.Avalonia.Tests/TilePaletteViewTests.cs`

**Interfaces:**
- Consumes: Tasks 5 and 6.
- Produces, in `ColorRise.Tessera.Editor.Palette`:

```csharp
/// A block of cells that a tool paints. Its rows count from the bottom, as cells do.
public sealed class TileStamp : IEquatable<TileStamp>
{
    public TileStamp(Int2 size, IReadOnlyList<TileCell> cells);      // cells[y * size.X + x]
    public static TileStamp Single(TileCell cell);
    public Int2 Size { get; }
    public TileCell this[int x, int y] { get; }
    public bool IsEmpty { get; }                                     // no cell has a tile
    public TileStamp FlipX();
    public TileStamp FlipY();
    public TileStamp RotateClockwise();
    public TileStamp RotateCounterClockwise();
}

public enum TilePickMode { One, Block, Many, Paint }

/// One source of a tile set on a canvas: its picture, an outline per tile, its marks and its selection.
public sealed class TileSourceScene : ICanvasScene, ICanvasInput
{
    public TileSourceLayout? Layout { get; set; }
    public TilePickMode Mode { get; set; }
    public IReadOnlyList<TileId> Selection { get; }                  // in the order of the tile ids
    public void Select(IEnumerable<TileId> tiles);
    public event EventHandler? SelectionChanged;
    public event EventHandler<TileId>? Painted;                      // Paint mode: a tile under the pressed pointer, once per drag
    public event EventHandler? PaintEnded;
    public Func<TileId, CanvasMark?>? MarkOf { get; set; }           // a mark the owner adds on a tile
}

/// The palette of a map editor for one tile set of the map.
public sealed class TilePaletteViewModel : DispatcherViewModel
{
    public TilePaletteViewModel(IViewModelServiceProvider services, TileSetPictures pictures, ushort slot);
    public ushort Slot { get; }
    public TileSetPictures Pictures { get; }
    public IReadOnlyList<TileSourceLayout> Sources { get; }          // follows the tile set
    public TileSourceLayout? SelectedSource { get; set; }            // the first one at first
    public TileSourceScene Scene { get; }                            // Block mode for a grid source, One for a sheet
    public CanvasView View { get; }
    public TileStamp? Picked { get; }                                // what the last click or drag picked, as cells of Slot
    public event EventHandler? PickedChanged;
    public void Show(TileId tile);                                   // selects the tile's source and the tile; does not raise PickedChanged
}
```

How a stamp turns. A cell's flips are diagonal first, then horizontal, then vertical (`TileFlip`), and `d`, `h`, `v` below are those three bits:

| Method | The cell at (x, y) of a stamp `w` by `h` goes to | Its bits become |
|---|---|---|
| `FlipX` | (w - 1 - x, y) | `h` toggled |
| `FlipY` | (x, h - 1 - y) | `v` toggled |
| `RotateClockwise` | (y, w - 1 - x), in a stamp `h` by `w` | `d' = !d`, `h' = !v`, `v' = h` |
| `RotateCounterClockwise` | (h - 1 - y, x), in a stamp `h` by `w` | `d' = !d`, `h' = v`, `v' = !h` |

How a source scene picks:

- `One`: a click selects the tile under it.
- `Block`: a press, a drag and a release select the tiles of the rectangle of grid positions between the press and the release; a click is a block of one.
- `Many`: a click selects one tile, Ctrl and a click adds or removes one, a drag selects a rectangle, a click on no tile clears.
- `Paint`: each tile the pointer passes while the left button is down raises `Painted`, once per drag, and the release raises `PaintEnded`.
- The scene gives: the picture (the image of a grid source as one sprite, or one sprite per box of a sheet), an `Outline` per visible tile, a red `Cross` on a missing tile, the owner's marks, and a `Fill` and an `Outline` on each selected tile. Its `GridSize` is zero: margins and spacings make the grid of a source uneven, so the outlines are its grid.
- A block of a grid source becomes a stamp with its top row on top: the stamp's cell (x, y) is the tile at the column `c0 + x` and the row `r1 - y`, and a position without a tile is an empty cell.

- [ ] **Step 1: Write the failing tests**

```csharp
// TileStampTests: `Stamp` is 3 by 2 with six different tiles; `flips` are the eight values of TileFlip
[Theory] [MemberData(nameof(Flips))] public void Two_flips_give_the_stamp_back(TileFlip start)         // FlipX twice, FlipY twice
[Theory] [MemberData(nameof(Flips))] public void Four_quarter_turns_give_the_stamp_back(TileFlip start)
[Theory] [MemberData(nameof(Flips))] public void A_turn_and_its_opposite_cancel(TileFlip start)

[Fact]
public void Flip_X_mirrors_the_block_and_flips_each_cell()
{
    var stamp = new TileStamp(new Int2(2, 1), [TestTown.Cell(0, 0), TestTown.Cell(1, 0)]).FlipX();
    Assert.Equal(TestTown.Cell(1, 0, TileFlip.Horizontal), stamp[0, 0]);
    Assert.Equal(TestTown.Cell(0, 0, TileFlip.Horizontal), stamp[1, 0]);
}

[Fact]
public void A_clockwise_turn_stands_a_row_up_with_its_left_cell_on_top()
{
    var stamp = new TileStamp(new Int2(2, 1), [TestTown.Cell(0, 0), TestTown.Cell(1, 0)]).RotateClockwise();
    Assert.Equal(new Int2(1, 2), stamp.Size);
    Assert.Equal(TestTown.Cell(0, 0, TileFlip.Diagonal | TileFlip.Horizontal), stamp[0, 1]);
    Assert.Equal(TestTown.Cell(1, 0, TileFlip.Diagonal | TileFlip.Horizontal), stamp[0, 0]);
}

[Fact] public void An_empty_cell_stays_empty_through_flips_and_turns()
[Fact] public void A_stamp_of_another_size_than_its_cells_is_refused()        // ArgumentException

// TilePaletteTests: the town set in the slot 3; `Press`, `Drag(from, to)` and `Click(at)` call the scene's ICanvasInput
[Fact] public async Task A_click_picks_one_tile()                  // Click((20, 4)): Picked.Size (1, 1), Picked[0, 0] == new TileCell(3, TileId.Parse("1:1,0"))
[Fact] public async Task A_drag_picks_a_block_with_its_top_row_on_top()
    // Drag((4, 4), (40, 20)): Size (3, 2); Picked[0, 1] is 1:0,0, Picked[2, 1] is 1:2,0, Picked[0, 0] is 1:0,1, Picked[2, 0] is 1:2,1;
    // the same from (40, 20) to (4, 4); PickedChanged was raised once per drag
[Fact] public async Task A_position_without_a_tile_is_a_hole_in_the_block()   // Drag((36, 20), (60, 20)): Size (2, 1), [0, 0] is 1:2,1, [1, 0] is empty
[Fact] public async Task A_click_on_no_tile_keeps_what_was_picked()
[Fact] public async Task A_sheet_source_picks_one_tile_whatever_the_drag()
[Fact] public async Task Show_selects_the_source_and_the_tile_without_picking()
[Fact] public async Task The_sources_follow_the_tile_set()         // a source added through the graph: two sources; the selected one removed: the first is selected
[Fact] public async Task The_scene_gives_the_picture_the_outlines_and_the_selection()
    // Collect over the whole layout: one sprite (the image), 7 outlines, and after Click((20, 4)) a Fill whose Bounds are (16, 0, 16, 16)
[Fact] public async Task Many_adds_and_removes_with_control_and_paints_in_paint_mode()
    // Mode Many: Click, then Ctrl+Click on another tile: two selected; Ctrl+Click on the first: one. Mode Paint: a drag over
    // three tiles and back raises Painted three times, then PaintEnded once

// TilePaletteViewTests (Avalonia)
[AvaloniaFact]
public async Task A_drag_in_the_palette_makes_a_stamp()
    // a TilePalette of 300 by 200 over the town set, fitted: the mouse goes down in the tile (0, 0), moves to the tile (1, 0)
    // and goes up (positions from View.ToScreen): the view model's Picked has the size (2, 1); the pixel in the middle of
    // the tile (2, 1) is TestTown.Flat(2, 1)
```

The session of a view test is opened with `Dispatcher = DispatcherService.Create()` in its options, so that the test stays on Avalonia's UI thread ("Testing a plugin's view models" in `docs/build/plugins.md`). If Task 0 found that this does not work, use Skein's way (`tests/ColorRise.Skein.Editor.Avalonia.Tests/ScriptView.cs`).

- [ ] **Step 2: Run them to see them fail.** Filters: `FullyQualifiedName~TileStampTests|FullyQualifiedName~TilePaletteTests`, and `FullyQualifiedName~TilePaletteViewTests`. Expected: FAIL to compile.

- [ ] **Step 3: Write the stamp, the scene and the palette's view model**, then the `TilePalette` view: a row to choose the source, shown when the tile set has more than one, over a `TileCanvas` bound to the view model's `Scene` (as scene and as input) and `View`, fitted once.

- [ ] **Step 4: Run the tests.** Expected: PASS (29 cases in the editor tests with the theories, 1 in the Avalonia tests).

- [ ] **Step 5: Commit and push**: `feat(tessera): stamps and the tile palette`


### Task 8: The tile set editor: its sources

**Files:**
- Create: `src/ColorRise.Tessera.Editor/TileSetEditor/TileSetEditorViewModel.cs`, `TileSourceItemViewModel.cs`, `PendingImageSourceViewModel.cs`, `TileSetGraph.cs`
- Modify: `src/ColorRise.Tessera.Editor/Pictures/TilePictureService.cs` (a public `Load`), `tests/ColorRise.Tessera.Editor.Tests/TesseraTest.cs` (`Dialogs`, `EditTileSetAsync`), `TestTown.cs` (`Objects`)
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileSetEditorSourcesTests.cs`

**Interfaces:**
- Consumes: Tasks 2, 3, 5, 6 and 7; the engine's `AssetEditorViewModel<T>` (`Initialize`, `Destroy`, `UndoRedoService`, `EditorProperties`, `ShowAssetProperties`), `IAddChildViewModel`, `IAssetPropertyProviderViewModel` (model: `SpriteInfoViewModel` in `Stride.Assets.Editor`), `IEditorDialogService`, `SpriteSheetViewModel`.
- Produces, in `ColorRise.Tessera.Editor.TileSetEditor`:

```csharp
[AssetEditorViewModel<TileSetViewModel>]
public sealed class TileSetEditorViewModel : AssetEditorViewModel<TileSetViewModel>, IAddChildViewModel
{
    public TileSetEditorViewModel(TileSetViewModel asset);
    public TileSetPictures Pictures { get; }
    public IReadOnlyObservableList<TileSourceItemViewModel> Sources { get; }   // in the asset's order; follows the asset
    public TileSourceItemViewModel? SelectedSource { get; set; }      // the first one at first; shown in the property grid
    public TileSourceScene Scene { get; }                             // the selected source
    public CanvasView View { get; }
    public PendingImageSourceViewModel? PendingImage { get; }         // an image that waits for Add or Cancel
    public IReadOnlyList<string> Problems { get; }                    // TileAssetChecks.Of, each source's Problem, missing sprites by name
    public string? Message { get; }                                   // why the last thing asked was not done
    public ICommandBase AddImageCommand { get; }                      // a file picker, then ProposeImage
    public ICommandBase AddSpriteSheetCommand { get; }                // an asset picker of SpriteSheetAsset, then AddSpriteSheet
    public ICommandBase RemoveSourceCommand { get; }                  // the selected source, after a question
    public ICommandBase RefreshCommand { get; }                       // Pictures.Refresh()
    public void ProposeImage(UFile image);                            // an absolute path
    public bool AddSpriteSheet(SpriteSheetViewModel sheet);
}

/// A source in the editor's list, and what the property grid shows for it.
public sealed class TileSourceItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel
{
    public TileSetSource Source { get; }
    public int Id { get; }
    public string Name { get; }                                       // follows the asset
    public bool IsGrid { get; }
    public TileSourceLayout? Layout { get; }                          // follows Pictures
    public string? Problem { get; }
}

/// An image the user is about to add: its grid can be changed and seen before anything is in the asset.
public sealed class PendingImageSourceViewModel : DispatcherViewModel
{
    public UFile Image { get; }
    public Int2 ImageSize { get; }                                    // (0, 0) when the file cannot be read
    public string Name { get; set; }                                  // the file's name without its extension at first
    public int TileWidth { get; set; }   public int TileHeight { get; set; }    // the tile set's TileSize at first
    public int MarginX { get; set; }     public int MarginY { get; set; }
    public int SpacingX { get; set; }    public int SpacingY { get; set; }
    public int Columns { get; }          public int Rows { get; }     // what TileSlicer.Grid makes of these numbers
    public string? Problem { get; }                                   // while it is set, Add cannot run
    public ICanvasScene Scene { get; }                                // the image, with an Outline per tile of the grid
    public ICommandBase ConfirmCommand { get; }
    public ICommandBase CancelCommand { get; }
}

/// Every change the editors make to a tile set asset, through its graph. The caller owns the transaction.
internal static class TileSetGraph
{
    public static GridTileSetSource AddImage(TileSetViewModel tileSet, string name, UFile image, Int2 tileSize, Int2 margin, Int2 spacing);
    public static SpriteSheetTileSetSource AddSheet(TileSetViewModel tileSet, string name, SpriteSheet reference, IEnumerable<string> spriteNames);
    public static void RemoveSource(TileSetViewModel tileSet, TileSetSource source);       // and the definitions of its tiles
    public static int BindSprites(TileSetViewModel tileSet, SpriteSheetTileSetSource source, IEnumerable<string> spriteNames);   // how many were bound
}

// TilePictureService
public TilePicture? Load(UFile absolutePath, Color? colorKey, out string? problem);

// TesseraSession
public TestEditorDialogService Dialogs { get; }                       // given to the kit in the options' Services
public Task<TileSetEditorViewModel> EditTileSetAsync(string location); // makes the editor's view model, awaits Initialize(), destroys it with the session
```

What the editor does:

- **Ids and names.** A new source gets the id `LastSourceId + 1`, and `LastSourceId` is written with it, both through the graph: an undo gives the id back. A name that a source already has gets a number: `town`, `town 2`.
- **An image.** `ProposeImage` only makes `PendingImage`; the asset is touched by `ConfirmCommand`, in one step `Add image`, and the new source is selected. The path is kept as the asset's other `UFile` members are in a loaded session, and the saved file names it relative to its own folder (the last test checks the file; if the session does not do it, `TileSetGraph.AddImage` makes the path relative to the asset's folder, which Task 5 and the compiler both read). A tile size equal to the tile set's is stored as (0, 0), which means "the tile set's" to `TileSlicer.Grid`, so that the source follows the tile set.
- **A problem of a pending image**: the file cannot be read as an image; a tile width or height below 1; a margin or a spacing below 0; no tile fits (`Columns * Rows == 0`).
- **A sprite sheet.** `AddSpriteSheet` adds a sheet source with every sprite bound, by number in the sheet's order (`TileSlicer.Bind`), in one step `Add sprite sheet`. A sheet the tile set already has is refused, with a `Message`.
- **Opening the editor binds the new sprites of its sheets** (section 9), in one step `Bind sprites`, and only when there is something to bind: an editor opened on a tile set that is up to date makes no step and leaves the asset clean. The same happens when a sheet of the session changes while the editor is open.
- **Removing a source** asks first (`MessageBoxAsync`, Yes or No): "Remove the source 'Town'? Maps that use its tiles keep their cells, and show them as missing." Yes removes the source and the definitions of its tiles in one step `Remove source`, and selects the source before it.
- **Drops.** `CanAddChildren` accepts `SpriteSheetViewModel`s, and `AddChildren` calls `AddSpriteSheet` for each. A dropped image file calls `ProposeImage` (the view of Task 10 reads the files with `AssetDragDropHelper.GetFiles`).
- **The property grid** shows the selected source: the item gives the node of its `TileSetSource` (`Session.AssetNodeContainer.GetNode(Source)`), as `SpriteInfoViewModel` does for a sprite, and hides `Id`, `Bindings` and `LastNumber` (`ShouldConstructMember`). With no source, `ShowAssetProperties()`.

- [ ] **Step 1: Write the failing tests.** Each opens the town set with its image, and `await session.EditTileSetAsync("Tiles/Town")`. `TestTown.Objects()` is a `SpriteSheetAsset` over `town.png` with the sprites `grass` (0, 0, 16, 16), `water` (16, 0, 16, 16) and `wall` (32, 0, 16, 16).

```csharp
[Fact] public async Task The_editor_opens_on_the_first_source_and_makes_no_step()
    // Sources has "Town"; SelectedSource is it; Scene.Layout is its layout; session.Steps is empty; the asset is not dirty

[Fact] public async Task An_image_is_proposed_with_the_tile_sets_tile_size_and_nothing_is_added()
    // ProposeImage(cave.png, 64 by 32): PendingImage has Name "cave", TileWidth 16, TileHeight 16, Columns 4, Rows 2, Problem null;
    // its Scene collects 1 sprite and 8 outlines; the asset has one source; Steps is empty

[Theory]
[InlineData(32, 32, 0, 0, 2, 1)] [InlineData(16, 16, 8, 0, 3, 2)] [InlineData(15, 15, 0, 1, 4, 2)]
public async Task The_numbers_change_the_grid(int tile, int tileY, int margin, int spacing, int columns, int rows)

[Theory]
[InlineData(0, 16)] [InlineData(16, -1)] [InlineData(128, 16)]
public async Task A_grid_with_no_tile_is_a_problem_and_cannot_be_added(int width, int height)   // Problem set, ConfirmCommand.IsEnabled false

[Fact] public async Task A_file_that_is_not_an_image_is_proposed_with_its_problem()          // "notes.png" with the bytes of a text: Problem has "notes.png"

[Fact] public async Task Add_makes_the_source_in_one_step_and_undo_takes_it_back()
    // Confirm: two sources, the new one "cave" with the id 2, LastSourceId 2, selected, PendingImage null, Steps == ["Add image"];
    // Undo: one source, LastSourceId 1; Redo: two again, and Pictures.Sources has two layouts

[Fact] public async Task Cancel_leaves_nothing()
[Fact] public async Task A_second_image_of_the_same_name_gets_a_number()                      // "town 2"
[Fact] public async Task The_saved_asset_names_the_image_relative_to_its_folder()             // the saved file has the line "Image: cave.png"

[Fact] public async Task A_sprite_sheet_is_added_with_all_its_sprites_bound()
    // AddSpriteSheet: a sheet source "Objects" with the bindings 1 grass, 2 water, 3 wall; Steps == ["Add sprite sheet"]
[Fact] public async Task A_sheet_the_set_already_has_is_refused_with_a_message()
[Fact] public async Task Dropped_sprite_sheets_are_added_and_other_assets_refused()          // CanAddChildren of a TileMapViewModel: false, with a message

[Fact] public async Task Opening_binds_the_new_sprites_of_a_sheet_in_one_step()
    // the asset has the sheet source with grass and water bound, the sheet has wall too: after EditTileSetAsync,
    // Bindings has 3 wall and Steps == ["Bind sprites"]
[Fact] public async Task A_sprite_added_to_the_sheet_while_the_editor_is_open_is_bound()

// Review Focus 1
[Fact] public async Task A_renamed_sprite_leaves_a_missing_tile_and_a_new_one()
    // "water" renamed "sea" in the sheet, through its graph: the binding 2 still says "water" and its slot IsMissing;
    // a binding 4 "sea" is there; Problems has a line with "water"
[Fact] public async Task A_removed_sprite_leaves_a_missing_tile_and_the_editor_stays_open()
[Fact] public async Task An_image_that_is_gone_is_a_problem_line_and_the_source_can_still_be_removed()
    // "town.png" deleted, then RefreshCommand: Problems has a line with "town.png", Scene.Layout has no slot, nothing throws

[Fact] public async Task Remove_asks_and_removes_the_source_and_its_tile_data_in_one_step()
    // a definition of 1:0,0 with Collision Solid; Dialogs.Answers has Yes: no source, no definition, Steps == ["Remove source"],
    // Dialogs.Messages[0] has "Town"; Undo brings both back
[Fact] public async Task Remove_answered_no_changes_nothing()

[Fact] public async Task The_property_grid_shows_the_selected_source_then_the_asset()
    // SelectedSource set: EditorProperties has the members Name, Image, TileSize, Margin, Spacing and no Id;
    // SelectedSource null: the asset's properties (PixelsPerUnit is there)
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileSetEditorSourcesTests`. Expected: FAIL to compile.
- [ ] **Step 3: Write `TileSetGraph`, the three view models and the helpers of the session.** The lists follow the asset by listening to its graph (`Asset.PropertyGraph`, its `Changed` and `ItemChanged`), not by being told by the commands, so that an undo and an edit in the property grid show the same way.
- [ ] **Step 4: Run the tests.** Expected: PASS (26 cases).
- [ ] **Step 5: Commit and push**: `feat(tessera): the tile set editor: image and sprite sheet sources`

### Task 9: The tile set editor: tiles and their data

**Files:**
- Create: `src/ColorRise.Tessera.Editor/TileSetEditor/TileSetEditorMode.cs`, `TileDataViewModel.cs`
- Modify: `TileSetEditorViewModel.cs`, `TileSetGraph.cs`
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileSetEditorTilesTests.cs`

**Interfaces:**
- Consumes: Task 8; `TileSourceScene.Mode`, `Selection`, `Painted`, `PaintEnded`, `MarkOf` (Task 7); Skein's `src/ColorRise.Skein.Editor/PropertyGrid/YarnNodeHeadersSelection.cs` and `ViewModels/YarnNodeHeadersViewModel.cs`, the model of a property grid over objects that are not in the asset.
- Produces:

```csharp
public enum TileSetEditorMode { Select, Solid }

// TileSetEditorViewModel
public TileSetEditorMode Mode { get; set; }                           // Select: the scene picks Many; Solid: it paints
public IReadOnlyList<TileId> SelectedTiles { get; }                   // the scene's selection
public IReadOnlyList<string> RebindCandidates { get; }                // sprite names, when one missing tile of a sheet is selected
public ICommandBase RebindSpriteCommand { get; }                      // parameter: a sprite name of RebindCandidates

/// The data of the selected tiles for the property grid: copies with a graph of their own, written back in one step.
public sealed class TileDataViewModel : DispatcherViewModel, IPropertyProviderViewModel
{
    public IReadOnlyList<TileId> Tiles { get; }
}

// TileSetGraph
public static void SetDefinition(TileSetViewModel tileSet, TileDefinition definition);   // replaces, inserts at its sorted place, or removes when IsDefault
public static bool SetSolid(TileSetViewModel tileSet, TileId tile, bool solid);           // false when it already was
public static void Rebind(TileSetViewModel tileSet, SpriteSheetTileSetSource source, int missingNumber, int fromNumber);
```

What the editor does:

- **Solid mode** (RPG Maker's passage settings): a press on a tile sets it solid when it was not, and clears it when it was; the tiles the drag passes get the same value as the first; the release makes one step `Solid`, and no step when nothing changed. `Scene.MarkOf` gives a `Hatch` for a solid tile in both modes. Only the `Solid` flag of `TileCollision` is touched.
- **Tile data in the property grid.** With tiles selected, the grid shows one `TileDefinition` per tile (several at once, as the grid does for several assets): `Collision`, `Kind` and `Properties`. The objects are copies (a tile without data has a default copy), in a graph of their own; each change of a copy is written to the asset with `SetDefinition`, in one step named `Tile data`. `Tile` is read only and `Animation` is not shown (it is step 3's). After an undo, a redo or any other change of `Tiles`, the copies are made again.
- **The spike comes first** (Step 1 below): the property grid has to edit a `Dictionary<string, TileProperty>` and a flags enum of an object outside the asset. If it cannot, the fallback is a small panel of the editor's own for `Kind`, the solid flag and a list of properties, over the same `SetDefinition`; write the finding, and tell Nicolas in the report.
- **Rebinding** (section 6: "a missing tile that the editor shows and can rebind"). With one missing tile of a sheet source selected, `RebindCandidates` has the sheet's sprite names that are bound to another tile. `RebindSpriteCommand` asks: "Give the sprite 'sea' to the tile #2? The tile #4, which has it now, goes away: cells of maps that use #4 will show as missing." Yes makes one step `Rebind sprite`: the binding 2 names `sea`, the binding 4 and its definition are removed.
- **A selection of tiles that are gone is cleared** (Review Focus 5): after an undo, a redo, a removed source or a new grid, the tiles of the selection that the layout no longer has leave it, and the grid follows.

- [ ] **Step 1: The spike, as a test.** `The_property_grid_edits_the_data_of_a_tile`: select 1:1,0; in `EditorProperties`, set `Kind` to `"door"`, add the property `locked` with a `TileProperty` of the bool kind, set `Collision` to `Solid`, each through the node presenters as Skein's `tests/ColorRise.Skein.Editor.Tests/Spikes/NodeHeadersGridTests.cs` does. Expected at the end: the asset has one definition for 1:1,0 with the three values, and three steps `Tile data`. If the grid cannot do one of them, stop and take the fallback above.
- [ ] **Step 2: Write the other failing tests**

```csharp
[Fact] public async Task A_click_selects_a_tile_and_control_adds_one()                    // Scene input: SelectedTiles == [1:1,0], then [1:1,0, 1:2,1]
[Fact] public async Task The_property_grid_shows_several_tiles_at_once()                  // two tiles, Kind set once: two definitions, one step
[Fact] public async Task Data_set_back_to_nothing_removes_the_definition()                // Kind "door" then "": Tiles is empty again
[Fact] public async Task Definitions_stay_sorted_by_tile()                                // data given to 1:2,1 then 1:0,0: Tiles is 1:0,0 then 1:2,1
[Fact] public async Task The_grid_follows_an_undo()                                       // Kind "door", Undo: the grid shows no Kind

[Fact] public async Task A_solid_drag_is_one_step_and_takes_the_value_of_its_first_tile()
    // Mode Solid; a press on 1:0,0 (not solid), a drag over 1:1,0 and 1:2,0 (which is solid already), a release:
    // the three are Solid, Steps == ["Solid"]; the same drag again clears the three, in one more step
[Fact] public async Task A_solid_drag_that_changes_nothing_makes_no_step()
[Fact] public async Task Solid_keeps_the_other_data_of_a_tile()                           // Kind "door" stays through a set and a clear
[Fact] public async Task Solid_tiles_are_hatched()                                        // Scene.Collect: a Hatch whose Bounds are the tile's

[Fact] public async Task A_missing_tile_is_rebound_and_the_other_tile_goes_away()
    // the renamed sprite of Task 8: select 1:#2; RebindCandidates has "sea"; the command with Yes: the binding 2 names "sea",
    // no binding 4, Steps ends with "Rebind sprite", and the slot of 1:#2 has a picture
[Fact] public async Task Rebind_answered_no_changes_nothing()
[Fact] public async Task A_tile_that_is_not_missing_has_no_rebind()

// Review Focus 5
[Fact] public async Task An_undo_that_removes_the_selected_tiles_clears_the_selection()
    // add a second image, select two of its tiles, Undo: SelectedTiles is empty, and the grid shows the asset, without throwing
[Fact] public async Task A_new_grid_keeps_only_the_tiles_it_still_has()                   // TileSize (32, 32) in the property grid: 1:3,0 leaves the selection
[Fact] public async Task An_undo_in_the_middle_of_a_solid_drag_ends_the_drag()            // press, drag, session Undo, release: no step, nothing throws
```

- [ ] **Step 3: Run them to see them fail.** Filter: `FullyQualifiedName~TileSetEditorTilesTests`. Expected: FAIL to compile.
- [ ] **Step 4: Write the mode, the tile data and the rebind.**
- [ ] **Step 5: Run the tests.** Expected: PASS (16 cases), with Task 8's.
- [ ] **Step 6: Commit and push**: `feat(tessera): the tile set editor: tile data, solid mode and rebinding a missing sprite`

### Task 10: The tile set editor's view

**Files:**
- Create: `src/ColorRise.Tessera.Editor.Avalonia/TesseraEditorAvaloniaPlugin.cs`, `Views/TileSetEditorView.axaml`, `Views/TileSetEditorView.axaml.cs`, `Views/FileDropBehavior.cs`
- Test: `tests/ColorRise.Tessera.Editor.Avalonia.Tests/TileSetEditorViewTests.cs`

**Interfaces:**
- Consumes: Tasks 6 to 9; `StrideAssetsAvaloniaPlugin`, `[AssetEditorView<T>]`, `IAssetEditorView` (model: Skein's `SkeinEditorAvaloniaPlugin.cs` and `Views/YarnProjectEditorView.axaml(.cs)`); `AddChildDropBehavior`, `AssetDragDropHelper.GetFiles`; `sd:EditCommands.Bindings` with `sd:EditCommandBinding Kind="Delete"`.
- Produces: `public sealed class TesseraEditorAvaloniaPlugin : StrideAssetsAvaloniaPlugin`; `[AssetEditorView<TileSetEditorViewModel>] public sealed partial class TileSetEditorView : UserControl, IAssetEditorView`; `internal sealed class FileDropBehavior` (an attached `Command` that gets the dropped files as `IReadOnlyList<UFile>`).

The layout is section 9's sketch, without the Animation mode:

- A tool bar: `+ Image`, `+ Sprite sheet`, the two modes as a pair of toggle buttons, the zoom as text (`400%`), `Refresh`.
- On the left, the list of sources (id, name, a warning sign with the problem as tool tip); Delete on it runs `RemoveSourceCommand` through an `EditCommandBinding`.
- In the middle, a `TileCanvas` over `Scene` and `View`, fitted when the source changes. The sprite sheets and image files dropped on it are added (the two behaviours).
- While `PendingImage` is set, a bar over the canvas: the name, the six numbers, "4 x 2 tiles" or the problem, `Add` and `Cancel`; the canvas shows the pending scene.
- With one missing tile selected, a bar under the canvas: "The sprite 'water' is gone." and a drop-down of `RebindCandidates` with a `Rebind` button.
- At the bottom, the lines of `Problems`, and `Message`.
- Every text of the view is plain English in the XAML; no string table in step 2.

- [ ] **Step 1: Write the failing tests** (the session has the Avalonia plugin too, and no stand-in editor)

```csharp
[AvaloniaFact] public async Task The_editor_opens_from_the_session_with_its_view()
    // the asset's editor is opened as the kit opens one (model: Skein's ProjectEditorViewTests): the view is a TileSetEditorView
    // whose DataContext is a TileSetEditorViewModel
[AvaloniaFact] public async Task The_view_draws_the_selected_source()        // in a window of 800 by 600: the pixel in the middle of the tile (2, 1) is TestTown.Flat(2, 1)
[AvaloniaFact] public async Task The_list_names_the_sources()                 // the texts "1" and "Town" are in the list
[AvaloniaFact] public async Task A_click_on_the_canvas_selects_a_tile()       // SelectedTiles == [1:2,1]
[AvaloniaFact] public async Task A_drag_in_solid_mode_hatches_the_tiles()     // the mode's button clicked, a drag over two tiles: both Solid, one step
[AvaloniaFact] public async Task A_proposed_image_shows_its_bar_and_add_makes_the_source()
    // ProposeImage: the bar is visible with the text "4 x 2 tiles"; TileWidth typed as 32: "2 x 1 tiles"; a click on Add: two sources, no bar
[AvaloniaFact] public async Task A_problem_shows_at_the_bottom()              // the image deleted and Refresh clicked: a text with "town.png"
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileSetEditorViewTests`. Expected: FAIL to compile.
- [ ] **Step 3: Write the plugin class and the view.** The view's code-behind has no logic of the editor: it gives the focus, fits the canvas, and passes dropped files on.
- [ ] **Step 4: Run the tests.** Expected: PASS (7 cases).
- [ ] **Step 5: Save a picture** of the view over the town set, with one solid tile and one selected (`CanvasProbe.Show` then `CaptureRenderedFrame().Save`), to `/mnt/project-files/tilemap/step2/tile-set-editor.png`, and look at it: texts readable, nothing clipped.
- [ ] **Step 6: Commit and push**: `feat(tessera): the view of the tile set editor`

### Task 11: The map document: cells in memory, strokes, and the asset

The map editor does not read the asset's text on every draw, and does not write a whole layer for one cell. This task makes the object between the two: the cells decoded once, a stroke that is seen before it is written, and a write that touches only the chunks that changed.

**Files:**
- Create: `src/ColorRise.Tessera.Editor/TileMapEditor/TileMapDocument.cs`, `TileCellsChangedEventArgs.cs`
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileMapDocumentTests.cs`

**Interfaces:**
- Consumes: `TileMapViewModel` (Task 3), `TestTown.Map` (Task 5); step 1's `TileLayer` (`GetCell`, `SetCell`, `Chunks`, `GetChunk`, `ChunkOf`), `TileChunkText.Encode` and `TryDecode`, `TileLayerAsset.SortChunks` (the order: `Position.Y` descending, then `Position.X`), `TileChunkAsset`.
- Produces, in `ColorRise.Tessera.Editor.TileMapEditor`:

```csharp
/// The cells of a map asset in memory, for one editor.
public sealed class TileMapDocument : IDisposable
{
    public TileMapDocument(TileMapViewModel map);
    public TileMapViewModel Map { get; }
    public int LayerCount { get; }
    public TileLayer GetLayer(int layer);                             // the decoded cells, with the cells of an open stroke; index as in the asset
    public IReadOnlyList<string> Errors { get; }                      // chunks that cannot be read or are given twice: layer name, chunk, reason
    public bool IsUnreadable(int layer, Int2 chunk);
    public Rectangle? UsedBounds(int layer);                          // in cells; null for a layer without a tile
    public int CountCells(ushort slot);                               // the cells of all layers that name this slot
    public bool CanWrite(int layer, Int2 cell, TileCell value);       // not in an unreadable chunk; a tile only inside the frame, when the map has one

    public bool IsStrokeOpen { get; }
    public void BeginStroke(int layer);
    public void SetStrokeCells(IReadOnlyDictionary<Int2, TileCell> cells);   // the stroke is these cells, and no other
    public void AddStrokeCells(IReadOnlyDictionary<Int2, TileCell> cells);   // the stroke is also these cells; the last value of a cell wins
    public bool CommitStroke(string name);                            // one step; false, and no step, when no cell has another value
    public void CancelStroke();
    public bool Write(int layer, IReadOnlyDictionary<Int2, TileCell> cells, string name);   // a stroke begun, set and committed

    public event EventHandler<TileCellsChangedEventArgs>? CellsChanged;      // Layer, Chunks (positions)
    public event EventHandler? StructureChanged;                      // layers added, removed, moved or changed; tile sets; the map's sizes
}
```

How it works:

- **Reading.** Each `TileChunkAsset` of each layer is decoded alone with `TileChunkText.TryDecode`. A chunk that cannot be read is in `Errors` and is "unreadable": the document never writes it and never removes it, and no cell of it can be painted. A second chunk at a position a layer already has is in `Errors` too, and is left alone as well: the first one is the layer's.
- **A stroke** changes the `TileLayer` at once (so the canvas shows it), and remembers the first value of each cell it touched. `CancelStroke` puts those back. Cells that `CanWrite` refuses are left out, silently: the tool says why with its message.
- **A commit** encodes each chunk the stroke touched, and compares the text with the item's. For each one that differs, in one transaction of the given name: the `Cells` of the item is updated through its node, a new `TileChunkAsset` is added at its place in the order of `SortChunks`, and the item of a chunk that became empty is removed. Nothing else of the asset is written.
- **Following the asset.** The document listens to the asset's graph. After a change that it did not make itself (an undo, a redo, an edit in the property grid, another editor), it compares the text of each chunk item with the text it last read or wrote (by reference first, then by value) and decodes again only those that differ. A change of the list of layers, of a layer's looks, of the tile sets or of the sizes raises `StructureChanged`.
- **An undo while a stroke is open** cancels the stroke first, then follows the asset: the stroke is lost, which is what the user asked for.

- [ ] **Step 1: Write the failing tests.** `Open(params string[][] layers)` opens a session with the town set and `TestTown.Map`, and gives the document. `ChunkLines(layer)` are the lines of the saved file inside the layer's chunks.

```csharp
[Fact] public async Task The_document_reads_the_cells_of_the_asset()
    // one layer, rows "0,0 1,0" over ". 2,1h": GetLayer(0).GetCell((0, 1)) == Cell(0, 0); GetCell((1, 0)) == Cell(2, 1, Horizontal);
    // UsedBounds(0) == (0, 0, 2, 2); CountCells(1) == 3; Errors is empty

[Fact] public async Task A_stroke_shows_at_once_and_is_one_step_that_writes_only_its_chunks()
    // a layer with a tile in the chunks (0, 0) and (3, 0); BeginStroke(0); AddStrokeCells({(1, 1): Cell(1, 0)}):
    // GetLayer(0).GetCell((1, 1)) is the cell and Steps is empty; CommitStroke("Stamp") is true: Steps == ["Stamp"],
    // the text of the chunk (0, 0) changed and the item of the chunk (3, 0) is the same object with the same string

[Fact] public async Task Undo_and_redo_bring_the_cells_back_and_forth()             // and CellsChanged names the chunk each time
[Fact] public async Task A_stroke_that_changes_nothing_makes_no_step()              // the cell is given the value it has: false, Steps empty
[Fact] public async Task A_cancelled_stroke_leaves_the_layer_as_it_was()
[Fact] public async Task Set_replaces_the_cells_of_the_stroke_and_add_keeps_them()
    // SetStrokeCells({a}), SetStrokeCells({b}): a has its first value again; AddStrokeCells({c}): b and c are set
[Fact] public async Task A_new_chunk_goes_to_its_place_in_the_file_and_an_empty_one_leaves()
    // chunks (0, 0) and (0, 2) exist; a cell at (20, 20) makes the chunk (1, 1): the items are (0, 2), (1, 1), (0, 0);
    // the only cell of (0, 2) erased: its item is gone, in the same step

// Review Focus 4
[Fact] public async Task A_stroke_on_the_negative_side_makes_negative_chunks()
    // cells (-1, -1), (-16, -17) and (0, 0): chunks (-1, -1), (-1, -2) and (0, 0); read back after a save and a reload of the session

// Review Focus 2
[Fact] public async Task A_cell_of_a_tile_set_the_map_does_not_have_is_kept_bit_for_bit()
    // the chunk's text has the token "7/9:3,3dv" (no slot 7 in the map); a stroke sets another cell of the chunk:
    // exactly one token of the saved chunk differs from before, and "7/9:3,3dv" is still there
[Fact] public async Task A_cell_of_a_tile_the_set_does_not_have_is_kept_too()       // "1/1:9,9" (the town has no column 9)

// Review Focus 3
[Fact] public async Task A_chunk_that_cannot_be_read_is_named_and_never_written()
    // the chunk (0, 0) of "Layer0" has a line with 15 tokens: Errors has one line with "Layer0", "0, 0" and the reader's reason;
    // IsUnreadable(0, (0, 0)); a stroke over cells of the chunks (0, 0) and (1, 0): only (1, 0) is written, the text of
    // (0, 0) is the same string; CanWrite(0, (3, 3), cell) is false
[Fact] public async Task A_conflict_marker_in_a_chunk_is_unreadable_and_the_other_chunks_load()     // a line "<<<<<<< ours" inside the block
[Fact] public async Task A_chunk_given_twice_keeps_the_first_and_touches_neither()
[Fact] public async Task A_chunk_made_readable_again_by_hand_is_read()               // its Cells fixed through the graph: no error, cells there

// Review Focus 5
[Fact] public async Task An_undo_while_a_stroke_is_open_drops_the_stroke()
    // one committed stroke, then BeginStroke and AddStrokeCells, then the session's Undo: IsStrokeOpen is false, the layer is
    // as before the first stroke, and CommitStroke("Stamp") is false without a step
[Fact] public async Task A_change_from_outside_is_followed()
    // another chunk's Cells set through the graph, a layer added through the graph: the cells are read, StructureChanged was raised
[Fact] public async Task A_frame_keeps_tiles_inside_and_lets_erase_work_outside()
    // Size (4, 4): a stroke over (3, 3) and (4, 4) writes (3, 3) only; a tile already at (9, 9) can be erased
[Fact] public async Task A_stroke_on_a_large_layer_writes_one_item()
    // a layer of 1000 chunks: a one-cell stroke raises one change of the asset's graph, and its commit takes under 50 ms
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileMapDocumentTests`. Expected: FAIL to compile.
- [ ] **Step 3: Write the document.**
- [ ] **Step 4: Run the tests.** Expected: PASS (18 cases).
- [ ] **Step 5: Commit and push**: `feat(tessera): the map document: decoded cells, strokes as one step, only the changed chunks written`

### Task 12: The map on a canvas (risk R4)

**Files:**
- Create: `src/ColorRise.Tessera.Editor/TileMapEditor/TileComposer.cs`, `TileMapScene.cs`
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileComposerTests.cs`, `TileMapSceneTests.cs`, `tests/ColorRise.Tessera.Editor.Avalonia.Tests/LargeMapTests.cs`

**Interfaces:**
- Consumes: Tasks 5, 6, 7 and 11.
- Produces:

```csharp
/// Draws tiles into pictures, on the CPU: nearest neighbour, alpha over what is there, clipped to the target.
public static class TileComposer
{
    public static Color MissingColor { get; }                         // magenta
    public static void Draw(TilePicture target, Rectangle destination, TilePicture source, Rectangle region, TileFlip flip, Color tint);
    public static void DrawMissing(TilePicture target, Rectangle destination);   // a box of MissingColor with a cross
}

/// A map as a canvas draws it: one picture per chunk and layer, kept until its cells change.
public sealed class TileMapScene : ICanvasScene, IDisposable
{
    public TileMapScene(TileMapDocument document, TilePictureService pictures);
    public Int2 CellPixels { get; }                                   // a cell in canvas units
    public RectangleF CellBounds(Int2 cell);                          // (x * w, -(y + 1) * h, w, h): the canvas has Y down, the map Y up
    public Int2 CellAt(Vector2 position);
    public int CurrentLayer { get; set; }
    public bool DimOthers { get; set; }
    public float DimOpacity { get; set; }                             // 0.35 at first
    public void SetGhost(TileStamp? stamp, Int2 origin);              // what the tool would paint, at 60% of opacity
    public IList<CanvasShape> Overlay { get; }                        // the tools' rectangles; call Invalidate() after a change
    public void Invalidate();
    public long PictureBudget { get; set; }                           // bytes; 64 MB at first
    public long PictureBytes { get; }
    public int ComposedCount { get; }                                 // pictures made since the start, for the tests
}
```

How it draws:

- **Units.** `CellPixels` is the map's `CellSize` times the largest `PixelsPerUnit` of its tile sets (16 for a map without one), rounded, at least 1. A tile is drawn at its pixel size times `CellPixels.X / (CellSize.X * PixelsPerUnit of its set)`, from its cell's bottom-left corner to the right and up, as the runtime places it. A layer's `Offset` moves its sprites, and the cursor's cell is found with the current layer's offset.
- **Pictures.** One per chunk and layer, of 16 by 16 cells, with the layer's `Tint` in its pixels. A tile larger than a cell is also drawn in the pictures of the chunks it reaches into, and a change of a cell makes those pictures again too. Cells are drawn in the order of the runtime inside a chunk (`TileRenderFeature`), and the same rule goes on across chunks. A cell whose slot has no tile set in the map, whose tile set is gone, or whose tile the set does not have, is drawn with `DrawMissing`.
- **Levels.** A picture is made at the scale 1, 1/2, 1/4 or 1/8: the smallest that is at least the zoom. At a zoom of 0.25 a chunk of 256 pixels is a picture of 64. Each level is made from the tiles, not from another level.
- **The cache** counts 4 bytes per pixel, and when it passes `PictureBudget` it drops the pictures that were drawn the longest ago and are not in the last frame.
- **A frame** has, bottom layer first, one sprite per visible chunk of each layer whose `Visible` is set, with the opacity of the layer (times `DimOpacity` for the layers that are not the current one, when `DimOthers`); the ghost as one sprite; then the shapes: a red `Hatch` on each unreadable chunk, an `Outline` of the map's frame, and the overlay.
- `Bounds` is the frame, or the used bounds of all layers, or one chunk at the origin for an empty map. `GridSize` is `CellPixels`.

- [ ] **Step 1: Write the failing tests**

```csharp
// TileComposerTests: `quarters` is the tile (0, 0) of the town's picture, pixels named TL, TR, BL, BR
[Theory]
[InlineData("", "RGBY")] [InlineData("h", "GRYB")] [InlineData("v", "BYRG")] [InlineData("hv", "YBGR")]
[InlineData("d", "RBGY")] [InlineData("dh", "BRYG")] [InlineData("dv", "GYRB")] [InlineData("dhv", "YGBR")]
public void A_flipped_tile_has_the_corners_the_runtime_gives_it(string flip, string corners)       // step 1's data, TileMapDrawTests
[Fact] public void A_stamp_turned_clockwise_draws_the_tile_turned_clockwise()
    // TileStamp.Single(Cell(0, 0)).RotateClockwise()[0, 0].Flip drawn: TL blue, TR red, BR green, BL yellow
[Fact] public void A_tile_is_scaled_with_hard_pixels_and_clipped()                 // 16 px into 32: blocks of 2 by 2; a destination half outside: no throw
[Fact] public void Transparent_pixels_keep_what_is_under_and_the_tint_multiplies()
[Fact] public void A_missing_tile_is_a_magenta_box_with_a_cross()

// TileMapSceneTests
[Fact] public async Task The_cell_zero_is_above_the_origin_of_the_canvas()         // CellBounds((0, 0)) == (0, -16, 16, 16); CellAt((5, -3)) == (0, 0); CellAt((-1, 1)) == (-1, -1)
[Fact] public async Task A_map_is_one_sprite_per_chunk_and_layer_with_the_pixels_of_its_tiles()
    // two layers in one chunk: Collect gives 2 sprites, bottom layer first; in the first one's picture, the pixel in the
    // middle of the cell (1, 0) is Flat(c, r) of the tile there
[Fact] public async Task Only_the_visible_chunks_are_collected()                    // a visible rectangle over one chunk of four: 1 sprite
[Fact] public async Task A_hidden_layer_is_not_drawn_and_the_others_can_be_dimmed() // Visible false: 1 sprite; DimOthers: the other layer's sprite has Opacity 0.35
[Fact] public async Task A_layer_s_tint_opacity_and_offset_show()
[Fact] public async Task A_change_of_a_cell_makes_one_picture_again()               // ComposedCount goes up by 1, and the pixel changed
[Fact] public async Task A_tile_larger_than_a_cell_reaches_into_the_next_chunk()    // a 16 by 32 tile on the top row of a chunk: the chunk above has its upper half
[Fact] public async Task Zoomed_out_pictures_are_smaller()                          // zoom 0.25: the sprite's Picture is 64 by 64, its Destination still 256 wide
[Fact] public async Task The_cache_keeps_under_its_budget()                         // budget 1 MB, 40 chunks collected one by one at zoom 1: PictureBytes <= 1 MB + one picture
[Fact] public async Task The_ghost_and_the_overlay_are_in_the_frame()               // SetGhost: one more sprite, Opacity 0.6, at CellBounds(origin); an overlay Outline is in Shapes

// Review Focus 2 and 3
[Fact] public async Task A_cell_whose_tile_set_or_tile_is_gone_is_a_marked_box()
    // "7/9:3,3" and "1/1:9,9" in the map: the middle of both cells has a pixel of MissingColor or of the cross, and nothing throws;
    // after the tile set asset of the slot 1 is deleted from the session: every cell of the slot 1 is a marked box
[Fact] public async Task An_unreadable_chunk_is_hatched()

// R4, in TileMapSceneTests
[Fact] public async Task A_large_map_is_composed_once()
    // 256 by 256 cells, four layers, every cell with a tile; at zoom 0.125, Collect over the whole map: 1024 sprites;
    // write the time of this first Collect to the test's output; a second Collect makes no picture and takes under 50 ms;
    // a one-cell stroke then a Collect makes one picture

// LargeMapTests (Avalonia)
[AvaloniaFact] public async Task A_large_map_draws_and_pans()
    // the same map in a TileCanvas of 1280 by 720 at zoom 0.125: a pixel of a known cell is right; 30 pans of 8 pixels, each
    // followed by a rendered frame: the mean time per frame goes to the output and is under 100 ms
```

- [ ] **Step 2: Run them to see them fail.** Filters: `FullyQualifiedName~TileComposerTests|FullyQualifiedName~TileMapSceneTests`, and `FullyQualifiedName~LargeMapTests`. Expected: FAIL to compile.
- [ ] **Step 3: Write the composer and the scene.** What the runtime does with a diagonal flip of a tile that is not square is what the composer does: read `TileQuads.Uvs` and the feature's corners, and pin it with one more case in `TileComposerTests`.
- [ ] **Step 4: Run the tests.** Expected: PASS. Write the three numbers of R4 (first collect, second collect, mean frame of the pan) in the "Done differently" note of this task; they go into the README's table in Task 18.
- [ ] **Step 5: If the pan is over 33 ms a frame**, the fallback of R4 is the next thing to do, before the tools: one picture per chunk for all the layers under the current one. If that does not hold either, stop and report.
- [ ] **Step 6: Commit and push**: `feat(tessera): a map on the canvas: a picture per chunk and layer, composed once`

### Task 13: The tools of the map editor

**Files:**
- Create: `src/ColorRise.Tessera.Editor/TileMapEditor/TilePainting.cs`, `TileToolKind.cs`, `TileClipboard.cs`, `TileMapEditorViewModel.cs`, `TileMapTools.cs`
- Modify: `tests/ColorRise.Tessera.Editor.Tests/TesseraTest.cs` (`EditMapAsync`)
- Test: `tests/ColorRise.Tessera.Editor.Tests/TilePaintingTests.cs`, `TileMapToolsTests.cs`

**Interfaces:**
- Consumes: Tasks 6, 7, 11 and 12.
- Produces:

```csharp
/// The geometry of the tools, without a map.
internal static class TilePainting
{
    public static IEnumerable<Int2> Line(Int2 from, Int2 to);                                  // every cell of the line, both ends included
    public static Int2 StampOrigin(TileStamp stamp, Int2 cursor);                              // the cursor is on the middle cell: cursor - (Size - 1) / 2
    public static void Stamp(TileStamp stamp, Int2 cursor, IDictionary<Int2, TileCell> cells); // the cells of the stamp that have a tile
    public static Dictionary<Int2, TileCell> Rectangle(TileStamp stamp, Int2 from, Int2 to);   // the stamp repeated from the top-left cell of the rectangle
    public static Dictionary<Int2, TileCell> Fill(Func<Int2, TileCell> read, Int2 start, Rectangle bounds, TileStamp stamp);
    public static TileStamp Capture(Func<Int2, TileCell> read, Int2 from, Int2 to);
    public static Rectangle FillBounds(Int2 mapSize, Rectangle? usedBounds, Int2 start);
}

public enum TileToolKind { Stamp, Rectangle, Fill, Erase, Pick, Select }

/// Cells copied in a session, with the tile set of each slot they name, so that another map can take them.
public sealed class TileClipboard { public static TileClipboard Of(ISessionViewModel session); }

[AssetEditorViewModel<TileMapViewModel>]
public sealed class TileMapEditorViewModel : AssetEditorViewModel<TileMapViewModel>
{
    public TileMapEditorViewModel(TileMapViewModel asset);
    public TileMapDocument Document { get; }
    public TileMapScene Scene { get; }
    public ICanvasInput Input { get; }
    public CanvasView View { get; }
    public TileToolKind Tool { get; set; }                            // Stamp at first
    public TileStamp? Stamp { get; set; }
    public int CurrentLayer { get; set; }                             // an index of the asset's layers; -1 with no layer; the top one at first
    public Rectangle? SelectedCells { get; }
    public Int2? HoverCell { get; }
    public string? Message { get; }                                   // why the last press did nothing
    public void FlipStampX();   public void FlipStampY();   public void RotateStamp(bool clockwise);
    public ICommandBase CopyCommand { get; }   public ICommandBase CutCommand { get; }
    public ICommandBase PasteCommand { get; }  public ICommandBase DeleteCommand { get; }
}

// TesseraSession
public Task<TileMapEditorViewModel> EditMapAsync(string location);
```

The tools (section 9), in the cells of the current layer:

- **Stamp** (B). The ghost follows the cursor. The left button paints the stamp, and a drag paints it at each cell of the line from the last cursor cell; the release is one step `Stamp`. Shift and a click paint along the line from the last painted cell. An empty cell of a stamp paints nothing. The right button, dragged, takes the cells under the rectangle as the stamp (a rectangle without a tile keeps the old stamp).
- **Rectangle** (P). A drag shows the stamp repeated in the rectangle, from its top-left cell; the release is one step `Rectangle`.
- **Fill** (F). A click fills the cells that touch the clicked one by a side and have the same cell (tile set, tile and flips), with the stamp repeated so that two fills next to each other line up (`stamp[x mod w, y mod h]`, with a modulo that is never negative). It stays inside `FillBounds`: the map's frame; without a frame, the used bounds of the layer when the click is inside them; else the chunk of the click. One step `Fill`.
- **Erase** (E). The left button erases along the drag, and a right drag erases a rectangle; one step `Erase`.
- **Pick** (I). A click on a cell with a tile makes it the stamp, shows it in the palette (Task 14), and goes back to the Stamp tool, as an eyedropper does **(C)**. A click on an empty cell does nothing.
- **Select** (R). A drag selects a rectangle of cells. A drag that starts inside the selection moves its tiles (step `Move`: the tiles leave their cells and replace what is where they land; the selection follows). `Copy` puts the selection in the session's `TileClipboard`; `Cut` copies and clears (step `Cut`); `Delete` clears (step `Delete`); `Paste` makes the copied cells the stamp and goes to the Stamp tool (decision 9). Pasting in a map that lacks one of the tile sets does nothing and says which: "This map does not have the tile set 'Town'. Add it, then paste again."
- **Keys**, through `KeyPressed`: B, P, F, E, I, R choose the tool; X and Y flip the stamp; Z turns it clockwise and Shift+Z the other way; Delete runs `DeleteCommand`; Escape cancels the open stroke, else clears the selection. Any other key gives false.
- **No stroke begins** without a layer, on a layer whose `Visible` is off, on a locked layer (Task 14), or without a stamp for the tools that need one; `Message` says which: "The layer 'Ground' is hidden.", "The layer 'Ground' is locked.", "The map has no layer. Add one.", "Pick a tile in the palette first."
- **The pointer leaving** the canvas hides the ghost. A release without a press, and a second button pressed during a stroke, do nothing.

- [ ] **Step 1: Write the failing tests.** `Press`, `Move`, `Release`, `Click`, `Drag` and `Key` call `editor.Input` with the middle of a cell (`Scene.CellBounds`). `Rows(layer, x0, y0, x1, y1)` gives the cells as the tokens of `TestTown.Map`.

```csharp
// TilePaintingTests
[Theory] [InlineData(0, 0, 3, 0, 4)] [InlineData(0, 0, 3, 3, 4)] [InlineData(2, 5, -1, 4, 4)] [InlineData(1, 1, 1, 1, 1)]
public void A_line_has_every_cell_between_its_ends(int x0, int y0, int x1, int y1, int count)      // first and last are the ends, each step moves by at most 1
[Fact] public void A_stamp_is_centred_on_the_cursor_and_skips_its_holes()            // 3 by 2 at (5, 5): the origin is (4, 5)
[Fact] public void A_rectangle_repeats_the_stamp_from_its_top_left_cell()            // a 2 by 2 stamp in 3 by 3, dragged from any corner
[Fact] public void A_fill_takes_the_cells_that_touch_by_a_side()                     // a diagonal neighbour is not taken
[Fact] public void A_fill_stops_at_its_bounds()                                      // an empty layer, bounds (0, 0, 16, 16): 256 cells, none outside
[Fact] public void A_fill_over_the_same_tile_as_the_stamp_changes_nothing()
[Theory] [InlineData(8, 8, 0, 0, 8, 8)] [InlineData(0, 0, 2, 2, 5, 5)] [InlineData(0, 0, -20, 3, 4, 4)]
public void The_bounds_of_a_fill_are_the_frame_then_the_used_cells_then_the_chunk(...)   // the last case, with used bounds away: the chunk (-2, 0)
[Fact] public void A_capture_keeps_empty_cells_as_holes_and_the_top_row_on_top()

// TileMapToolsTests
[Fact] public async Task A_click_stamps_in_one_step()                                // Steps == ["Stamp"], and the cell is the stamp's
[Fact] public async Task A_drag_stamps_along_the_way_in_one_step()                   // a fast move from (0, 0) to (5, 0): six cells, one step
[Fact] public async Task The_stroke_shows_before_the_release_and_undo_takes_all_of_it()
[Fact] public async Task Shift_click_draws_a_line_from_the_last_cell()
[Fact] public async Task A_stamp_of_several_tiles_paints_its_block_and_skips_its_holes()
[Fact] public async Task A_right_drag_captures_a_stamp()
[Fact] public async Task The_ghost_follows_the_cursor_and_leaves_with_it()           // Scene.Collect has the ghost at StampOrigin; after PointerLeft, none
[Fact] public async Task The_rectangle_tool_fills_the_dragged_rectangle()            // one step "Rectangle"; the preview during the drag shrinks with the drag
[Fact] public async Task The_fill_tool_fills_inside_the_frame()                      // Size (8, 8), an empty layer: 64 cells, one step "Fill"
[Fact] public async Task The_erase_tool_erases_along_a_drag_and_a_rectangle_with_the_right_button()
[Fact] public async Task The_pick_tool_takes_the_cell_as_the_stamp_and_goes_back_to_the_stamp_tool()
[Fact] public async Task X_Y_and_Z_flip_and_turn_the_stamp()                         // Key(X): Stamp equals the old one's FlipX(); Z then Shift+Z: the same stamp again
[Fact] public async Task The_tool_keys_choose_the_tool()                             // B, P, F, E, I, R; Key(Enter) gives false
[Fact] public async Task Select_then_move_is_one_step()                              // the tiles are at the new place, the old cells empty, Steps ends with "Move"
[Fact] public async Task Copy_and_paste_make_the_selection_the_stamp()               // Paste: Tool is Stamp, Stamp has the copied cells; a click paints them
[Fact] public async Task Cut_and_delete_clear_the_selection_in_one_step()
[Fact] public async Task Paste_in_another_map_uses_that_map_s_slot_for_the_tile_set()
    // a second map whose slot 3 is the town set: the pasted stamp's cells name the slot 3
[Fact] public async Task Paste_without_the_tile_set_says_which_one_is_missing()
[Fact] public async Task Escape_cancels_the_open_stroke_then_clears_the_selection()
[Fact] public async Task A_hidden_or_locked_layer_or_no_layer_takes_no_stroke_and_says_why()

// Review Focus 4
[Fact] public async Task The_tools_work_on_the_negative_side()                       // a stamp drag from (-3, -3) to (2, -3), a rectangle from (-20, -20) to (-17, -18)
[Fact] public async Task A_fill_with_nothing_to_stop_it_stays_in_its_bounds()
    // no frame, an empty layer, a click at (40, 40): the 256 cells of the chunk (2, 2); no frame and tiles in (0, 0) to (9, 9),
    // a click on the empty cell (5, 5) of a ring: only cells inside the used bounds change

// Review Focus 5
[Fact] public async Task An_undo_in_the_middle_of_a_drag_ends_the_stroke()
    // press, move, the session's Undo (of an earlier stroke), move, release: no new step, the layer is as the undo left it
[Fact] public async Task A_layer_removed_from_outside_during_a_drag_ends_the_stroke()   // through the graph: CurrentLayer is valid or -1, nothing throws
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TilePaintingTests|FullyQualifiedName~TileMapToolsTests`. Expected: FAIL to compile.
- [ ] **Step 3: Write the geometry, then the editor's view model with one small class per tool** in `TileMapTools.cs` (a press, a move, a release and a cancel each). The view model owns the document and the scene, and disposes both in `Destroy`.
- [ ] **Step 4: Run the tests.** Expected: PASS (39 cases with the theories).
- [ ] **Step 5: Commit and push**: `feat(tessera): the map editor's tools: stamp, rectangle, fill, erase, pick and select`

### Task 14: Layers, tile sets, settings, commands and the status line

**Files:**
- Create: `src/ColorRise.Tessera.Editor/TileMapEditor/TileLayerItemViewModel.cs`, `TileSetSlotItemViewModel.cs`, `TileMapGraph.cs`
- Create: `src/ColorRise.Tessera.Editor/TesseraEditorSettings.cs`, `TesseraCommands.cs`
- Modify: `TileMapEditorViewModel.cs`, `TesseraEditorPlugin.cs` (registers the commands), `TileSetEditorViewModel.cs` (nothing but what the commands call)
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileMapEditorTests.cs`, `TesseraCommandsTests.cs`

**Interfaces:**
- Consumes: Tasks 7, 9 and 13; the engine's `CommandDescriptor` and `context.Commands.Register` (models: Skein's `SkeinCommands.cs` and `SkeinCommandsTests.cs`), `[EditorSettings]` with `SettingsKey<T>` (model: `UIEditorSettings.cs` in `Stride.Assets.Editor`), `IAssetPropertyProviderViewModel`.
- Produces:

```csharp
// TileMapEditorViewModel
public IReadOnlyObservableList<TileLayerItemViewModel> Layers { get; }        // the top layer first, as the list shows them
public TileLayerItemViewModel? SelectedLayer { get; set; }                    // the same layer as CurrentLayer; shown in the property grid
public ICommandBase AddLayerCommand { get; }       public ICommandBase RemoveLayerCommand { get; }
public ICommandBase MoveLayerUpCommand { get; }    public ICommandBase MoveLayerDownCommand { get; }
public IReadOnlyObservableList<TileSetSlotItemViewModel> TileSets { get; }    // by slot
public TileSetSlotItemViewModel? SelectedTileSet { get; set; }
public ICommandBase AddTileSetCommand { get; }     public ICommandBase RemoveTileSetCommand { get; }
public bool AddTileSet(TileSetViewModel tileSet);
public bool ShowGrid { get; set; }                 public bool DimOtherLayers { get; set; }   // kept in the editor settings
public string StatusCell { get; }                  // "cell 12, 7", or "" off the canvas
public string StatusLayer { get; }                 // "Ground", "Ground (locked)", "no layer"
public string StatusTile { get; }                  // "Town 1:3,4" for the cell under the cursor, or ""
public string StatusZoom { get; }                  // "300%"
public IReadOnlyList<string> Problems { get; }     // TileAssetChecks.Of, Document.Errors, tile sets that are gone, by slot

public sealed class TileLayerItemViewModel : DispatcherViewModel, IAssetPropertyProviderViewModel
{
    public TileLayerAsset Layer { get; }
    public string Name { get; set; }               // a rename is one step "Rename layer"; an empty name is refused
    public bool IsEditing { get; set; }            // the name is being typed in the list (the view's inline edit)
    public bool IsVisible { get; set; }            // the asset's Visible, through the graph, one step "Layer visibility" (decision 10)
    public bool IsLocked { get; set; }             // the editor's own, in the settings
}

public sealed class TileSetSlotItemViewModel : DispatcherViewModel
{
    public ushort Slot { get; }
    public string Name { get; }                    // the asset's name, or "missing (slot 2)"
    public bool IsMissing { get; }
    public TilePaletteViewModel? Palette { get; }  // null when the tile set is gone
}

/// Every change the editor makes to a map asset besides its cells, through its graph. The caller owns the transaction.
internal static class TileMapGraph
{
    public static TileLayerAsset AddLayer(TileMapViewModel map, int index, string name);
    public static void RemoveLayer(TileMapViewModel map, int index);
    public static void MoveLayer(TileMapViewModel map, int from, int to);
    public static TileSetSlot AddTileSet(TileMapViewModel map, TileSetViewModel tileSet);   // the slot LastSlot + 1, and LastSlot with it
    public static void RemoveTileSet(TileMapViewModel map, TileSetSlot slot);
}

[EditorSettings]
internal static class TesseraEditorSettings
{
    public static SettingsKey<bool> ShowGrid;            // "Tessera/ShowGrid", true
    public static SettingsKey<Color> GridColor;          // "Tessera/GridColor", white with the alpha 64
    public static SettingsKey<bool> DimOtherLayers;      // "Tessera/DimOtherLayers", false
    public static SettingsKey<float> DimOpacity;         // "Tessera/DimOpacity", 0.35
    public static SettingsKey<float> LastZoom;           // "Tessera/LastZoom", 0: never set
    public static SettingsKey<List<string>> LockedLayers;   // "Tessera/LockedLayers": "<asset id>/<layer name>" (check that the container saves a list; else one string, an entry per line)
}

internal static class TesseraCommands { public static IReadOnlyList<CommandDescriptor> Create(); }
```

What the editor does:

- **Layers.** `Add` puts a layer over the current one, named `Layer 2` (the first number no layer has), and selects it; step `Add layer`. `Remove` removes the current one and selects the one under it; step `Remove layer`; a layer with tiles asks first: "Remove the layer 'Details' and its 214 tiles?". Up and down move it by one; step `Move layer`. Two layers may not have the same name: a rename to a name in use is refused with a `Message`. The property grid shows the selected layer through its node (`Name`, `Visible`, `Opacity`, `Tint`, `Offset`, `Collision`; `Chunks` is not shown), and the map when no layer is selected.
- **The lock** is kept by asset id and layer name, so a rename moves it (the editor rewrites the key) and a layer removed leaves nothing behind.
- **Tile sets.** `AddTileSetCommand` opens the asset picker for `TileSetAsset`; a tile set the map already has is refused with a `Message`; step `Add tile set`. `RemoveTileSetCommand` removes the selected slot; when `Document.CountCells(slot)` is above 0 it asks first: "Remove the tile set 'Town'? 1,204 cells use it. They stay in the map and show as missing." Step `Remove tile set`. The cells are not touched, and adding the tile set again does not bring them back (a slot is never given twice): the question says so in its last sentence, "This cannot be put right by adding the tile set again; use Undo."
- **The palette and the stamp.** `PickedChanged` of the selected slot's palette sets `Stamp` and the Stamp tool. The Pick tool and a captured stamp of one tile call `Show` on the palette of the tile's slot, and select that slot.
- **Settings.** `ShowGrid` and `DimOtherLayers` are read at opening and written when changed. `LastZoom` is written when the zoom changes; an editor opens at that zoom, centred on the map, and fitted the first time (while the setting is 0).
- **Commands**, all in the category `Tessera`, with `GestureScope` set to the editor's view model type, no `DefaultGesture`, and `CanExecute` true only when that editor has the focus (`context.Editor`). The ids never change:
  `Tessera.Map.Tool.Stamp`, `Tessera.Map.Tool.Rectangle`, `Tessera.Map.Tool.Fill`, `Tessera.Map.Tool.Erase`, `Tessera.Map.Tool.Pick`, `Tessera.Map.Tool.Select` (each with `IsChecked`), `Tessera.Map.FlipStampX`, `Tessera.Map.FlipStampY`, `Tessera.Map.RotateStampClockwise`, `Tessera.Map.RotateStampCounterClockwise`, `Tessera.Map.ToggleGrid`, `Tessera.Map.ToggleDimOtherLayers`, `Tessera.Map.AddLayer`, `Tessera.TileSet.Mode.Select`, `Tessera.TileSet.Mode.Solid`, `Tessera.TileSet.AddImage`, `Tessera.TileSet.AddSpriteSheet`. Titles are what the tool bar says ("Stamp tool", "Flip stamp horizontally"); the single key is in the keywords ("B") so that the palette finds it.

- [ ] **Step 1: Write the failing tests**

```csharp
// TileMapEditorTests
[Fact] public async Task The_layers_are_listed_top_first_and_the_top_one_is_current()
[Fact] public async Task Add_puts_a_layer_over_the_current_one_with_a_name_no_layer_has()   // "Layer 2"; Steps == ["Add layer"]; Undo removes it
[Fact] public async Task Remove_asks_when_the_layer_has_tiles()                              // the message has the name and the count; No: nothing; Yes: one step
[Fact] public async Task Up_and_down_move_the_layer_and_keep_it_current()                    // the asset's order changed, CurrentLayer follows, the scene draws in the new order
[Fact] public async Task A_rename_is_one_step_and_a_name_in_use_or_empty_is_refused()
[Fact] public async Task The_eye_is_the_asset_s_visible_and_an_undo_step()
[Fact] public async Task The_lock_is_not_in_the_asset_and_comes_back_when_the_editor_opens_again()   // the asset is not dirty; a second editor has IsLocked
[Fact] public async Task A_locked_layer_takes_no_stroke()
[Fact] public async Task The_property_grid_shows_the_selected_layer_then_the_map()          // Opacity is there and Chunks is not; then CellSize
[Fact] public async Task An_opacity_set_in_the_property_grid_shows_on_the_canvas()          // the sprite's Opacity is the new one
[Fact] public async Task A_tile_set_is_added_in_a_new_slot()                                 // the slot LastSlot + 1; Steps == ["Add tile set"]; a palette for it
[Fact] public async Task A_tile_set_the_map_has_is_refused()
[Fact] public async Task Removing_a_tile_set_in_use_asks_with_the_count_and_keeps_the_cells()
    // the message has "Town" and the count; Yes: the slot is gone, CountCells(1) is unchanged, the cells draw as missing
[Fact] public async Task A_slot_removed_and_the_set_added_again_gets_a_new_slot()            // LastSlot + 1, never the old one
[Fact] public async Task A_pick_in_the_palette_makes_the_stamp_and_the_stamp_tool()
[Fact] public async Task The_pick_tool_shows_the_tile_in_its_palette()                       // the palette of the slot is selected, its scene's Selection is the tile
[Fact] public async Task A_tile_set_deleted_from_the_session_shows_as_missing()              // TileSets[0].IsMissing, Palette null, a line in Problems
[Fact] public async Task The_status_line_tells_the_cell_the_layer_the_tile_and_the_zoom()
    // the pointer over the cell (12, 7) that has 1:3,0: "cell 12, 7", "Ground", "Town 1:3,0", "100%"; after PointerLeft: "" and ""
[Fact] public async Task The_grid_the_dimming_and_the_last_zoom_are_kept_in_the_settings()   // a second editor opens with them

// TesseraCommandsTests (model: SkeinCommandsTests)
[Fact] public void Every_command_has_its_id_a_scope_and_no_default_gesture()                // the 17 ids above, exactly
[Fact] public async Task A_tool_command_changes_the_tool_of_the_focused_map_editor_and_is_checked()
[Fact] public async Task A_map_command_cannot_run_without_a_map_editor()
[Fact] public async Task The_flip_commands_flip_the_stamp()
[Fact] public async Task The_tile_set_commands_reach_the_tile_set_editor()
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileMapEditorTests|FullyQualifiedName~TesseraCommandsTests`. Expected: FAIL to compile.
- [ ] **Step 3: Write the items, the graph helpers, the settings and the commands.**
- [ ] **Step 4: Run the tests.** Expected: PASS (24 cases).
- [ ] **Step 5: Commit and push**: `feat(tessera): the map editor's layers, tile sets, settings and commands`

### Task 15: The map editor's view (risk R6)

**Files:**
- Create: `src/ColorRise.Tessera.Editor.Avalonia/Views/TileMapEditorView.axaml`, `TileMapEditorView.axaml.cs`
- Test: `tests/ColorRise.Tessera.Editor.Avalonia.Tests/TileMapEditorViewTests.cs`

**Interfaces:**
- Consumes: Tasks 6, 7, 10, 13 and 14; `sd:EditCommands.Bindings` (Copy, Cut, Paste, Delete); `sd:InlineEditBehavior` (`Name`, `IsEditing`) in `Stride.Core.Presentation.Avalonia/Behaviors`, with an `EditCommandBinding` of the kind `Rename` and a double click to start the edit.
- Produces: `[AssetEditorView<TileMapEditorViewModel>] public sealed partial class TileMapEditorView : UserControl, IAssetEditorView`.

The layout is section 9's sketch:

- A tool bar: the six tools as toggle buttons (the tool tip has the key: "Stamp (B)"), `Flip X`, `Flip Y`, `Rotate`, then `Grid` and `Dim other layers` as toggles.
- The map's `TileCanvas` over `Scene`, `Input` and `View`, at the last zoom or fitted the first time (Task 14), with `ShowGrid` and the grid colour of the settings.
- On the right, the layers (an eye, a lock, the name; the current one marked) with `+`, `-`, up and down under them; then the tile sets as a drop-down with `+` and `-`, and the `TilePalette` of the selected one. A missing tile set shows its name in the drop-down and "This tile set is missing." in place of the palette.
- The status line: the four texts of Task 14, then `Message` when there is one.
- The lines of `Problems` in a bar over the canvas, each with a warning sign.
- **Keys (R6).** The canvas gets the focus on a press, so `TileCanvas` gives it the single keys while it has the focus; a `TextBox` that has the focus (a layer's name) keeps its keys, because the canvas does not have it then. Nothing in the view handles a key at the level of the window. Copy, Cut, Paste and Delete come through the `EditCommandBinding`s on the view, so the Edit menu and the user's shortcuts reach them.

- [ ] **Step 1: Write the failing tests** (a window of 1280 by 720; the session has both plugins)

```csharp
[AvaloniaFact] public async Task The_editor_opens_from_the_session_with_its_view()
[AvaloniaFact] public async Task The_view_draws_the_map()                     // the pixel in the middle of a known cell is its tile's colour
[AvaloniaFact] public async Task A_click_with_the_stamp_tool_paints_in_one_step()
    // a click in the palette on the tile (2, 1), then a click on the canvas at the cell (3, 3): the cell is 1:2,1, one step "Stamp",
    // and the pixel there is Flat(2, 1)
[AvaloniaFact] public async Task A_drag_in_the_palette_makes_a_stamp_of_several_tiles()   // Stamp.Size == (2, 1), and the ghost shows under the cursor
[AvaloniaFact] public async Task The_keys_reach_the_tools_when_the_canvas_has_the_focus()
    // a click on the canvas, then KeyPressQwerty(E): Tool is Erase; F: Fill; X: the stamp is flipped; Shift+Z turns it
[AvaloniaFact] public async Task The_keys_do_not_reach_the_tools_while_a_name_is_typed()
    // the rename of a layer started, "Beef" typed with KeyTextInput and the keys B, E, E, F pressed: Tool is still Stamp,
    // and after Enter the layer is named "Beef"
[AvaloniaFact] public async Task The_eye_hides_the_layer_and_the_lock_stops_the_stamp()   // the asset's Visible is false; with the lock, a click paints nothing and the status says why
[AvaloniaFact] public async Task The_tool_buttons_show_the_current_tool()     // a click on Fill: Tool is Fill and only that button is checked; the key B checks Stamp
[AvaloniaFact] public async Task The_status_line_follows_the_pointer()        // the texts "cell 3, 3" and "100%" are in the view
[AvaloniaFact] public async Task A_missing_tile_set_shows_in_place_of_the_palette()
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileMapEditorViewTests`. Expected: FAIL to compile.
- [ ] **Step 3: Write the view.**
- [ ] **Step 4: Run the tests.** Expected: PASS (10 cases). If a single key cannot be kept from the canvas while a name is typed, or cannot reach it at all, that is R6's fallback: the keys stay off, the commands and the tool bar remain, and the finding says what stopped them.
- [ ] **Step 5: Save a picture** of the view over a map of two layers with a stamp of 2 by 2 under the cursor, to `/mnt/project-files/tilemap/step2/tile-map-editor.png`, and look at it.
- [ ] **Step 6: Write the finding of R6** in `docs/plugin-findings.md`: a plugin's command cannot have a single key as its default shortcut (`CommandGestureBindings`), so the keys of the tools are read by the control and cannot be rebound.
- [ ] **Step 7: Commit and push**: `feat(tessera): the view of the map editor`

### Task 16: A click on a map in the scene view (risk R7)

The scene editor finds what is under a click by drawing every object with an id, in a render stage named `Picking`, which it sets up for meshes and sprites only (`EditorGameEntitySelectionService.UpdateGraphicsCompositor`). It keeps the id of every component of the scene (`PickingSceneRenderer`), so the id of the `TileMapComponent` is enough, and its filter takes a render object whose `Source` is a component of a selectable entity.

**Files:**
- Modify: `src/ColorRise.Tessera/Rendering/TileRenderFeature.cs`, `TileBatch.cs`
- Create: `src/ColorRise.Tessera.Editor/Scene/TilePickingService.cs`
- Modify: `tests/ColorRise.Tessera.Tests/Graphics/HarnessOptions.cs` and the harness (a picking stage drawn to a target of floats)
- Test: `tests/ColorRise.Tessera.Tests/Graphics/TilePickingTests.cs`, `tests/ColorRise.Tessera.Editor.Tests/TilePickingServiceTests.cs`

**Interfaces:**
- Consumes: `TileTransparentRenderStageSelector`, `TileRenderFeature`, `TileBatch.Begin` (step 1); the engine's `EditorGameServiceBase` (in `Stride.Editor`; `EditorGameRenderModeService` is a small model), `[EditorGameService(typeof(EntityHierarchyEditorController), Order = 210)]` (after the selection service, whose order is 200 and which makes the stage), `RuntimeIdHelper.ToRuntimeId`.
- Produces:

```csharp
// TileRenderFeature
public const string PickingStageName = "Picking";

// TileBatch: a Begin that takes the colour as it is, with no blending
public void BeginPicking(GraphicsContext context, in Matrix viewProjection, Color4 id);

// ColorRise.Tessera.Editor.Scene
[EditorGameService(typeof(EntityHierarchyEditorController), Order = 210)]
public sealed class TilePickingService : EditorGameServiceBase
{
    public override void UpdateGraphicsCompositor(EditorServiceGame editorGame);
    internal static bool AddPickingSelector(GraphicsCompositor compositor);   // false when there is no picking stage or no TileRenderFeature
}

// HarnessOptions
public bool Picking { get; init; }             // the frame also has the picking stage, in a target of the format R32G32B32A32_Float
// Frame
public int PickedId(int x, int y);             // the red channel, as the scene editor reads it
```

How it works:

- In a stage whose name is `PickingStageName`, the feature draws each chunk of each visible layer with a white texture of 1 by 1, the opaque blend state, and the colour `new Color4(RuntimeIdHelper.ToRuntimeId(component), 0, 0, 1)` as it is (`BeginPicking` skips the conversion to linear and the premultiply). The quads are the same as in the normal stage, so a cell without a tile is not clickable, as an empty part of a sprite is not.
- The render object's `Source` is the `TileMapComponent` already (`RenderTileChunk`), which the scene editor's filter needs.
- `AddPickingSelector` finds the stage named `Picking` and the compositor's `TileRenderFeature`, and adds a `TileTransparentRenderStageSelector` for that stage when the feature has none.

- [ ] **Step 1: Write the failing tests**

```csharp
// TilePickingTests (the harness of step 1, with Picking = true)
[Fact] public void A_cell_with_a_tile_gives_the_id_of_its_map()        // PickedId in the middle of a cell == RuntimeIdHelper.ToRuntimeId(component)
[Fact] public void A_cell_without_a_tile_gives_nothing()               // 0
[Fact] public void Two_maps_give_their_own_ids_and_the_one_in_front_wins()
[Fact] public void A_hidden_layer_is_not_picked()
[Fact] public void The_normal_frame_is_the_same_with_and_without_the_picking_stage()   // the colours of step 1's frame, pixel for pixel

// TilePickingServiceTests
[Fact] public void The_selector_is_added_once_for_the_picking_stage()  // a compositor with a "Picking" stage and a TileRenderFeature: true, then no second selector
[Fact] public void Without_a_picking_stage_or_a_feature_nothing_is_added()
[Fact] public void The_service_is_found_as_the_editor_finds_its_game_services()
    // AssemblyRegistry.GetScanTypes(the Editor assembly, typeof(EditorGameServiceAttribute)) has TilePickingService
```

- [ ] **Step 2: Run them to see them fail**, **Step 3: write the three pieces**, **Step 4: run the tests** (filters `FullyQualifiedName~TilePickingTests` and `FullyQualifiedName~TilePickingServiceTests`; expected: PASS, 8 cases), and the packages check, since the runtime changed.
- [ ] **Step 5: The fallback.** The real proof is Task 17's click in Game Studio. If the service is not found for a plugin, runs before the stage exists, or the click still selects nothing, keep the runtime's picking pass only if its tests cost nothing to keep, remove the service, and write the finding: a map is selected in the entity tree, and the selection service names the features it picks by type.
- [ ] **Step 6: Commit and push**: `feat(tessera): a click on a tile map in the scene view selects its entity`

### Task 17: Game Studio by hand, with a check game

Nothing before this task opens Game Studio. This one does, and it is where R5 and R7 are proved or their fallbacks taken.

**Files:**
- Create: `tests/StudioCheck/StudioCheck.slnx`, `StudioCheck.Game/` (a library on Stride packages, with a `ProjectReference` to the runtime, the game settings, a scene, a compositor, `town.png` drawn by `draw-town.py`), `StudioCheck.Linux/` (the head), `README.md` (how to open it)
- Create: `docs/studio-check.md` (the list below with what was seen, and the names of the screenshots)

**Model:** Skein's `sample/Brinemoor/` on `origin/feature/colorrise-skein`: the solution that also holds the extension's projects, `Brinemoor.Game.csproj`, `Brinemoor.Linux.csproj` (`StrideCurrentPackagePath`, `StridePlatform`). Game Studio is built in Task 0 (`sources/editor/Stride.GameStudio.Avalonia.Desktop`) and run with `DOTNET_ROOT`, `DISPLAY=:99` under Xvfb, the Lavapipe ICD, and the path of the `.slnx` as its argument; windows are driven with `xdotool` and pictured with `import` or `xwd`. Screenshots go to `/mnt/project-files/tilemap/step2/`, numbered as the list.

- [ ] **Step 1: Make the check game** and build it from the command line. Expected: the build succeeds and runs the Tessera asset compilers (no map yet, so nothing to compile).
- [ ] **Step 2: Open it in Game Studio and go through the list.** Each line gets "seen", "not as planned: ..." or "could not be checked: ..." in `docs/studio-check.md`.
  1. The plugin loads: no error about Tessera in the log; the two packages are in the plugins of the project.
  2. *Add asset* has `Tile set` and `Tile map` in the group `Sprite`, with their icons; each makes an asset with the default name, and the map has the layer `Ground`.
  3. The tile set editor opens. `+ Image` with `town.png` shows the grid bar, and `Add` makes the source. A sprite sheet dragged from the asset view adds a sheet source. An image file dragged from a file manager cannot be done under Xvfb: say so.
  4. A tile selected shows its data in the property grid; `Kind` and a property can be set; Solid mode hatches; undo and redo work from the Edit menu.
  5. The map editor opens. The tile set is added, a tile picked, a stamp painted; B, P, F, E, I, R, X, Y, Z change the tool and the stamp while the canvas has the focus (R6), and typing a layer's name does not.
  6. A shortcut given to `Stamp tool` in the Keyboard shortcuts page (Ctrl+1) works in the map editor, and the command palette lists the Tessera commands.
  7. Save, then `git diff` of the map's file after one more cell and a save: only lines of one chunk changed. Copy the diff into `docs/studio-check.md`.
  8. The compositor editor: *Add package features* adds `TileRenderFeature` with its selector, and the transparent stage's sort mode can be set to `Back to front along an axis` (finding 8, decision 1).
  9. The scene editor: `Create entity > 2D > Tile map` makes an entity with the component; a map asset dropped in the scene makes one with the map set; the map is drawn. With the camera tilted and a sprite between two layers, the order is right with the axis sort mode and wrong without (two screenshots).
  10. **R5**: with the scene open, a stamp in the map editor then a save: the scene view shows the new tile without closing the scene. If it shows only after the scene is opened again, that is the fallback's ground: try a notification from the map's view model on save *(check what `SpriteSheetViewModel` or `SceneViewModel` does to make the scene reload an asset)*, and if nothing public does it, write the finding and the README says to reopen the scene.
  11. **R7**: a click on the map in the scene view selects its entity; a click on an empty cell selects what is behind. If not, Task 16's fallback.
  12. The game runs from the head project (`dotnet run`, a few frames under Xvfb) and draws the map; a screenshot.
  13. With the neutral editor only (the Avalonia package's plugin not loaded) nothing can be checked here: say that the WPF Game Studio remains unchecked, as the design says.
- [ ] **Step 3: Fix what the list found** in the task that owns it, each fix with a test that would have caught it where a test can, and each gap of the plugin model as a row of `docs/plugin-findings.md`.
- [ ] **Step 4: Commit and push**: `test(tessera): a check game for Game Studio, and what was seen in it`

### Task 18: The write-up and the gates

**Files:**
- Modify: `README.md`, `docs/plugin-findings.md`, `docs/design.md`, `docs/plan-step2.md`
- Create: `/mnt/project-files/tilemap/2026-11-24-tessera-step2-report.md` *(the date of the day it is written)*, and the brief of step 3 beside it

- [ ] **Step 1: The README.** "What works today" gains the two editors, the templates, the scene editor's pieces, the sort mode and the warnings; "Not yet" keeps flipbooks, the sample, the Animation mode, painting in the scene view, real thumbnails, the WPF Game Studio (not checked), and that a map gets into a scene by the entity factory or by dropping its asset, since the cross-platform Game Studio cannot add a component to an entity yet (Skein's finding 17: say whether it still holds). New parts: "Setting up the compositor" (the feature, by *Add package features* or by hand; the opaque stage a `ForwardRenderer` needs; the sort mode and what it costs), "Keys of the map editor" (the table of Task 13, and that they cannot be rebound yet but the commands can get shortcuts), and the three numbers of R4 with the map they were measured on.
- [ ] **Step 2: The findings.** Every finding of this step has its row, in the format of the file: at least the provider that cannot set a sort mode (Task 4), the closed list of fast reloads (decision 4), the single keys (Task 15), the picking stage set up by type (Task 16), and whatever Task 17 met. Skein's findings met again go to that table.
- [ ] **Step 3: The design.** A short dated note where this plan changed or settled it: section 9 (the grid bar, paste as a stamp, the eye is the asset's `Visible`, rebinding takes a tile's sprite), section 11 (R4 to R7: what was measured or which fallback), section 16 (what was open and is now decided). The decisions D and C of this plan are copied to the design's lists.
- [ ] **Step 4: The plan.** Every box of `docs/plan-step2.md` is ticked or has a reason, and each task has its "Done differently" note, even when it says "nothing".
- [ ] **Step 5: The gates.** The whole of the three test projects, once, and `tests/PackagesCheck/run.sh`. Expected: every test passes (step 1's 241 and this step's), and `TESSERA-PACKAGES-CHECK OK`. Then `git diff --stat feature/colorrise-tessera..HEAD -- . ':!extensions/ColorRise.Tessera'` from the repository's root. Expected: empty.
- [ ] **Step 6: The review.** One fresh reviewer of the whole branch, as for step 1, given: the design, this plan with its notes, `docs/studio-check.md`, the diff from `feature/colorrise-tessera`, and the five lines of Review Focus. Fix what it finds that is a defect; list the minors that wait, with a number each, in the report.
- [ ] **Step 7: The report**, for Nicolas: what was built, the numbers (tests, R4), the fallbacks taken, the findings, what waits, the screenshots by name; then a brief of step 3 (flipbooks: asset, component, editor, and the Animation mode of the tile set editor) in the shape of step 2's brief. Update the project memory (`tilemap-plugin`).
- [ ] **Step 8: Commit and push**: `docs(tessera): step 2 written up: README, findings, design notes`
