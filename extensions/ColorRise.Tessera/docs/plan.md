# ColorRise.Tessera, step 1: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The extension `ColorRise.Tessera` exists as Skein does, its five risks are tried, and a tile map built from a tile set asset and a tile map asset is drawn by a game without a window, from packages only.

**Architecture:** A third-party extension in `extensions/ColorRise.Tessera/` that reaches Stride through the packages of a local engine build. The runtime holds the tile data (`TileSet`, `TileMap`), a component and a render feature that draws chunks of 16 by 16 cells with its own batch in the transparent stage. The `Assets` companion holds the two assets, their text format and their compilers. The editors, the flipbooks and the sample are steps 2 to 4.

**Tech Stack:** .NET 10, C# (latest), Stride 4.5.4-dev packages, xunit 2.9.3, the editor test kit `Stride.Assets.Editor.Testing`, Lavapipe (software Vulkan) for graphics tests.

**Spec:** [design.md](design.md), approved by Nicolas on 2026-11-24. "The design" below means this file, and "section N" is a section of it.

## Scope

The design has four steps on four stacked branches (section 12). This plan is step 1 only, on `feature/colorrise-tessera`. Steps 2 to 4 get their own plans when step 1 is done, because the spikes of this step (R1, R2, R3, R10) can change what they build on.

Not in this step: the three editors and everything in Game Studio but loading the plugin (so the risks R4 to R7, the picking stage and the render feature provider of the editor are in step 2), flipbooks, the sample game, Kenney's art. Test images are made in code. A game adds the render feature to its compositor in code until step 2.

## Two decisions the design left open, and one change to it

1. **Change: the bits of a tile id.** The design gives a grid tile 12 bits of column and 12 of row. This plan gives it 12 bits of column and 11 of row (2048 rows), and uses the freed bit to say "numbered tile" (a tile of a sprite sheet). Without that bit, `1:17,0` and `1:#17` are the same number and a file could not say which is meant. An image 2048 tiles high is over 32000 pixels at 16 pixels a tile, more than a texture can be.
2. **The token of a cell** in a chunk's text (section 16): `.` for an empty cell, else `<slot>/<tile id><flips>`, with the flips as letters in the order they are applied, `d` then `h` then `v`. Examples: `1/1:3,4`, `1/1:3,4h`, `2/1:#17dv`. One space between tokens, 16 tokens a line, 16 lines, each ending with a line feed.
3. **Sprites of a sheet that have no number yet** are not tiles for the build, which warns and names how many. The tile set editor of step 2 gives the numbers. Tests give them with `TileSlicer.Bind`.

## Global Constraints

- Every file of this work is inside `extensions/ColorRise.Tessera/`. Nothing else in the repository changes, and nothing in the engine.
- Stride is reached through packages only, at `$(TesseraStrideVersion)` (`4.5.4-dev`). No `ProjectReference` to an engine project. The `src/` projects use no engine internal, by reflection or any other way. A test may, with a finding that says why.
- A gap in the plugin model is a row in `docs/plugin-findings.md`, in the format of Skein's file: where, what was tried, what happened, the workaround, what would close the gap in Stride.
- All projects: `net10.0`, `LangVersion` latest, `Nullable` enable, `ImplicitUsings` enable, version `0.1.0-dev`, MIT, authors `Color-Rise`.
- Code: explicit access modifiers, classes sealed by default, the `field` keyword in place of a new private backing field, no licence header in source files, plain English in comments and documentation, `<summary>` on public types and members.
- Commits: conventional titles with the scope `tessera` (`feat(tessera): ...`, `test(tessera): ...`, `docs(tessera): ...`, `build(tessera): ...`). Author `Nicolas Musset <musset.nicolas@gmail.com>` (run the two `git config` commands before the first commit). No `Co-Authored-By` line, no link to Claude, no model name anywhere.
- Branch `feature/colorrise-tessera`, from `origin/master`. Push it after each task: the container does not last.
- Nothing stays only in the conversation (Nicolas, 2026-11-24). In the commit of each task: the task's boxes ticked in `docs/plan.md`, the result of its gate, and a row in `docs/plugin-findings.md` for each gap met. After each task, one line in the project's memory (`tilemap-plugin`) says which task is done.
- Values from the design: a chunk is 16 by 16 cells. A tile id is 8 bits of source (1 to 255) and 24 bits of local number. A cell is 8 bytes. The atlas border is 2 pixels. `PixelsPerUnit` is 16 by default, `CellSize` is (1, 1), `LayerSpacing` is 0.1. Flips are applied diagonal first, then horizontal, then vertical. Cells count Y up, and a chunk's text has its top row first.
- A fallback of section 11 that loses a goal of section 6 stops the work: write what was found and ask Nicolas.
- Engine names in this plan come from the fork's `master` at `659bb239e`. A name marked *(check)* was not read today: read the model file named beside it before relying on it.

## Review Focus

Inputs the design implies and that will reach this code, most likely first. Each has its test in the task named.

1. **A tile set image that is missing, renamed or not an image.** The build fails with an error that names the file and the source. No exception escapes the command. (Task 13)
2. **Grid numbers that make no tile:** a tile size of zero or larger than the image, a margin or spacing larger than the image, an image that the tile size does not divide. Zero tiles or whole tiles only, a warning for zero, no division by zero and no endless loop. (Task 12)
3. **A chunk's text edited by hand or left by a merge:** a line too short or too long, an unknown token, conflict markers. Loading says the layer, the chunk, the line and the token. The other chunks still load. (Tasks 7 and 11)
4. **A cell whose tile set or tile is gone** after a tile set changed. The cell is kept in the data, draws nothing, is not solid, and nothing throws in a query or in a frame. (Tasks 8, 9 and 11)
5. **Cells far from the origin and on the negative side:** chunk positions round down for negative cells, and `int.MinValue` and `int.MaxValue` do not overflow. (Task 6)

---

### Task 0: The .NET SDK and Stride's packages (R9)

**Files:** none in the repository.

**Interfaces:**
- Produces: `dotnet` 10 on the `PATH`, the worktree `/home/user/stride-master` on the branch `feature/colorrise-tessera`, and Stride's packages at `4.5.4-dev` in `/home/user/stride-master/bin/packages`.

- [x] **Step 1: Install the .NET 10 SDK** in `~/.dotnet` with Microsoft's `dotnet-install.sh` (`--channel 10.0`), and export `PATH=$HOME/.dotnet:$PATH` and `DOTNET_ROOT=$HOME/.dotnet`.

Run: `dotnet --version`
Expected: `10.0.1xx`, at least the version in the repository's `global.json`.

- [x] **Step 2: Make the branch** in the worktree, and set the author.

```bash
cd /home/user/stride-master
git switch -c feature/colorrise-tessera origin/master
git config user.name "Nicolas Musset" && git config user.email "musset.nicolas@gmail.com"
git lfs pull
```

- [x] **Step 3: Build the engine, the editor and the asset compiler**, as Skein's README says. This is a long run: start it in the background and write its output to a log file in the scratch folder. Check the free disk space first (`df -h /home/user`).

```bash
dotnet build sources/Stride.Editor.Avalonia.slnf
dotnet build sources/assets/Stride.AssetCompiler/Stride.AssetCompiler.csproj
```

If the build asks for a platform, add `-p:StridePlatforms=Linux -p:StrideGraphicsApis=Vulkan` (the project's memory, note on Orb Hunt).

Expected: both builds succeed, and `ls bin/packages` lists at `4.5.4-dev`: `Stride.Engine`, `Stride.Assets`, `Stride.Core.Assets`, `Stride.AssetCompiler`, `Stride.Assets.Editor`, `Stride.Assets.Editor.Avalonia`, `Stride.Assets.Editor.Testing`. If the test kit's package is missing, build its project by name and write it in the findings.

- [x] **Step 4: Gate.** If the engine cannot be built in the container, stop and tell Nicolas what failed, with the end of the log.

### Task 1: Scaffold the extension

**Files** (all created, under `extensions/ColorRise.Tessera/`):
- `.gitignore`, `global.json`, `nuget.config`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Build.local.props.example`, `ColorRise.Tessera.slnx`, `LICENSE`, `README.md`, `THIRD-PARTY-NOTICES.md`
- `docs/design.md` (a copy of the spec), `docs/plan.md` (a copy of this plan), `docs/plugin-findings.md`
- `src/ColorRise.Tessera/ColorRise.Tessera.csproj`, `ColorRise.Tessera.sdpkg`
- `src/ColorRise.Tessera.Assets/ColorRise.Tessera.Assets.csproj`, `ColorRise.Tessera.Assets.sdpkg`
- `src/ColorRise.Tessera.Editor/ColorRise.Tessera.Editor.csproj`, `ColorRise.Tessera.Editor.sdpkg`, `TesseraEditorPlugin.cs`
- `src/ColorRise.Tessera.Editor.Avalonia/ColorRise.Tessera.Editor.Avalonia.csproj`, `ColorRise.Tessera.Editor.Avalonia.sdpkg`
- `tests/ColorRise.Tessera.Tests/ColorRise.Tessera.Tests.csproj`
- `tests/ColorRise.Tessera.Editor.Tests/ColorRise.Tessera.Editor.Tests.csproj`, `AssemblyInfo.cs`, `TesseraTest.cs`, `PluginTests.cs`
- `tests/ColorRise.Tessera.Editor.Avalonia.Tests/ColorRise.Tessera.Editor.Avalonia.Tests.csproj`

**Interfaces:**
- Consumes: Task 0.
- Produces: the seven projects in one solution; MSBuild properties `TesseraStrideVersion` (`4.5.4-dev`) and `TesseraAvaloniaVersion` (`12.1.1`); `public sealed class TesseraEditorPlugin : StrideAssetsPlugin` in `ColorRise.Tessera.Editor`; `internal static class TesseraTest` in the editor tests, which opens a kit session with the plugin (the shape of Skein's `SkeinTest`); `InternalsVisibleTo` from each `src/` project to the test project that tests it.

- [x] **Step 1: Copy Skein's build files and project files** and rename. The source of each file is `git show origin/feature/colorrise-skein:extensions/ColorRise.Skein/<path>`.
  - Replace `Skein` by `Tessera` in names and properties. Remove what is Yarn's, AvaloniaEdit's, and the `UI` and `Audio` projects.
  - `Directory.Build.props`, `Directory.Build.targets`, `nuget.config`, `global.json`, `.gitignore`: otherwise as they are, comments included where they still hold.
  - The runtime project references `Stride.Engine` and declares the `Assets` and `Editor` projects with `StrideCompanionProject`. `PackageTags`: `stride-plugin;tilemap;tileset;2d`.
  - The `Assets` project also references the package `Stride.Assets` (sprite sheets and the texture packer).
  - `ColorRise.Tessera.Tests` references the runtime and the `Assets` projects. The two editor test projects are as Skein's, without the audio package.
  - One `.sdpkg` per `src/` project, as Skein's (`Version: 0.1.0-dev`). The `Assets` one has no template folder yet.
  - `README.md`: what Tessera is, the package table, "Try it" (Skein's three steps with Tessera's paths), "What works today" (empty for now), "Tied to the fork", licence. `LICENSE`: MIT, Color-Rise. `THIRD-PARTY-NOTICES.md`: says there is none yet.
  - `docs/plugin-findings.md`: Skein's two tables with their headers and no row, "Tied to the fork" with T1 to T3 reworded for Tessera, and a third table "Skein's findings, met again" with the columns: Skein's number, where Tessera met it, holds or not.

- [x] **Step 2: Write the failing test** in `tests/ColorRise.Tessera.Editor.Tests/PluginTests.cs`. `AssemblyInfo.cs` has `[assembly: CollectionBehavior(DisableTestParallelization = true)]` (Skein's finding 5).

```csharp
[Fact]
public async Task A_session_loads_the_plugin_without_an_error()
{
    await using var session = await TesseraTest.OpenAsync();
    Assert.Contains(session.Plugins, p => p is TesseraEditorPlugin);
    Assert.Empty(session.Errors);
}
```

`TesseraTest.OpenAsync` and the two members the test reads are written after `tests/ColorRise.Skein.Editor.Tests/SkeinTest.cs` on Skein's branch: use the kit's own names for "the plugins of the session" and "what the session logged as an error".

- [x] **Step 3: Run it to see it fail**

Run: `dotnet test extensions/ColorRise.Tessera/tests/ColorRise.Tessera.Editor.Tests`
Expected: FAIL to compile, `TesseraEditorPlugin` does not exist.

- [x] **Step 4: Write `TesseraEditorPlugin`**, empty, as the template's `samples/Plugin/Plugin/MyTemplate.Editor/MyTemplateEditorPlugin.cs` without its resource helper.

- [x] **Step 5: Build, test and pack**

Run: `dotnet build extensions/ColorRise.Tessera/ColorRise.Tessera.slnx && dotnet test extensions/ColorRise.Tessera/ColorRise.Tessera.slnx && dotnet pack extensions/ColorRise.Tessera/ColorRise.Tessera.slnx`
Expected: the test passes, and `extensions/ColorRise.Tessera/bin/packages` holds four packages at `0.1.0-dev`: `ColorRise.Tessera`, `.Assets`, `.Editor`, `.Editor.Avalonia`. A warning that Skein already wrote about goes in the third table of the findings.

- [x] **Step 6: Commit and push**

```bash
git add extensions/ColorRise.Tessera
git commit -m "feat(tessera): scaffold the ColorRise.Tessera extension"
git push -u origin feature/colorrise-tessera
```

### Task 2: A game without a window for the graphics tests (R10)

**Files:**
- Create: `tests/ColorRise.Tessera.Tests/Graphics/GraphicsHarness.cs`, `Frame.cs`, `GraphicsCollection.cs`, `TestTextures.cs`
- Modify: `tests/ColorRise.Tessera.Tests/ColorRise.Tessera.Tests.csproj` (the package `Stride.Dependencies.Lavapipe`, at the version in `sources/Directory.Packages.props`)
- Test: `tests/ColorRise.Tessera.Tests/Graphics/HarnessTests.cs`

**Interfaces:**
- Produces, all `internal` in `ColorRise.Tessera.Tests.Graphics`:

```csharp
internal sealed class HarnessOptions
{
    public int Width { get; init; } = 64;
    public int Height { get; init; } = 64;
    public Color Background { get; init; } = new(255, 0, 255, 255);   // magenta
    public ColorSpace ColorSpace { get; init; } = ColorSpace.Gamma;   // flat colours come back exact
    public int Msaa { get; init; } = 1;
    // Draws in place of the scene, after the clear. For tests of a batch alone.
    public Action<RenderDrawContext, Matrix>? CustomDraw { get; init; }   // the matrix is view times projection
}

internal sealed class HarnessScene
{
    public Game Game { get; }
    public Scene Scene { get; }
    public Entity Camera { get; }                  // orthographic, at z = 10, looking down -Z, near 0.1, far 100
    public void SetView(Vector2 center, float unitsHigh, Quaternion? cameraRotation = null);
    public Int2 ToPixel(Vector2 world);            // the pixel that shows a point of the plane z = 0, y down
    public Entity Add(string name, params EntityComponent[] components);
}

internal static class GraphicsHarness
{
    // Runs a game without a window: build once, then for each action of frames, runs it, draws a frame and keeps its pixels.
    public static Frame[] Run(HarnessOptions options, Action<HarnessScene> build, params Action<HarnessScene>[] frames);
}

internal sealed class Frame
{
    public int Width { get; }
    public int Height { get; }
    public Color this[int x, int y] { get; }       // y down
    public Color this[Int2 pixel] { get; }
    public int Count(Color color, Rectangle area);
    public void Save(string name);                 // a PNG in the folder `frames` beside the test assembly
}

internal static class TestTextures
{
    public static Texture Flat(GraphicsDevice device, Color color, int size = 4);
    // 8 by 8: top-left red (255,0,0), top-right green (0,255,0), bottom-left blue (0,0,255), bottom-right yellow (255,255,0)
    public static Texture Quarters(GraphicsDevice device);
}
```

Every test class that uses the harness is in the xunit collection `GraphicsCollection.Name` (`"Graphics"`), so that games run one after the other.

Model: `sources/engine/Stride.Graphics.Regression/GameTester.cs` and `Module.cs` (`game.Run(GameContextFactory.NewGameContext(AppContextType.Headless))`, and `Stride.Dependencies.Lavapipe.Lavapipe.TryConfigure()` when `VK_DRIVER_FILES` is not set). The compositor is made in code, with a forward renderer without post effects, and the game's own classes only: the engine's `Stride.Graphics.Regression` is not referenced.

- [x] **Step 1: Write the failing tests** in `HarnessTests.cs`

```csharp
private static readonly Color Magenta = new(255, 0, 255, 255);

[Fact]
public void A_frame_without_entities_is_the_background()
{
    var frame = GraphicsHarness.Run(new HarnessOptions(), _ => { }, _ => { })[0];
    Assert.Equal(64 * 64, frame.Count(Magenta, new Rectangle(0, 0, 64, 64)));
}

[Fact]
public void A_sprite_is_drawn_where_the_view_says()
{
    var origin = default(Int2);
    var frame = GraphicsHarness.Run(new HarnessOptions(), scene =>
    {
        scene.SetView(new Vector2(0, 0), unitsHigh: 4);
        origin = scene.ToPixel(Vector2.Zero);
        var white = TestTextures.Flat(scene.Game.GraphicsDevice, Color.White);
        // a sprite one unit wide and high, centred on the origin
        scene.Add("sprite", new SpriteComponent { SpriteProvider = new SpriteFromTexture { Texture = white, PixelsPerUnit = 4 } });
    }, _ => { })[0];
    Assert.Equal(new Int2(32, 32), origin);
    Assert.Equal(Color.White, frame[32, 32]);
    Assert.Equal(Magenta, frame[4, 4]);
}

[Fact]
public void Each_frame_action_gets_its_own_frame()   // two actions that move the sprite: the two frames differ at (32, 32)

[Fact]
public void An_exception_in_an_action_comes_out_of_Run()   // the build action throws InvalidOperationException: Run throws it, and a second Run still works
```

A test that only needs a graphics device (to make a texture) calls `Run` with a build action and no frame action.

- [x] **Step 2: Run them to see them fail**

Run: `dotnet test extensions/ColorRise.Tessera/tests/ColorRise.Tessera.Tests --filter "FullyQualifiedName~HarnessTests"`
Expected: FAIL to compile, `GraphicsHarness` does not exist.

- [x] **Step 3: Write the harness.** The game is a subclass of `Game` that runs the frame actions in `Update`, reads the back buffer into a `Frame` at the end of each `Draw`, and exits after the last one. An exception in an action or in a frame ends the run and is thrown again by `Run`.

- [x] **Step 4: Run the tests**

Run: the command of step 2.
Expected: PASS, 4 tests, and no window is opened (`DISPLAY` may be unset).

- [x] **Step 5: Gate.** If the Lavapipe package cannot be restored from nuget.org, or the game cannot run without a window from packages, use the software driver installed in the container (`VK_DRIVER_FILES` set to its `lvp_icd` file, under `Xvfb :99` if a display is needed), skip the graphics tests with a clear message where there is none, and write the finding.

- [x] **Step 6: Commit and push**

```bash
git add extensions/ColorRise.Tessera
git commit -m "test(tessera): a game without a window whose frames the tests read"
git push
```

### Task 3: Tile ids, flips and cells

**Files:**
- Create: `src/ColorRise.Tessera/TileId.cs`, `TileFlip.cs`, `TileCell.cs`
- Test: `tests/ColorRise.Tessera.Tests/TileIdTests.cs`, `TileCellTests.cs`

**Interfaces:**
- Produces, in the namespace `ColorRise.Tessera`:

```csharp
[DataContract]
public readonly struct TileId : IEquatable<TileId>, IComparable<TileId>
{
    // Bits 24 to 31: the source (1 to 255, 0 for "no tile"). Bit 23: set for a numbered tile.
    // Numbered: bits 0 to 22, the number. Grid: bits 0 to 11 the column, bits 12 to 22 the row.
    public uint Value { get; }
    public const int MaxSource = 255, MaxColumn = 4095, MaxRow = 2047, MaxNumber = 8388607;
    public static TileId FromGrid(int source, int column, int row);    // ArgumentOutOfRangeException out of range
    public static TileId FromNumber(int source, int number);           // the same
    public bool IsNone { get; }
    public int Source { get; }
    public bool IsNumbered { get; }
    public int Column { get; }    // 0 for a numbered tile
    public int Row { get; }       // 0 for a numbered tile
    public int Number { get; }    // 0 for a grid tile
    public static TileId Parse(string text);                            // FormatException
    public static bool TryParse(ReadOnlySpan<char> text, out TileId id);
    public override string ToString();    // "1:3,4" (source:column,row) or "2:#17", "" for none
}

[Flags]
public enum TileFlip : byte { None = 0, Horizontal = 1, Vertical = 2, Diagonal = 4 }

[DataContract]
[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly struct TileCell : IEquatable<TileCell>
{
    public TileCell(ushort slot, TileId tile, TileFlip flip = TileFlip.None);
    public ushort Slot { get; }       // the tile set's slot in the map, 0 for an empty cell
    public TileFlip Flip { get; }
    public TileId Tile { get; }
    public bool IsEmpty { get; }      // Slot == 0
    public static TileCell Empty { get; }
}
```

- [x] **Step 1: Write the failing tests**

```csharp
// TileIdTests
[Fact]
public void A_grid_id_keeps_its_source_column_and_row()
{
    var id = TileId.FromGrid(1, 3, 4);
    Assert.Equal((1, 3, 4, false), (id.Source, id.Column, id.Row, id.IsNumbered));
    Assert.Equal("1:3,4", id.ToString());
    Assert.Equal(id, TileId.Parse("1:3,4"));
}

[Fact]
public void A_numbered_id_keeps_its_source_and_number()
{
    var id = TileId.FromNumber(2, 17);
    Assert.Equal((2, 17, true), (id.Source, id.Number, id.IsNumbered));
    Assert.Equal("2:#17", id.ToString());
    Assert.Equal(id, TileId.Parse("2:#17"));
}

[Fact]
public void A_grid_id_and_a_numbered_id_differ()
    => Assert.NotEqual(TileId.FromGrid(1, 17, 0), TileId.FromNumber(1, 17));

[Fact]
public void The_largest_values_round_trip()
{
    Assert.Equal("255:4095,2047", TileId.FromGrid(255, 4095, 2047).ToString());
    Assert.Equal(TileId.FromNumber(255, 8388607), TileId.Parse("255:#8388607"));
}

[Theory]
[InlineData(0, 0, 0)] [InlineData(256, 0, 0)] [InlineData(1, 4096, 0)] [InlineData(1, 0, 2048)] [InlineData(1, -1, 0)] [InlineData(1, 0, -1)]
public void A_grid_id_out_of_range_throws(int source, int column, int row)
    => Assert.Throws<ArgumentOutOfRangeException>(() => TileId.FromGrid(source, column, row));

[Theory]
[InlineData("")] [InlineData("1")] [InlineData("1:")] [InlineData("1:3")] [InlineData("x:3,4")] [InlineData("1:#")]
[InlineData("1:3,4,5")] [InlineData("0:1,1")] [InlineData("1: 3,4")] [InlineData("1:#-2")] [InlineData("256:0,0")]
public void TryParse_refuses(string text) => Assert.False(TileId.TryParse(text, out _));

[Fact]
public void The_default_id_is_none()
{
    Assert.True(default(TileId).IsNone);
    Assert.Equal("", default(TileId).ToString());
}

// TileCellTests
[Fact] public void A_cell_is_eight_bytes() => Assert.Equal(8, Unsafe.SizeOf<TileCell>());
[Fact] public void The_default_cell_is_empty() => Assert.True(default(TileCell).IsEmpty);

[Fact]
public void A_cell_keeps_its_slot_tile_and_flips()
{
    var cell = new TileCell(3, TileId.FromGrid(1, 2, 5), TileFlip.Horizontal | TileFlip.Diagonal);
    Assert.Equal((3, TileId.FromGrid(1, 2, 5), TileFlip.Horizontal | TileFlip.Diagonal), (cell.Slot, cell.Tile, cell.Flip));
    Assert.False(cell.IsEmpty);
}
```

- [x] **Step 2: Run them to see them fail**

Run: `dotnet test extensions/ColorRise.Tessera/tests/ColorRise.Tessera.Tests --filter "FullyQualifiedName~TileIdTests|FullyQualifiedName~TileCellTests"`
Expected: FAIL to compile.

- [x] **Step 3: Write the three types** with the signatures above. Parsing and formatting use the invariant culture.

- [x] **Step 4: Run the tests.** Expected: PASS.

- [x] **Step 5: Commit and push**: `feat(tessera): tile ids, flips and the cell of a map`

### Task 4: The batch (R3)

**Files:**
- Create: `src/ColorRise.Tessera/Rendering/TileQuad.cs`, `TileBatch.cs`
- Test: `tests/ColorRise.Tessera.Tests/Graphics/TileBatchTests.cs`

**Interfaces:**
- Consumes: the harness of Task 2 (`HarnessOptions.CustomDraw`).
- Produces, in `ColorRise.Tessera.Rendering`:

```csharp
// A quad by its four corners, in world space, with the texture coordinates of each (0 to 1).
public struct TileQuad
{
    public Vector3 BottomLeft, BottomRight, TopRight, TopLeft;
    public Vector2 UvBottomLeft, UvBottomRight, UvTopRight, UvTopLeft;
}

public sealed class TileBatch : BatchBase<TileBatch.DrawInfo>
{
    public TileBatch(GraphicsDevice device, int bufferElementCount = 1024, int batchCapacity = 64);
    // One colour for every quad until End. Alpha blended, depth read and not written, no culling, by default.
    public void Begin(GraphicsContext context, in Matrix viewProjection, Color4 color, SamplerState? sampler = null,
        BlendStateDescription? blend = null, DepthStencilStateDescription? depthStencil = null);
    public void Draw(Texture texture, in TileQuad quad);
    public struct DrawInfo { /* the quad */ }
}
```

Model: `sources/engine/Stride.Graphics/Sprite3DBatch.cs` and `BatchBase.cs`. The two bytecodes given to `BatchBase` are both `SpriteEffect.Bytecode` (public). The colour is the effect's `Color` parameter, turned to linear when the device's colour space is linear. The vertex layout is the batch's own: position and texture coordinate. `SpriteBatch.Bytecode` is internal: do not reach it.

- [x] **Step 1: Write the failing tests**

```csharp
[Collection(GraphicsCollection.Name)]
public sealed class TileBatchTests
{
    private static readonly Color Magenta = new(255, 0, 255, 255);

    // Draws a grid of 8 by 8 white quads of one unit, corners made from whole numbers.
    private static Frame DrawGrid(Vector2 center, float unitsHigh, int size = 128);

    [Fact]
    public void A_quad_shows_its_texture_with_y_up()
    {
        // one quad over the square (0,0)-(2,2) with the quarters texture and the whole texture as uvs
        // (UvTopLeft = (0,0), UvBottomRight = (1,1)); view centred on (1,1), 2 units high, 64 by 64
        Assert.Equal(new Color(255, 0, 0, 255), frame[16, 16]);     // top-left
        Assert.Equal(new Color(0, 255, 0, 255), frame[48, 16]);     // top-right
        Assert.Equal(new Color(0, 0, 255, 255), frame[16, 48]);     // bottom-left
        Assert.Equal(new Color(255, 255, 0, 255), frame[48, 48]);   // bottom-right
    }

    [Fact]
    public void The_batch_colour_tints_every_quad()   // white texture, colour (1, 0, 0, 1): the centre pixel is (255, 0, 0, 255)

    [Fact]
    public void A_half_transparent_colour_blends_with_the_background()
    // white texture, colour (1, 1, 1, 0.5) not premultiplied, over black: each channel of the centre pixel is 127 or 128

    [Fact]
    public void Neighbouring_quads_leave_no_gap()
    {
        foreach (var unitsHigh in new[] { 8f, 9.37f, 11.113f, 23.5f })
            for (var i = 0; i < 32; i++)
            {
                var center = new Vector2(4 + i / 32f * 0.3f, 4 + i / 32f * 0.7f);
                var frame = DrawGrid(center, unitsHigh);
                // the part of the frame that the grid covers, one pixel in from its edges
                Assert.Equal(0, frame.Count(Magenta, InsideGrid(center, unitsHigh)));
            }
    }

    [Fact]
    public void A_rotated_grid_leaves_no_gap()   // the same, with the grid's corners turned by 17 degrees about Z
}
```

- [x] **Step 2: Run them to see them fail.** Run: `dotnet test extensions/ColorRise.Tessera/tests/ColorRise.Tessera.Tests --filter "FullyQualifiedName~TileBatchTests"`. Expected: FAIL to compile.

- [x] **Step 3: Write `TileQuad` and `TileBatch`.** A vertex position is the corner given, unchanged: the batch computes nothing from a centre and a size.

- [x] **Step 4: Run the tests.** Expected: PASS. Save the frames of the gap test at `unitsHigh` 9.37 (`Frame.Save`) and look at one.

- [x] **Step 5: Gate.** If `BatchBase<T>` cannot be derived from outside the engine with the public bytecode, write the finding, then try the first fallback of R3: a shader of the extension's own. If that needs a step from the game that uses Tessera, it still keeps every goal: go on, and write what a game must do. If only `Sprite3DBatch` works, the goal "no cracks at any camera position" is lost: stop and ask Nicolas.

- [x] **Step 6: Commit and push**: `feat(tessera): a batch of quads given by their corners`

### Task 5: Tile sets at runtime

**Files:**
- Create: `src/ColorRise.Tessera/TileCollision.cs`, `TileAnimationFrame.cs`, `Tile.cs`, `TileSet.cs`
- Test: `tests/ColorRise.Tessera.Tests/TileSetTests.cs`

**Interfaces:**
- Consumes: `TileId` (Task 3).
- Produces, in `ColorRise.Tessera`:

```csharp
[Flags] public enum TileCollision { None = 0, Solid = 1 }

[DataContract] public struct TileAnimationFrame { public TileId Tile; public float Duration; }   // seconds

[DataContract]
public sealed class Tile
{
    public TileId Id { get; set; }
    public Texture? Texture { get; set; }
    public Rectangle Region { get; set; }          // in the texture, in pixels, without the border
    public TileCollision Collision { get; set; }
    public string? Kind { get; set; }
    public Dictionary<string, object> Properties { get; } = [];   // bool, int, float, string or Color
    public List<TileAnimationFrame> Animation { get; } = [];
    public bool IsSolid { get; }                   // Collision has Solid
    public TileId FrameAt(double time);            // Task 10; returns Id until then
}

[DataContract]
public sealed class TileSet
{
    public float PixelsPerUnit { get; set; } = 16;
    public Int2 TileSize { get; set; } = new(16, 16);
    public Dictionary<TileId, Tile> Tiles { get; } = [];
    public Tile? GetTile(TileId id);
    // A tile set of one grid source (id 1) over a texture: a tile at each whole grid position, no margin, no spacing,
    // no look at the pixels. pixelsPerUnit 0 takes the tile's width.
    public static TileSet FromTexture(Texture texture, Int2 tileSize, float pixelsPerUnit = 0);
}
```

The content attributes of `TileSet` come in Task 11.

- [ ] **Step 1: Write the failing tests.** They need a texture, so they are in the collection `Graphics` and make it inside a harness run. The tests of the later tasks that need no texture make their tiles with `new Tile { Id = ... }` and no texture.

```csharp
[Fact]
public void FromTexture_makes_a_tile_per_grid_position()
{
    // a texture of 64 by 48 pixels, made on the device of a harness game
    var set = TileSet.FromTexture(texture, new Int2(16, 16));
    Assert.Equal(12, set.Tiles.Count);
    Assert.Equal(new Rectangle(48, 32, 16, 16), set.GetTile(TileId.FromGrid(1, 3, 2))!.Region);
    Assert.Equal(16, set.PixelsPerUnit);
    Assert.Null(set.GetTile(TileId.FromGrid(1, 4, 0)));
    Assert.Null(set.GetTile(default));
}

[Fact]
public void FromTexture_drops_the_part_of_the_texture_that_is_not_a_whole_tile()   // 40 by 20 pixels, tiles of 16: 2 tiles

[Theory]
[InlineData(0, 16)] [InlineData(16, 0)] [InlineData(-1, 16)]
public void FromTexture_refuses_a_tile_size_that_is_not_positive(int w, int h)
    => Assert.Throws<ArgumentOutOfRangeException>(() => TileSet.FromTexture(texture, new Int2(w, h)));
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileSetTests`. Expected: FAIL to compile.
- [ ] **Step 3: Write the four types.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit and push**: `feat(tessera): tile sets and tiles at runtime`

### Task 6: Chunks, layers and maps at runtime

**Files:**
- Create: `src/ColorRise.Tessera/TileChunk.cs`, `TileLayer.cs`, `TileLayerCollection.cs`, `TileMap.cs`, `TileChunkChangedEventArgs.cs`
- Test: `tests/ColorRise.Tessera.Tests/TileLayerTests.cs`, `TileMapTests.cs`

**Interfaces:**
- Consumes: `TileCell`, `TileId`, `TileFlip` (Task 3), `TileSet`, `Tile` (Task 5).
- Produces, in `ColorRise.Tessera`:

```csharp
[DataContract]
public sealed class TileChunk
{
    public const int Size = 16;
    public Int2 Position { get; }       // in chunks
    public TileCell[] Cells { get; }    // Size * Size; index = y * Size + x, x and y inside the chunk, y up
    public int Count { get; }           // the cells that are not empty
}

public sealed class TileChunkChangedEventArgs : EventArgs { public TileChunk Chunk { get; } public bool Removed { get; } }

[DataContract]
public sealed class TileLayer
{
    public string Name { get; set; } = "";
    public bool Visible { get; set; } = true;
    public float Opacity { get; set; } = 1;
    public Color Tint { get; set; } = Color.White;
    public Vector2 Offset { get; set; }            // world units, for the look only: queries ignore it
    public bool Collision { get; set; } = true;
    public TileMap? Map { get; }                   // set when the layer joins a map
    public IReadOnlyCollection<TileChunk> Chunks { get; }
    public TileChunk? GetChunk(Int2 chunkPosition);
    public TileCell GetCell(Int2 cell);            // Empty where there is no chunk
    public void SetCell(Int2 cell, TileCell value);   // an empty value clears; a chunk left without a tile is removed
    // InvalidOperationException when the layer is in no map; ArgumentException when the tile set is not one of the map's
    public void SetCell(Int2 cell, TileSet tileSet, TileId tile, TileFlip flip = TileFlip.None);
    public void ClearCell(Int2 cell);
    public event EventHandler<TileChunkChangedEventArgs>? Changed;   // only when a cell's value really changes
    public static Int2 ChunkOf(Int2 cell);         // each coordinate divided by 16, rounded down
}

public sealed class TileLayerCollection : IReadOnlyList<TileLayer>   // bottom first
{
    public TileLayer this[string name] { get; }    // KeyNotFoundException; the first layer of that name
    public bool TryGet(string name, out TileLayer layer);
    public int IndexOf(TileLayer layer);
    public TileLayer Add(string name);
    public void Insert(int index, TileLayer layer);
    public bool Remove(TileLayer layer);
}

[DataContract]
public sealed class TileMap
{
    public Vector2 CellSize { get; set; } = Vector2.One;   // ArgumentOutOfRangeException unless both are above 0
    public Int2 Size { get; set; }                         // in cells; (0, 0) for a map without a frame
    public IReadOnlyDictionary<ushort, TileSet> TileSets { get; }   // by slot, 1 to 65535
    public TileLayerCollection Layers { get; }
    public ushort AddTileSet(TileSet tileSet);             // its slot: the one it has, else the highest slot plus 1
    public void SetTileSet(ushort slot, TileSet tileSet);  // ArgumentOutOfRangeException for slot 0
    public ushort GetSlot(TileSet tileSet);                // 0 when the map does not have it
    public Tile? GetTile(TileCell cell);                   // null for an empty cell, a slot or a tile the map does not have
    public TileMap Clone();                                // its own cells and layers, the same tile sets
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
// TileLayerTests
[Theory]
[InlineData(0, 0, 0, 0)] [InlineData(15, 15, 0, 0)] [InlineData(16, 0, 1, 0)] [InlineData(-1, -1, -1, -1)]
[InlineData(-16, -17, -1, -2)] [InlineData(int.MinValue, int.MaxValue, -134217728, 134217727)]
public void ChunkOf_rounds_down(int x, int y, int cx, int cy)
    => Assert.Equal(new Int2(cx, cy), TileLayer.ChunkOf(new Int2(x, y)));

[Fact]
public void A_cell_set_is_the_cell_got()
{
    // a map with one tile set and one layer "Ground"
    foreach (var cell in new[] { new Int2(0, 0), new Int2(-1, -1), new Int2(100, -37), new Int2(int.MaxValue, int.MinValue) })
    {
        layer.SetCell(cell, set, TileId.FromGrid(1, 1, 0), TileFlip.Vertical);
        Assert.Equal(new TileCell(1, TileId.FromGrid(1, 1, 0), TileFlip.Vertical), layer.GetCell(cell));
    }
    Assert.Equal(4, layer.Chunks.Count);
}

[Fact] public void A_cell_never_set_is_empty()            // GetCell((5, 5)) on a new layer is Empty, and no chunk is made
[Fact] public void Clearing_the_last_cell_of_a_chunk_removes_the_chunk()   // Changed is raised with Removed true
[Fact] public void Changed_is_raised_once_per_real_change()   // set A: 1; set A again: still 1; set B: 2; clear: 3; clear again: 3
[Fact] public void The_index_of_a_cell_in_its_chunk_has_y_up()
    // SetCell((2, 15)) then GetChunk((0, 0))!.Cells[15 * 16 + 2] is not empty; SetCell((-1, -1)): chunk (-1, -1), index 15 * 16 + 15
[Fact] public void Setting_a_tile_of_a_tile_set_the_map_does_not_have_throws()   // ArgumentException
[Fact] public void Setting_a_tile_on_a_layer_without_a_map_throws()              // InvalidOperationException

// TileMapTests
[Fact] public void Layers_are_found_by_name_and_by_index()
[Fact] public void A_tile_set_added_twice_keeps_its_slot()     // AddTileSet(a) == 1, AddTileSet(b) == 2, AddTileSet(a) == 1
[Fact] public void GetTile_is_null_for_what_the_map_does_not_have()
    // an empty cell, a cell of slot 9, a cell of slot 1 whose tile id the set does not have: all null, nothing throws
[Fact] public void A_clone_has_its_own_cells_and_the_same_tile_sets()
    // change a cell of the clone: the original keeps its value; ReferenceEquals of the tile sets is true
[Theory] [InlineData(0, 1)] [InlineData(1, 0)] [InlineData(-1, 1)] [InlineData(float.NaN, 1)]
public void A_cell_size_that_is_not_positive_is_refused(float x, float y)   // ArgumentOutOfRangeException
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileLayerTests|FullyQualifiedName~TileMapTests`. Expected: FAIL to compile.
- [ ] **Step 3: Write the types.** A layer keeps its chunks in a dictionary by chunk position.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit and push**: `feat(tessera): chunks, layers and maps at runtime`

### Task 7: The text of a chunk, the tile map asset, and the asset graph (R1)

**Files:**
- Create: `src/ColorRise.Tessera.Assets/TileChunkText.cs`, `TileMapAsset.cs`, `TileSetSlot.cs`, `TileLayerAsset.cs`, `TileChunkAsset.cs`, `TileIdYamlSerializer.cs`
- Test: `tests/ColorRise.Tessera.Tests/Assets/TileChunkTextTests.cs`, `TileMapAssetYamlTests.cs`, `Assets/Golden/small.sdtilemap`
- Test: `tests/ColorRise.Tessera.Editor.Tests/TileMapAssetGraphTests.cs`

**Interfaces:**
- Consumes: `TileCell`, `TileId`, `TileFlip` (Task 3), `TileSet` (Task 5), `TileMap` (Task 6), `TesseraTest` (Task 1).
- Produces, in `ColorRise.Tessera.Assets`:

```csharp
public static class TileChunkText
{
    // 256 cells, index = y * 16 + x with y up. 16 lines, the top row (y = 15) first, each ending with '\n'.
    public static string Encode(ReadOnlySpan<TileCell> cells);
    // FormatException whose message has "line N" and "token N" (from 1), and the token
    public static void Decode(string text, Span<TileCell> cells);
    public static bool TryDecode(string text, Span<TileCell> cells, [NotNullWhen(false)] out string? error);
}

[DataContract("TileMapAsset")]
[AssetDescription(".sdtilemap")]
[AssetContentType(typeof(TileMap))]
[AssetFormatVersion("ColorRise.Tessera", "1.0.0.0")]
[Display(110, "Tile map")]
public sealed class TileMapAsset : Asset
{
    public Vector2 CellSize { get; set; } = Vector2.One;
    public Int2 Size { get; set; }
    public List<TileSetSlot> TileSets { get; } = [];
    public List<TileLayerAsset> Layers { get; } = [];    // bottom first
    public int LastSlot { get; set; }                    // hidden; a slot is never given twice
    public TileSetSlot AddTileSet(TileSet tileSet);      // slot = ++LastSlot
    public TileMap ToTileMap(Func<TileSetSlot, TileSet?> resolve, ICollection<string>? errors = null);   // decodes the chunks
    public static TileMapAsset FromTileMap(TileMap map);  // for tests and for code that makes maps
}

[DataContract] public sealed class TileSetSlot { public int Slot { get; set; } public TileSet? TileSet { get; set; } }

[DataContract]
public sealed class TileLayerAsset
{
    public string Name { get; set; } = "";
    public bool Visible { get; set; } = true;
    public float Opacity { get; set; } = 1;
    public Color Tint { get; set; } = Color.White;
    public Vector2 Offset { get; set; }
    public bool Collision { get; set; } = true;
    public List<TileChunkAsset> Chunks { get; } = [];    // not shown in the property grid
    public void SortChunks();                            // top to bottom (Position.Y from high to low), then left to right
}

[DataContract] public sealed class TileChunkAsset { public Int2 Position { get; set; } public string Cells { get; set; } = ""; }
```

A cell's token is in "Two decisions" at the top. `TileIdYamlSerializer` writes a `TileId` member as its text (`1:3,4`): model, a scalar serializer of Stride's own asset YAML *(check: find how `Stride.Assets` registers one for a type of its own, such as its serializer of `UFile` or of a colour)*. If a third party cannot register one, give the asset classes a `string` member with the text and a typed property the YAML ignores, and write the finding.

- [ ] **Step 1: Write the failing tests of the text**

```csharp
private const string EmptyLine = ". . . . . . . . . . . . . . . .";

[Fact]
public void An_empty_chunk_is_sixteen_lines_of_dots()
    => Assert.Equal(string.Concat(Enumerable.Repeat(EmptyLine + "\n", 16)), TileChunkText.Encode(new TileCell[256]));

[Fact]
public void The_first_line_is_the_top_row()
{
    var cells = new TileCell[256];
    cells[15 * 16 + 2] = new TileCell(1, TileId.FromGrid(1, 3, 4));
    cells[15 * 16 + 3] = new TileCell(1, TileId.FromGrid(1, 3, 4), TileFlip.Horizontal);
    cells[0] = new TileCell(2, TileId.FromNumber(1, 17), TileFlip.Diagonal | TileFlip.Vertical);
    var lines = TileChunkText.Encode(cells).Split('\n');
    Assert.Equal(". . 1/1:3,4 1/1:3,4h . . . . . . . . . . . .", lines[0]);
    Assert.Equal("2/1:#17dv . . . . . . . . . . . . . . .", lines[15]);
    Assert.Equal("", lines[16]);
}

[Fact] public void All_the_flips_are_written_d_then_h_then_v()   // the eight values: "", "h", "v", "hv", "d", "dh", "dv", "dhv"

[Fact]
public void Decode_reads_back_what_encode_wrote()
    // 256 random cells from new Random(12345): slot 0 to 65535, grid and numbered ids, the eight flips

[Theory]
[InlineData(2, "line 3", ". . . .")]                                  // a line of four tokens
[InlineData(0, "token 17", EmptyLine + " .")]                        // a line of seventeen tokens
[InlineData(5, "<<<<<<<", "<<<<<<< HEAD")]                           // a conflict marker
[InlineData(1, "1/1:3,4x", "1/1:3,4x . . . . . . . . . . . . . . .")]   // an unknown flip
[InlineData(1, "0/1:3,4", "0/1:3,4 . . . . . . . . . . . . . . .")]     // slot 0 with a tile
[InlineData(1, "1/", "1/ . . . . . . . . . . . . . . .")]               // a slot without a tile
public void A_bad_line_is_refused_and_named(int index, string expectedInMessage, string badLine)
    // the empty chunk's text with line `index` replaced: TryDecode is false, and the error has expectedInMessage

[Fact] public void Fifteen_or_seventeen_lines_are_refused()
[Fact] public void Windows_line_ends_are_read()   // "\r\n" in place of "\n" decodes the same
```

- [ ] **Step 2: Run, see them fail, write `TileChunkText`, run again.** Filter: `FullyQualifiedName~TileChunkTextTests`. Expected: FAIL to compile, then PASS.

- [ ] **Step 3: Write the failing tests of the file**

```csharp
// The map of the golden file: CellSize (1, 1), Size (0, 0), a slot 1 without a tile set, two layers "Ground" and "Details".
// Ground has cells in the chunks (0, 0) and (-1, 0); Details has one cell with the flip dh and an opacity of 0.5.
private static TileMapAsset SmallMap();

[Fact] public void A_map_saved_loaded_and_saved_again_has_the_same_bytes()
[Fact] public void A_small_map_is_the_golden_file()     // the YAML of SmallMap(), ids aside, equals Assets/Golden/small.sdtilemap
[Fact] public void The_cells_of_a_chunk_are_a_literal_block()   // the YAML has a line that ends with "Cells: |"
[Fact]
public void A_change_of_one_cell_changes_one_line()
    // save, change one cell of Ground, save: of the lines of the two texts, exactly one differs
[Fact] public void Chunks_are_written_top_to_bottom_then_left_to_right()
    // chunks added in the order (0,0), (1,1), (-1,1), (0,-1) are saved in the order (-1,1), (1,1), (0,0), (0,-1)
[Fact] public void A_map_goes_to_a_tile_map_and_back()   // FromTileMap(asset.ToTileMap(...)) saves to the same text
[Fact]
public void A_bad_chunk_is_reported_and_the_others_load()
    // one chunk of Ground has a bad line: errors has one entry with "Ground", "(-1, 0)", "line"; the other chunk's cells are in the map
```

Write the golden file by running the test once, read it as a person would in a diff, then commit it. If the saved file has an id per chunk that changes from run to run, the test compares after loading the golden file's ids.

- [ ] **Step 4: Write the asset classes.** Try `Chunks` first as a plain list without item ids *(check: the attribute that makes a collection's items non-identifiable in Stride's assets)*: the file is then a plain YAML sequence. Keep that only if the graph tests of step 5 pass with it. `ToTileMap` sorts nothing and `FromTileMap` calls `SortChunks`.

- [ ] **Step 5: Write the failing tests of the asset graph**, in the editor tests, on a kit session with a `TileMapAsset` of one layer and 1000 chunks

```csharp
[Fact]
public async Task The_text_of_a_chunk_is_replaced_through_its_node_and_undone()
    // inside one transaction named "Stamp", update the node of Chunks[500].Cells with a new text:
    // the asset has the new text and is dirty; undo gives the old text back; redo the new one; the history has one step "Stamp"
[Fact] public async Task A_chunk_is_added_and_removed_through_the_graph_and_undone()
[Fact] public async Task One_transaction_over_several_chunks_is_one_step()
[Fact] public async Task A_map_of_a_thousand_chunks_opens_in_under_five_seconds()   // session open and the asset's graph, with a Stopwatch
[Fact] public async Task The_chunks_are_not_in_the_property_grid()
    // the property grid's view model for the asset has no node named Chunks under a layer
```

Model for nodes and transactions: `docs/editor/custom-editor.md` ("Manual transaction wrapping"), `docs/quantum/asset-graph.md`, and Skein's editor tests.

- [ ] **Step 6: Run them, make them pass, and gate.** If the graph cannot hold the chunks this way (too slow, or no undo of an item), use R1's fallback: the chunks stay out of the graph and the undo steps are written by hand, with an operation that still marks the asset dirty. That keeps every goal: go on, and write the finding. The file format does not change.

- [ ] **Step 7: Commit and push**: `feat(tessera): the tile map asset and the text of its chunks`

### Task 8: The tile map component

**Files:**
- Create: `src/ColorRise.Tessera/TileMapComponent.cs`, `TileAnchor.cs`, `TileSampler.cs`
- Test: `tests/ColorRise.Tessera.Tests/TileMapComponentTests.cs`

**Interfaces:**
- Consumes: `TileMap`, `TileLayer` (Task 6), `Tile` (Task 5).
- Produces, in `ColorRise.Tessera`:

```csharp
public enum TileAnchor { BottomLeft, Center }
public enum TileSampler { Point, Linear }

[DataContract("TileMapComponent")]
[Display("Tile map", Expand = ExpandRule.Once)]
[ComponentCategory("2D")]
public sealed class TileMapComponent : ActivableEntityComponent
{
    public TileMap? Map { get; set; }
    public Color4 Color { get; set; } = Color4.White;
    public float LayerSpacing { get; set; } = 0.1f;
    public TileSampler Sampler { get; set; }
    public float AnimationSpeed { get; set; } = 1;
    [DataMemberIgnore] public double AnimationTime { get; set; }     // seconds; the render processor adds to it (Task 10)
    public RenderGroup RenderGroup { get; set; }
    public Int2 WorldToCell(Vector3 world);          // through the entity's world matrix; (0, 0) without a map
    public Vector3 CellToWorld(Int2 cell, TileAnchor anchor = TileAnchor.BottomLeft);
    public float GetLayerZ(string layerName);        // the layer's index times LayerSpacing, in the entity's space
    public bool IsSolid(Int2 cell);                  // a solid tile on a layer whose Collision is true
    public bool OverlapsSolid(RectangleF worldRectangle);   // X, Y the bottom-left corner, in the map's plane; touching an edge is not overlapping
}
```

Layer offsets are for the look only: the queries use the grid.

- [ ] **Step 1: Write the failing tests** (an entity made in the test, `entity.Transform.UpdateWorldMatrix()` called by the test)

```csharp
[Fact]
public void World_and_cells_convert_with_y_up()
{
    // entity at (10, 20, 0), CellSize (1, 1)
    Assert.Equal(new Int2(2, 3), map.WorldToCell(new Vector3(12.5f, 23.5f, 0)));
    Assert.Equal(new Int2(-1, -1), map.WorldToCell(new Vector3(9.99f, 19.99f, 0)));
    Assert.Equal(new Vector3(12, 23, 0), map.CellToWorld(new Int2(2, 3)));
    Assert.Equal(new Vector3(12.5f, 23.5f, 0), map.CellToWorld(new Int2(2, 3), TileAnchor.Center));
}

[Fact] public void A_cell_size_scales_the_grid()   // CellSize (2, 0.5): WorldToCell((5, 1.2, 0)) is (2, 2)
[Fact]
public void A_turned_and_scaled_entity_converts_back()
    // rotation of 90 degrees about Z, scale 2, position (3, -4, 1): for each cell of -20..20 squared,
    // WorldToCell(CellToWorld(cell, TileAnchor.Center)) == cell
[Fact] public void The_z_of_a_layer_is_its_index_times_the_spacing()   // layers Ground, Details, Overhead: 0, 0.1f, 0.2f; an unknown name throws KeyNotFoundException
[Fact] public void A_solid_tile_on_any_colliding_layer_makes_the_cell_solid()
[Fact] public void A_layer_that_does_not_collide_is_ignored()
[Fact]
public void A_cell_whose_tile_set_or_tile_is_gone_is_not_solid()
    // a cell of slot 9, and a cell of slot 1 with an id the set does not have: IsSolid is false and nothing throws
[Fact]
public void Overlap_finds_a_solid_cell_under_a_rectangle()
    // solid at (2, 3): (2.4, 3.4, 0.2, 0.2) true; (1.2, 3.2, 0.9, 0.2) true; (1, 3, 1, 1) false (touches the edge); (0, 0, 1, 1) false
[Fact] public void A_component_without_a_map_answers_without_throwing()   // IsSolid false, WorldToCell (0, 0)
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileMapComponentTests`.
- [ ] **Step 3: Write the component.**
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit and push**: `feat(tessera): the tile map component, cells and solid queries`

### Task 9: Drawing a map (R2)

**Files:**
- Create: `src/ColorRise.Tessera/Rendering/RenderTileChunk.cs`, `TileMapRenderProcessor.cs`, `TileRenderFeature.cs`, `TileTransparentRenderStageSelector.cs`, `TileQuads.cs`
- Modify: `src/ColorRise.Tessera/TileMapComponent.cs` (`[DefaultEntityComponentRenderer(typeof(TileMapRenderProcessor))]`)
- Modify: `tests/ColorRise.Tessera.Tests/Graphics/GraphicsHarness.cs` (the harness's compositor has the tile render feature and its stage selector)
- Create: `tests/ColorRise.Tessera.Tests/Graphics/TestTiles.cs`
- Test: `tests/ColorRise.Tessera.Tests/TileQuadsTests.cs`, `Graphics/TileMapDrawTests.cs`

**Interfaces:**
- Consumes: `TileBatch`, `TileQuad` (Task 4), `TileMapComponent` (Task 8), the runtime data (Tasks 5 and 6), the harness (Task 2).
- Produces, in `ColorRise.Tessera.Rendering`:

```csharp
// One per chunk of a visible layer, with the chunk's bounding box, so that chunks out of view are culled.
public sealed class RenderTileChunk : RenderObject
{
    public TileMapComponent Component { get; }
    public TileLayer Layer { get; }
    public int LayerIndex { get; }
    public TileChunk Chunk { get; }
}

public sealed class TileMapRenderProcessor : EntityProcessor<TileMapComponent, TileMapRenderProcessor.MapState>, IEntityComponentRenderProcessor
{
    public VisibilityGroup VisibilityGroup { get; set; }
    public sealed class MapState { }
}

public sealed class TileRenderFeature : RootRenderFeature
{
    public override Type SupportedRenderObjectType { get; }   // typeof(RenderTileChunk)
    internal int DrawnChunks { get; }                         // in the last frame, for the tests
}

public sealed class TileTransparentRenderStageSelector : RenderStageSelector
{
    public RenderStage? TransparentRenderStage { get; set; }
    public RenderGroupMask RenderGroup { get; set; } = RenderGroupMask.All;
}

internal static class TileQuads
{
    // The world position of the grid corner (x, y) of a layer. Every tile that has this corner gets it from this call.
    public static Vector3 Corner(in Matrix world, Vector2 cellSize, Vector2 layerOffset, float layerZ, int x, int y);
    // The texture coordinates of a tile's region after its flips, in the order bottom-left, bottom-right, top-right, top-left.
    public static void Uvs(Rectangle region, int textureWidth, int textureHeight, TileFlip flip, Span<Vector2> uvs);
}
```

- Produces, in the tests:

```csharp
internal static class TestTiles
{
    // A tile set of one texture: a flat tile of 4 by 4 pixels per colour, in a row. The tile of colours[i] is 1:i,0. PixelsPerUnit 4.
    public static TileSet Flat(GraphicsDevice device, params Color[] colors);
    // A tile set of one tile, 1:0,0, the quarters texture (8 by 8). PixelsPerUnit 8.
    public static TileSet Quarters(GraphicsDevice device);
    // A map of one tile set (slot 1) and one layer per array of rows. A row is tokens split by spaces, the top row first:
    // "." is empty, else the index of the tile in its row of the set with flips after it, as in a chunk's text ("0", "2h", "1dv").
    public static TileMap Map(TileSet set, params string[][] layers);
}
```

Models: `sources/engine/Stride.Rendering/Rendering/Sprites/SpriteRenderFeature.cs`, `RenderSprite.cs`, `SpriteRenderProcessor.cs` and `SpriteTransparentRenderStageSelector.cs`.

What the feature does: layer `i` is at the entity's local Z `i * LayerSpacing`. The colour of a chunk's draw is the layer's tint with its alpha times its opacity, times the component's colour. The sampler is point or linear clamp, by the component. A tile of the cell's size has its four corners from `TileQuads.Corner`. A larger tile is anchored at the cell's bottom-left corner and goes up and to the right, by its size in pixels over `PixelsPerUnit`. A cell whose tile the map does not have is skipped. A hidden layer, a disabled component and a component without a map have no render object. The diagonal flip of a tile that is not square is drawn as the data says, in the tile's own rectangle: the editor of step 2 does not offer it.

- [ ] **Step 1: Write the failing tests of the corners and the flips**

```csharp
[Fact]
public void A_corner_shared_by_two_cells_is_the_same_bits()
{
    // world: translation (0.1f, 0.7f, 0) times a rotation of 17 degrees about Z; cellSize (1.3f, 0.7f)
    for (var x = -40; x <= 40; x++)
        for (var y = -40; y <= 40; y++)
            Assert.Equal(TileQuads.Corner(world, cellSize, default, 0, x + 1, y), TileQuads.Corner(world, cellSize, default, 0, (x + 2) - 1, (y + 3) - 3));
}

[Theory]
// region (16, 32, 16, 16) of a 64 by 64 texture: left 0.25, right 0.5, top 0.5, bottom 0.75
// columns: flip, then u,v of the bottom-left, bottom-right, top-right and top-left corners
[InlineData(TileFlip.None,       0.25, 0.75,  0.5, 0.75,  0.5, 0.5,   0.25, 0.5)]
[InlineData(TileFlip.Horizontal, 0.5, 0.75,   0.25, 0.75, 0.25, 0.5,  0.5, 0.5)]
[InlineData(TileFlip.Vertical,   0.25, 0.5,   0.5, 0.5,   0.5, 0.75,  0.25, 0.75)]
[InlineData(TileFlip.Diagonal,   0.5, 0.5,    0.5, 0.75,  0.25, 0.75, 0.25, 0.5)]
public void Uvs_follow_the_flips(TileFlip flip, params double[] expected)
```

The diagonal row is the transpose: the top-left texel stays at the top-left, and the top-right and bottom-left ones change places.

- [ ] **Step 2: Write the failing graphics tests**, in `TileMapDrawTests.cs` (collection `Graphics`). `R`, `G`, `B`, `Y`, `W`, `K` are red, green, blue, yellow, white, black, all opaque. Unless said, the frame is 64 by 64 and the view is centred on the map.

```csharp
[Fact]
public void A_map_is_drawn_cell_by_cell_with_y_up()
{
    // set Flat(R, G, B, Y); map rows "0 1" (top) and "2 3" (bottom); view centred on (1, 1), 2 units high
    Assert.Equal(R, frame[16, 16]);   // cell (0, 1)
    Assert.Equal(G, frame[48, 16]);   // cell (1, 1)
    Assert.Equal(B, frame[16, 48]);   // cell (0, 0)
    Assert.Equal(Y, frame[48, 48]);   // cell (1, 0)
}

[Theory]
// the quarters tile in one cell, view on that cell; expected: the colours at the top-left, top-right, bottom-left, bottom-right
[InlineData("0", "RGBY")] [InlineData("0h", "GRYB")] [InlineData("0v", "BYRG")] [InlineData("0hv", "YBGR")]
[InlineData("0d", "RBGY")] [InlineData("0dh", "BRYG")] [InlineData("0dv", "GYRB")] [InlineData("0dhv", "YGBR")]
public void The_eight_flips_are_Tileds(string token, string expected)

[Fact] public void An_upper_layer_covers_a_lower_one_and_shows_it_through_its_empty_cells()   // three layers
[Fact] public void A_hidden_layer_is_not_drawn()
[Fact] public void A_layer_at_half_opacity_blends()         // white at opacity 0.5 over a black layer: each channel 127 or 128
[Fact] public void A_tint_multiplies_the_colours()          // white tiles, tint (255, 0, 0): R
[Fact] public void The_component_colour_multiplies_the_layers()
[Fact] public void A_layer_offset_moves_the_layer_only()    // Offset (1, 0) on the top layer of two
[Fact]
public void A_sprite_goes_between_two_layers_by_its_z()
    // layers Ground (K), Details (one R cell); a white SpriteComponent over the whole map at z = GetLayerZ("Details") - 0.05f:
    // the pixel of the R cell is R, the others are W
[Fact] public void A_tilted_camera_keeps_the_order_of_the_layers()   // the camera turned 30 degrees about X, the same three layers
[Fact] public void Two_maps_order_by_their_entities_z()
[Fact] public void Only_the_chunks_in_view_are_drawn()
    // a map of 10 by 10 chunks, a view of 16 units: TileRenderFeature.DrawnChunks is at most 4
[Fact] public void A_cell_changed_between_two_frames_shows_in_the_second()
[Fact] public void A_cell_whose_tile_is_gone_draws_nothing_and_does_not_throw()   // slot 9, and an id the set does not have: the background shows
[Fact] public void A_tile_larger_than_a_cell_goes_up_and_to_the_right()   // a tile of 8 by 16 pixels at PixelsPerUnit 8 in cell (0, 0): covers (0,0) and (0,1)
[Fact] public void A_map_at_a_fraction_of_a_pixel_has_no_gap()   // the gap test of Task 4 on a map of 32 by 32 white cells, in four chunks
[Fact] public void Linear_colour_space_gives_the_same_tint()     // HarnessOptions.ColorSpace = Linear: R within 3 per channel
[Fact] public void Linear_sampling_blends_two_texels_and_point_does_not()   // a tile of two colours, magnified: the middle column
```

- [ ] **Step 3: Run them to see them fail.** Filter: `FullyQualifiedName~TileQuadsTests|FullyQualifiedName~TileMapDrawTests`.
- [ ] **Step 4: Write `TileQuads`, then the render object, the processor, the feature and the selector.** The harness adds the feature and its selector to its compositor.
- [ ] **Step 5: Run the tests.** Expected: PASS. Save the frame of the three-layer test and look at it.
- [ ] **Step 6: Gate (R2).** If layers, sprites and maps do not order by depth in the transparent stage, try the fallback: a render stage of the extension's own that a game adds to its compositor. It keeps the order between layers, but a `SpriteComponent` can then no longer go between two layers, which the design promises: stop and ask Nicolas before taking it.
- [ ] **Step 7: Commit and push**: `feat(tessera): draw tile maps in the transparent stage`

### Task 10: Animated tiles

**Files:**
- Modify: `src/ColorRise.Tessera/Tile.cs` (`FrameAt`), `src/ColorRise.Tessera/Rendering/TileMapRenderProcessor.cs` (the clock), `TileRenderFeature.cs` (the frame's tile)
- Test: `tests/ColorRise.Tessera.Tests/TileAnimationTests.cs`, `Graphics/TileAnimationDrawTests.cs`

**Interfaces:**
- Consumes: `Tile.Animation`, `TileMapComponent.AnimationTime` and `AnimationSpeed`.
- Produces: `public TileId Tile.FrameAt(double time)`. Each frame, the processor adds the frame's elapsed time times `AnimationSpeed` to `AnimationTime`. A cell shows the tile `FrameAt(AnimationTime)` of its own tile, taken from the same tile set. A frame that names a tile the set does not have shows the cell's own tile.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
// frames: 1:1,0 for 0.25 s, then 1:2,0 for 0.5 s
[InlineData(0, "1:1,0")] [InlineData(0.2499, "1:1,0")] [InlineData(0.25, "1:2,0")] [InlineData(0.7499, "1:2,0")]
[InlineData(0.75, "1:1,0")] [InlineData(75.3, "1:2,0")] [InlineData(-0.1, "1:2,0")]
public void The_frame_at_a_time_loops(double time, string expected)

[Fact] public void A_tile_without_frames_is_itself()
[Fact] public void Frames_whose_durations_are_not_positive_show_the_first()   // durations 0 and -1: FrameAt(5) is the first frame's tile, nothing throws or loops

// graphics: a cell of a tile (R) whose frames are G for 0.25 s and B for 0.5 s; AnimationSpeed 0, AnimationTime set by each frame action
[Fact] public void An_animated_tile_shows_the_frame_of_its_time()   // times 0, 0.3, 0.8: G, B, G
[Fact] public void Two_cells_of_the_same_tile_are_in_step()
[Fact] public void The_clock_runs_with_the_game_at_the_components_speed()
    // AnimationSpeed 2, three frames drawn: AnimationTime is above 0 and twice what a second component at speed 1 has, within 1 %
```

- [ ] **Step 2: Run, see them fail, write the code, run again.** Filter: `FullyQualifiedName~TileAnimation`.
- [ ] **Step 3: Commit and push**: `feat(tessera): animated tiles`

### Task 11: The content types and the tile map compiler

**Files:**
- Create: `src/ColorRise.Tessera/Module.cs` (the `AssetFileExtension` attributes), serializers if the data contracts are not enough
- Modify: `src/ColorRise.Tessera/TileSet.cs`, `TileMap.cs` (content attributes), `TileLayer.cs`, `TileChunk.cs`
- Create: `src/ColorRise.Tessera.Assets/TileMapAssetCompiler.cs`
- Create: `tests/ColorRise.Tessera.Tests/Assets/AssetBuild.cs`
- Test: `tests/ColorRise.Tessera.Tests/ContentTests.cs`, `Assets/TileMapCompilerTests.cs`

**Interfaces:**
- Consumes: the runtime data (Tasks 5 and 6), `TileMapAsset.ToTileMap` (Task 7).
- Produces:
  - On `TileSet` and on `TileMap`: `[ContentSerializer(typeof(DataContentSerializer<T>))]` and `[ReferenceSerializer, DataSerializerGlobal(typeof(ReferenceSerializer<T>), Profile = "Content")]`, as the template's `samples/Plugin/Plugin/MyTemplate/MyTemplateData.cs`.
  - `[assembly: AssetFileExtension(".sdtileset", typeof(TileSet))]` and `[assembly: AssetFileExtension(".sdtilemap", typeof(TileMap))]`.
  - `public event EventHandler? TileMap.Reloaded`, raised after the content manager read the map again in place.
  - `[AssetCompiler(typeof(TileMapAsset), typeof(AssetCompilationContext))] public sealed class TileMapAssetCompiler : AssetCompilerBase`. Its build command saves `asset.ToTileMap(...)` at the asset's URL and fails with the errors of the chunks. Its runtime types name `TileSet`, so that a game's build takes the tile sets a map uses. Silence `STRDIAG010` on the command, with the reason (Skein's finding 7).
  - In the tests: `AssetBuild`, which compiles assets of a temporary package into a database and loads content from it:

```csharp
internal sealed class AssetBuild : IDisposable
{
    public AssetBuild();                                      // a package in a new temporary folder
    public string Folder { get; }
    public AssetItem Add(string url, Asset asset);            // "Maps/Town"
    public string WriteFile(string name, byte[] bytes);       // a source file beside the assets; returns its path
    public BuildResult Build();                               // Succeeded, Errors, Warnings (texts), and what each command did
    public T Load<T>(string url) where T : class;
    public ContentManager Content { get; }
}
```

Model: how Skein's `tests/ColorRise.Skein.Tests` compile a Yarn project "as the build of the game does".

- [ ] **Step 1: Write the failing tests**

```csharp
// ContentTests: written and read back with a content manager
[Fact]
public void A_tile_map_comes_back_as_it_was_saved()
    // CellSize (2, 0.5), Size (40, 30), tile sets in slots 1 and 7, two layers with every property changed from its default,
    // cells in the chunks (-1, -1) and (3, 2) with flips: every value is equal after loading, and layer.Map is the loaded map
[Fact]
public void A_tile_set_comes_back_as_it_was_saved()
    // PixelsPerUnit 8, TileSize (16, 24), three tiles; one with Solid, Kind "water", two animation frames, and the properties
    // "depth" = 3, "speed" = 0.5f, "deep" = true, "name" = "pond", "tint" = new Color(1, 2, 3, 4)
[Fact]
public void A_map_read_again_keeps_its_object_and_tells()
    // load the map, save another map at the same URL, Content.Reload(map): the same object has the new cells, Reloaded was raised once

// graphics, collection "Graphics"
[Fact] public void A_reloaded_map_shows_its_new_cells_in_the_next_frame()

// TileMapCompilerTests
[Fact]
public void A_map_asset_compiles_to_its_cells()
    // SmallMap() of Task 7 with a tile set asset in slot 1 (use TileSet content saved by the test until Task 13 exists):
    // the loaded TileMap has the cells of the asset, cell by cell, and TileSets[1] is loaded
[Fact]
public void A_bad_chunk_fails_the_build_and_is_named()
    // Succeeded is false; one error has "Maps/Town", "Ground", "(-1, 0)" and "line"
[Fact]
public void A_cell_of_a_slot_the_map_does_not_have_is_kept()
    // the asset has a cell of slot 9 and no slot 9: the build succeeds with no error, and the loaded map has the cell
[Fact] public void A_second_build_runs_no_command()
[Fact] public void A_change_of_a_cell_builds_the_map_again()
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~ContentTests|FullyQualifiedName~TileMapCompilerTests`.
- [ ] **Step 3: Write `AssetBuild`, the content attributes and serializers, and the compiler.** If Stride's serializer cannot write a `Dictionary<string, object>` of the five property types, write a data serializer for `Tile` and keep the public shape.
- [ ] **Step 4: Run the tests.** Expected: PASS.
- [ ] **Step 5: Commit and push**: `feat(tessera): tile maps and tile sets as content, and the tile map compiler`

### Task 12: The tile set asset and its tiles

**Files:**
- Create: `src/ColorRise.Tessera.Assets/TileSetAsset.cs`, `TileSetSource.cs`, `GridTileSetSource.cs`, `SpriteSheetTileSetSource.cs`, `SpriteTileBinding.cs`, `TileDefinition.cs`, `TileSlice.cs`, `TileSlicer.cs`
- Test: `tests/ColorRise.Tessera.Tests/Assets/TileSlicerTests.cs`, `TileSetAssetYamlTests.cs`, `Assets/Golden/small.sdtileset`

**Interfaces:**
- Consumes: `TileId` and its YAML serializer (Tasks 3 and 7), `TileCollision`, `TileAnimationFrame`, `TileSet` (Task 5).
- Produces, in `ColorRise.Tessera.Assets`:

```csharp
[DataContract("TileSetAsset")]
[AssetDescription(".sdtileset")]
[AssetContentType(typeof(TileSet))]
[AssetFormatVersion("ColorRise.Tessera", "1.0.0.0")]
[Display(100, "Tile set")]
public sealed class TileSetAsset : Asset
{
    public Int2 TileSize { get; set; } = new(16, 16);
    public float PixelsPerUnit { get; set; } = 16;
    // The texture options of SpriteSheetAsset, with its member names and defaults: the color key and its switch, the alpha
    // format, premultiplied alpha, the colour space. Compression has the same name and is off by default.
    public List<TileSetSource> Sources { get; } = [];
    public List<TileDefinition> Tiles { get; } = [];     // only the tiles that have data; kept sorted by id
    public int LastSourceId { get; set; }                // hidden; an id is never given twice
    public T AddSource<T>(string name) where T : TileSetSource, new();   // Id = ++LastSourceId; InvalidOperationException above 255
    public TileDefinition? GetDefinition(TileId tile);
    public TileDefinition GetOrAddDefinition(TileId tile);
    public void RemoveDefaultDefinitions();              // a definition that says nothing is removed
}

[DataContract(Inherited = true)]
public abstract class TileSetSource { public int Id { get; set; } public string Name { get; set; } = ""; }

[DataContract("GridTileSetSource")]
public sealed class GridTileSetSource : TileSetSource
{
    public UFile? Image { get; set; }    // a source file of the asset
    public Int2 TileSize { get; set; }   // (0, 0) takes the tile set's
    public Int2 Margin { get; set; }     // pixels around the grid
    public Int2 Spacing { get; set; }    // pixels between tiles
}

[DataContract("SpriteSheetTileSetSource")]
public sealed class SpriteSheetTileSetSource : TileSetSource
{
    public SpriteSheet? Sheet { get; set; }
    public List<SpriteTileBinding> Bindings { get; } = [];
    public int LastNumber { get; set; }  // hidden; a number is never given twice
}

[DataContract] public sealed class SpriteTileBinding { public int Number { get; set; } public string SpriteName { get; set; } = ""; }

[DataContract]
public sealed class TileDefinition
{
    public TileId Tile { get; set; }
    public TileCollision Collision { get; set; }
    public string? Kind { get; set; }
    public Dictionary<string, object> Properties { get; } = [];
    public List<TileAnimationFrame> Animation { get; } = [];
    public bool IsDefault { get; }
}

public readonly record struct TileSlice(TileId Tile, UFile Image, Rectangle Region);    // the region in the image, in pixels
public readonly record struct NamedRegion(string Name, UFile Image, Rectangle Region);  // a sprite of a sheet

public static class TileSlicer
{
    // The grid positions of a source: each position wholly inside the image, by row then column. No look at the pixels.
    public static List<TileSlice> Grid(GridTileSetSource source, Int2 defaultTileSize, Int2 imageSize);
    // True when every pixel of the region has an alpha of 0 or is the color key.
    public static bool IsEmpty(ReadOnlySpan<Color> pixels, int imageWidth, Rectangle region, Color? colorKey);
    // The tiles of a sheet source: one per binding whose sprite exists. A name used twice takes the first sprite.
    public static List<TileSlice> Sheet(SpriteSheetTileSetSource source, IReadOnlyList<NamedRegion> sprites,
        out List<SpriteTileBinding> missing, out List<string> duplicateNames, out int unbound);
    // Gives a number to each sprite name that has none, in the order given. True when a binding was added.
    public static bool Bind(SpriteSheetTileSetSource source, IEnumerable<string> spriteNames);
}
```

A position fits when `Margin.X + column * (width + Spacing.X) + width <= imageWidth`, and the same for rows. This is Tiled's rule: the margin moves the start of the grid and asks for nothing at the far edge. The grid stops at column 4095 and row 2047.

- [ ] **Step 1: Write the failing tests**

```csharp
// TileSlicerTests
[Fact]
public void A_grid_gives_a_tile_per_position_inside_the_image()
{
    var slices = TileSlicer.Grid(new GridTileSetSource { Id = 1 }, new Int2(16, 16), new Int2(64, 48));
    Assert.Equal(12, slices.Count);
    Assert.Equal(TileId.FromGrid(1, 0, 0), slices[0].Tile);
    Assert.Equal(new TileSlice(TileId.FromGrid(1, 3, 2), null!, new Rectangle(48, 32, 16, 16)), slices[11] with { Image = null! });
}

[Fact]
public void Margin_and_spacing_move_the_grid()
    // image 70 by 36, tiles of 16, Margin (2, 1), Spacing (1, 2): columns at x = 2, 19, 36, 53 (the next, 70, is outside), rows at y = 1, 19
    // 8 slices; the last is 1:3,1 with the region (53, 19, 16, 16)
[Fact] public void A_part_of_a_tile_at_the_edge_is_dropped()        // image 40 by 16: 2 slices
[Fact] public void The_tile_size_of_a_source_wins_over_the_tile_sets()   // source TileSize (16, 24), image 32 by 48: 4 slices of 16 by 24

[Theory]
[InlineData(0, 16, 0, 0, 0, 0)]      // a width of zero
[InlineData(16, -4, 0, 0, 0, 0)]     // a height below zero
[InlineData(128, 16, 0, 0, 0, 0)]    // a tile wider than the image (64 by 48)
[InlineData(16, 16, 80, 0, 0, 0)]    // a margin wider than the image
[InlineData(16, 16, 0, 0, -20, 0)]   // a spacing that would walk backwards
public void Numbers_that_make_no_tile_give_no_tile_and_do_not_throw(int w, int h, int mx, int my, int sx, int sy)

[Fact] public void A_grid_stops_at_the_last_column_and_row_an_id_can_hold()   // image 5000 by 1 pixels, tiles of 1: 4096 slices
[Fact] public void A_region_of_alpha_zero_is_empty_and_one_opaque_pixel_makes_it_a_tile()
[Fact] public void The_color_key_counts_as_empty()

[Fact]
public void Sprites_are_bound_by_name_and_keep_their_numbers()
    // Bind(["grass", "water", "rock"]) gives 1, 2, 3 and returns true; Bind of the same names in another order returns false;
    // Sheet(...) with the sprites reordered gives the same id to each name
[Fact]
public void A_renamed_sprite_is_missing_and_a_new_one_is_unbound()
    // bound: grass, water. sprites: grass, pond. missing has the binding of water, unbound is 1, one slice (grass)
[Fact] public void A_name_used_twice_takes_the_first_sprite()   // duplicateNames has it once
[Fact] public void A_number_is_not_given_twice()   // bind a, b; remove the binding of b; bind c: c gets 3

// TileSetAssetYamlTests
[Fact] public void A_tile_set_saved_loaded_and_saved_again_has_the_same_bytes()
[Fact] public void A_small_tile_set_is_the_golden_file()
    // two sources (a grid with a margin, a sheet with two bindings), a definition with Solid and Kind, one with two frames:
    // in the file, a tile id reads as 1:3,4 and 2:#1, and no definition is written for a tile without data
[Fact] public void Sources_get_ids_that_are_never_reused()   // add a, b; remove b; add c: ids 1 and 3
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileSlicerTests|FullyQualifiedName~TileSetAssetYamlTests`.
- [ ] **Step 3: Write the types.** Read `sources/engine/Stride.Assets/Sprite/SpriteSheetAsset.cs` for the texture options.
- [ ] **Step 4: Run the tests, write the golden file as in Task 7, read it, run again.** Expected: PASS.
- [ ] **Step 5: Commit and push**: `feat(tessera): the tile set asset and the tiles of its sources`

### Task 13: The tile set compiler and its atlas

**Files:**
- Create: `src/ColorRise.Tessera.Assets/TileAtlasBuilder.cs`, `TileSetAssetCompiler.cs`
- Test: `tests/ColorRise.Tessera.Tests/Assets/TileAtlasBuilderTests.cs`, `TileSetCompilerTests.cs`

**Interfaces:**
- Consumes: `TileSlicer`, `TileSlice`, `TileSetAsset` (Task 12), `TileSet`, `Tile` (Task 5), `AssetBuild` (Task 11).
- Produces, in `ColorRise.Tessera.Assets`:

```csharp
internal sealed class TileAtlas
{
    public List<Image> Textures { get; }
    public Dictionary<TileId, (int Texture, Rectangle Region)> Placements { get; }   // the region without the border
}

internal static class TileAtlasBuilder
{
    public const int Border = 2;
    // Packs the tiles into as few images as fit maxSize. Around each tile, Border pixels repeat the tile's edge.
    // No tile is rotated. A slice whose pixels are all empty is left out.
    public static TileAtlas Build(IReadOnlyList<TileSlice> slices, Func<UFile, Image> loadImage, Color? colorKey, int maxSize = 2048);
}

[AssetCompiler(typeof(TileSetAsset), typeof(AssetCompilationContext))]
public sealed class TileSetAssetCompiler : AssetCompilerBase
```

The compiler reads each sheet source's sprite sheet **asset** in `Prepare` (its sprites' names, source images and regions) and gives them to the command with the tile set. It declares `SpriteSheetAsset` as an input type with `BuildDependencyType.CompileAsset`. The command's input files are the images. The command saves the textures at `<url>__ATLAS_TEXTURE__<n>` and the `TileSet` at the asset's URL. Models: `sources/engine/Stride.Assets/Sprite/SpriteSheetAssetCompiler.cs` (`SpriteSheetCommand`, for `TexturePacker`, `AtlasTextureFactory` and `TextureHelper`) and `sources/engine/Stride.Assets/Textures/Packing/`.

Build messages: an image that cannot be read is an error with the file and the source's name. A source without a tile, sprites that are bound and missing, sprites that are not bound, a sprite name used twice, and an animation frame that names a tile the set does not have are warnings, each with the count and the names.

- [ ] **Step 1: Write the failing tests**

```csharp
// TileAtlasBuilderTests: images made in the test
[Fact]
public void Each_tile_has_a_border_of_two_pixels_that_repeats_its_edge()
    // an image of two tiles of 16 by 16, red and blue: for the red tile's region r in its texture, the pixels
    // (r.X - 1, r.Y), (r.X - 2, r.Y + 5), (r.Right, r.Y + 3), (r.Right + 1, r.Bottom - 1), (r.X + 4, r.Y - 2), (r.X + 4, r.Bottom + 1) are red
[Fact] public void The_corner_of_a_border_repeats_the_corner_pixel()   // a tile with four different corner pixels
[Fact] public void A_tile_keeps_its_size_and_is_not_rotated()   // a tile of 16 by 24: its region is 16 by 24
[Fact] public void The_pixels_of_a_tile_are_those_of_its_slice()
[Fact]
public void Tiles_that_do_not_fit_one_texture_go_to_another()
    // 300 tiles of 16 by 16 with maxSize 256: at least two textures, none larger than 256, 300 placements, no two regions of one texture overlap
[Fact] public void An_empty_position_is_left_out()   // 4 positions, one of alpha 0: 3 placements
[Fact] public void The_color_key_becomes_transparent()
[Fact] public void No_slice_gives_no_texture()

// TileSetCompilerTests
[Fact]
public void A_tile_set_compiles_to_its_tiles_with_their_data()
    // "tiles.png", 32 by 16: a red tile and a blue tile. A grid source. A definition: 1:1,0 is Solid, Kind "wall", "hp" = 3.
    // Loaded: 2 tiles; GetTile(1:0,0) has a texture and a region of 16 by 16; GetTile(1:1,0) is solid, "wall", Properties["hp"] is 3
    // The pixel at the middle of each tile's region in its texture is red, then blue (read the texture's image back)
[Fact]
public void A_sprite_sheet_gives_tiles_like_a_grid()
    // a SpriteSheetAsset of two sprites "grass" and "water" over one image, bound 1 and 2 in source 2:
    // the tile set has 2:#1 and 2:#2, each with the size of its sprite's region; the sheet's packing and pixels per unit play no part
[Fact] public void A_second_build_runs_no_command()
[Fact] public void A_change_of_the_image_builds_the_tile_set_again()
[Fact] public void A_change_of_the_sprite_sheet_builds_the_tile_set_again()
[Fact]
public void An_image_that_is_missing_fails_the_build_and_is_named()
    // Succeeded is false, an error has "tiles.png" and the source's name; the same for a file that is not an image
[Fact] public void A_missing_sprite_and_an_unbound_sprite_are_warnings()   // Succeeded is true; the warnings name "water" and say 1 sprite is not bound
[Fact] public void A_source_without_a_tile_is_a_warning()                 // a grid whose tile size is larger than the image
[Fact] public void An_animation_frame_of_a_tile_that_is_gone_is_a_warning()
```

- [ ] **Step 2: Run them to see them fail.** Filter: `FullyQualifiedName~TileAtlasBuilderTests|FullyQualifiedName~TileSetCompilerTests`.
- [ ] **Step 3: Write the atlas builder, then the compiler and its command.** The command runs inside Stride's asset compiler, which has the texture tools' native libraries.
- [ ] **Step 4: Run the tests.** Expected: PASS. If the packing classes cannot be used from a third party's `Assets` package (a missing native library, an internal type in a public signature), write the finding and pack with code of the extension's own: tiles in rows, with the same border.
- [ ] **Step 5: Commit and push**: `feat(tessera): the tile set compiler and its padded atlas`

### Task 14: From assets to a frame, from packages, and the write-up

**Files:**
- Test: `tests/ColorRise.Tessera.Tests/Graphics/EndToEndTests.cs`, `BleedingTests.cs`
- Create: `tests/PackagesCheck/run.sh`, `tests/PackagesCheck/Game/` (a game's project, its `Program.cs`, a `nuget.config`, a tile set, a map and an image)
- Modify: `README.md`, `docs/plugin-findings.md`, `docs/plan.md`

**Interfaces:**
- Consumes: everything above.
- Produces: `tests/PackagesCheck/run.sh`, which prints `TESSERA-PACKAGES-CHECK OK` and exits with 0, or says what failed and exits with 1.

- [ ] **Step 1: Write the failing end-to-end tests**

```csharp
[Collection(GraphicsCollection.Name)]
public sealed class EndToEndTests
{
    [Fact]
    public void A_map_built_from_assets_is_drawn_by_a_game()
        // an image of four tiles (R, G, B, Y) in a row; a tile set asset over it; a map asset whose layer has the rows "0 1" and "2 3";
        // AssetBuild compiles both; the harness game loads "Maps/Town" from that database into a TileMapComponent:
        // the four pixels of A_map_is_drawn_cell_by_cell_with_y_up are R, G, B, Y

    [Fact] public void A_tile_set_of_a_sprite_sheet_is_drawn_the_same()
}

[Collection(GraphicsCollection.Name)]
public sealed class BleedingTests
{
    // a tile set compiled from an image of a red tile between two blue tiles; a map of 8 by 8 red cells
    [Theory]
    [InlineData(TileSampler.Point, 1)] [InlineData(TileSampler.Linear, 1)] [InlineData(TileSampler.Point, 4)] [InlineData(TileSampler.Linear, 4)]
    public void No_neighbour_of_the_atlas_shows_at_any_zoom(TileSampler sampler, int msaa)
        // for unitsHigh in 2.9f, 7.77f, 13.4f and 16 offsets of a sixteenth of a pixel: inside the map, one pixel in from its
        // edges, every pixel has blue 0 and red 255, and none is the background
}
```

- [ ] **Step 2: Run them, and fix what they find.** Filter: `FullyQualifiedName~EndToEndTests|FullyQualifiedName~BleedingTests`. Expected: PASS. If a neighbour shows (R8), first make the border larger, then inset the texture coordinates by a fraction of a texel, and write which was needed.

- [ ] **Step 3: Write the packages check.** `run.sh` packs the solution, copies `tests/PackagesCheck/Game` to a new folder outside the checkout, builds it there with only `<PackageReference Include="ColorRise.Tessera" Version="0.1.0-dev" />` and Stride's packages, and runs it. The game loads its map, draws one frame without a window, checks four pixels and prints the line. Its `nuget.config` names nuget.org and the two local feeds by their full paths, which `run.sh` writes in.

Run: `extensions/ColorRise.Tessera/tests/PackagesCheck/run.sh`
Expected: `TESSERA-PACKAGES-CHECK OK`. If the asset compiler's restore of the companion fails on the signatures of this sandbox, use Skein's workaround (its finding 21) inside `run.sh`, and say in the third table of the findings that it still holds.

- [ ] **Step 4: Run every test once**

Run: `dotnet test extensions/ColorRise.Tessera/ColorRise.Tessera.slnx`
Expected: PASS, no test skipped. Keep the output for the report.

- [ ] **Step 5: Write up.** `README.md`: "What works today" says what a person can try at this commit, in Skein's manner, and "Not yet" lists the editors, the flipbooks, the sample, Windows and the WPF Game Studio. `docs/plugin-findings.md`: a row for each gap met, the three tables complete. `docs/plan.md`: the boxes ticked, and a short note under a task where what was done differs from the plan.

- [ ] **Step 6: Commit and push**: `test(tessera): maps from assets to a frame, and from packages`, then `docs(tessera): what step 1 gives, and what it found in the plugin model`

- [ ] **Step 7: Report to Nicolas.** The branch, the test count, the result of each of R1, R2, R3, R8, R9 and R10 in a line, the findings, and three frames saved in `/mnt/project-files/tilemap/`: the three-layer test, the gap test and the end-to-end map. Then the plan of step 2.
