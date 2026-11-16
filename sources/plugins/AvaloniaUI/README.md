# Avalonia UI for Stride games (prototype)

`Stride.AvaloniaUI` lets a game write its UI with Avalonia (XAML, controls, styles, bindings) and draws it with Stride. It is an alternative to `Stride.UI`, not part of it: a game uses one or the other.

## Using it

Reference `Stride.AvaloniaUI` from the game, then add an **Avalonia UI** component to an entity of the scene:

- `ViewType`: the type of a control to show, such as a `UserControl` written in XAML (`MyGame.Views.MainMenu, MyGame`), or
- `Content`: a control set from a script, which takes precedence.

The placement properties keep the names and meaning of `UIComponent`'s: `Resolution` (virtual resolution, 1280x720 by default) and `ResolutionStretch`. `DrawOrder` orders several views: the higher one is drawn on top and gets the input first.

A game that doesn't set up Avalonia gets the Fluent theme, dark, with the Inter font. To use its own application (resources, styles, theme), the game sets Avalonia up on the game thread before it runs:

```csharp
AppBuilder.Configure<MyApp>().UseStride().WithInterFont().SetupWithoutStarting();
using var game = new Game();
game.Run();
```

## How it works

- `Platform/`: an Avalonia platform with no native windows. Avalonia's dispatcher and render loop run on the game thread, driven by the game loop (`StrideDispatcherImpl`, `StrideRenderTimer`). Each view is an offscreen top level (`StrideTopLevelImpl`) that Skia renders on the CPU into memory that keeps the previous frame, so Avalonia redraws only what changed.
- `AvaloniaUISystem` (a `[GameSystem]`): pumps Avalonia in the update, renders the views in the draw, uploads the frames that changed to textures and draws them over the back buffer after the scene.
- Input goes through the `InputManager`'s capture: a view captures the mouse (`Hover` priority) while it is over one of its controls or a button pressed on it is held, and the keyboard (`Focus` priority) while a text box has the focus, so scripts don't see what the UI takes. A click where no control is gives the focus back to the game, and a game drag that crosses the UI stays with the game.

## Status and limits

This is the CPU foundation of the design in the analysis document; Skia on Stride's own Vulkan or Direct3D 12 device comes next, then a native Stride renderer as research.

- Views are full screen overlays drawn after the scene; views in the 3D world (like `UIComponent`'s non full screen mode) are not done yet.
- Each changed frame is uploaded whole.
- Avalonia's colors are blended in sRGB and Stride's in linear: on an sRGB back buffer, anti-aliased edges can look slightly different from a desktop Avalonia app.
- Touch and pens act as the left mouse button, first finger only. Text input takes committed text; IME compositions in progress are not shown.
- Not yet: clipboard, cursors (the requested cursor type is in `StrideTopLevelImpl.Cursor` but the game's cursor doesn't follow it), editor support (the editor's scene view doesn't show the views). Popups (combo box lists, menus, tool tips) are not tested yet.
- The platform uses Avalonia APIs marked private, so the package depends on the exact Avalonia version it was built with.

## Tests

`Stride.AvaloniaUI.Tests` runs a headless game with simulated input for the layout, rendering and input capture, and a game on the GPU (Lavapipe on CI) that checks the view is drawn over the scene and takes its clicks.
