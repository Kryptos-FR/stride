# Avalonia UI for Stride games (prototype)

`Stride.AvaloniaUI` lets a game write its UI with Avalonia (XAML, controls, styles, bindings) and draws it with Stride. It is an alternative to `Stride.UI`, not part of it: a game uses one or the other.

## Using it

Reference `Stride.AvaloniaUI` and `Stride.AvaloniaUI.Skia` from the game, then add an **Avalonia UI** component to an entity of the scene:

- `ViewType`: the type of a control to show, such as a `UserControl` written in XAML (`MyGame.Views.MainMenu, MyGame`), or
- `Content`: a control set from a script, which takes precedence.

The placement properties keep the names and meaning of `UIComponent`'s: `Resolution` (virtual resolution, 1280x720 by default) and `ResolutionStretch`. `DrawOrder` orders several views: the higher one is drawn on top and gets the input first.

A game that doesn't set up Avalonia gets the Fluent theme, dark, with the Inter font, and the Skia renderer, found among the packages the game references. To use its own application (resources, styles, theme), the game sets Avalonia up on the game thread before it runs:

```csharp
AppBuilder.Configure<MyApp>().UseStride().WithInterFont().SetupWithoutStarting();
using var game = new Game();
game.Run();
```

The native renderer, Stride's own, is opt-in: such a game sets Avalonia up with `UseStride(AvaloniaUIRenderer.Native)`. It still needs the Skia package, through which it draws on the CPU for now, so `RenderOnGpu` has no effect with it.

## How it works

- Two packages: `Stride.AvaloniaUI` (the platform, the component and the game system; no Skia) and `Stride.AvaloniaUI.Skia` (the Skia renderer). The renderer is chosen once per process when Avalonia is set up (`UseStride(renderer)`), as Avalonia has one render interface per process. The core finds the Skia package among the registered assemblies, through its `[AssemblyScan]` index, or gets it from `UseSkiaFallback()` on the `AppBuilder`.
- `Platform/`: an Avalonia platform with no native windows. Avalonia's dispatcher and render loop run on the game thread, driven by the game loop (`StrideDispatcherImpl`, `StrideRenderTimer`). Each view is an offscreen top level (`StrideTopLevelImpl`).
- `Stride.AvaloniaUI.Skia/WholeFrame/`: Skia draws whole frames (`SkiaRendererBackend`), on the game's own Vulkan device. Skia renders each view into a texture of the device, with the device's queue, and the game draws that texture without a copy. Avalonia takes it as the GPU of its Skia backend (`StrideSkiaGpu`, given through `SkiaGpuProxy`); `Stride.Graphics` gives the Vulkan handles Skia needs (`VulkanInterop`). Where Skia can't share the device, it renders on the CPU into memory that keeps the previous frame, and the game uploads the frames that changed.
- `Rendering/`: the native renderer (`NativeRendererBackend`). Avalonia's geometry, bitmaps and glyph runs are the core's own (`StrideRenderInterface`), and the drawing context records each frame's calls (`RecordingDrawingContext`) for a fallback to draw (`IRenderFallback`). The Skia package's fallback converts the core's objects to Skia's and draws the calls into the frame's memory with Skia's own drawing context (`Stride.AvaloniaUI.Skia/Fallback/`).
- `AvaloniaUISystem` (a `[GameSystem]`): pumps Avalonia in the update, renders the views in the draw, and draws their frames over the back buffer after the scene. `RenderOnGpu` (on by default) chooses Skia on the device or on the CPU; a change takes effect at the next frame.
- Input goes through the `InputManager`'s capture: a view captures the mouse (`Hover` priority) while it is over one of its controls or a button pressed on it is held, and the keyboard (`Focus` priority) while a text box has the focus, so scripts don't see what the UI takes. A click where no control is gives the focus back to the game, and a game drag that crosses the UI stays with the game.

## Status and limits

This is the design in the analysis document up to Skia on Stride's own Vulkan device; Skia on Direct3D 12 comes next. The native Stride renderer has its first step.

- The native renderer is opt-in (`AvaloniaUIRenderer.Native`). In this step it records Avalonia's drawing with Stride's own geometry, bitmaps and text, and draws it through the Skia package on the CPU, so it needs that package and draws the same pixels as Skia on the CPU.
- Skia renders on the GPU with Vulkan only. With Direct3D 11 or 12 and OpenGL, it renders on the CPU and each changed frame is uploaded whole.
- Avalonia has one GPU in a process: it follows the device of the last game that loaded its graphics content.
- Avalonia's Skia GPU interface is internal: the platform implements it with runtime code generation, so where there is none (NativeAOT) Skia renders on the CPU.
- Text is anti-aliased in grayscale, as the views are drawn over the game (subpixel text needs an opaque background).
- Views are full screen overlays drawn after the scene; views in the 3D world (like `UIComponent`'s non full screen mode) are not done yet.
- Avalonia's colors are blended in sRGB and Stride's in linear: on an sRGB back buffer, anti-aliased edges can look slightly different from a desktop Avalonia app.
- Touch and pens act as the left mouse button, first finger only. Text input takes committed text; IME compositions in progress are not shown.
- Not yet: clipboard, cursors (the requested cursor type is in `StrideTopLevelImpl.Cursor` but the game's cursor doesn't follow it), editor support (the editor's scene view doesn't show the views). Popups (combo box lists, menus, tool tips) are not tested yet.
- The platform uses Avalonia APIs marked private or internal, so the package depends on the exact Avalonia version it was built with.

## Tests

`Stride.AvaloniaUI.Tests` runs a headless game with simulated input for the layout, rendering and input capture, and games on the GPU (Lavapipe on CI) that check the view is drawn over the scene and takes its clicks with Skia on the device and on the CPU, that both draw the same frame, and that the view is drawn again when it moves between them. `Stride.AvaloniaUI.Native.Tests` runs games with the native renderer, as Avalonia has one renderer per process. With `VK_INSTANCE_LAYERS=VK_LAYER_KHRONOS_validation`, the Vulkan validation layer fails the GPU tests on any error.

Both projects draw the reference views (`Stride.AvaloniaUI.Tests/Shared/ReferenceViews.cs`: nine pages of 640x480 pixels, from the Fluent controls to shapes, text, gradients, images, paths, clips, opacity and effects) and compare each frame with its gold image, `tests/baselines/<project>/<platform>.<API>/<device>/ReferenceViewTests.<Page>.png`. The golds are the frames of the whole-frame Skia renderer on the CPU. The native project holds copies of them, and `GoldTests` checks that they are the same files, so the native renderer is held to Skia's frames. Golds exist for Linux with Vulkan (Lavapipe); the Direct3D 11 and 12 golds are made on Windows.

To make the golds again, after a page or the Skia renderer changed:

1. Run the Skia project's reference views: `dotnet test sources/plugins/AvaloniaUI/Stride.AvaloniaUI.Tests --filter "FullyQualifiedName~ReferenceViewTests"` (on Linux, with `-p:StridePlatforms=Linux -p:StrideGraphicsApis=Vulkan`). The frames that have no gold, or that differ from it, are written under `tests/local/Stride.AvaloniaUI.Tests/`.
2. Look at each image. A page shows nothing that animates, so two runs give the same files.
3. Copy the `.png` and `.metadata.json` files to the same folder under both `tests/baselines/Stride.AvaloniaUI.Tests/` and `tests/baselines/Stride.AvaloniaUI.Native.Tests/`, and commit them with the change that made them.
