# Orb Hunt: an Avalonia UI sample

A small game whose menus and HUD are Avalonia views written in XAML, drawn by Stride over the game. Orbs float over a field of stars: click them before they fade, the small ones are worth more, and the red ones cost a life.

It shows how a game uses `Stride.AvaloniaUI`:

- **Setting Avalonia up with the game's own application** (`Program.cs`): `AppBuilder.Configure<SampleApp>().UseStride(renderer).UseSkiaFallback().WithInterFont().SetupWithoutStarting()`, on the game thread, before the game runs. `SampleApp.axaml` holds the theme (Fluent, dark, with the game's accent color), the colors and the styles the views share.
- **Two views, two ways** (`OrbHuntGame.cs`): the HUD names its view by type (`ViewType`), as a scene asset made in Game Studio does, and the game gives it its view model once it is created; the menus are a view made in code (`Content`), drawn over the HUD (`DrawOrder`).
- **Bindings to a view model** (`ViewModels/GameViewModel.cs`): compiled bindings (`x:DataType`), commands, a collection for the best scores, data templates, style classes set from bindings (`Classes.lost`, `Classes.latest`) and a transition.
- **The game and the UI sharing the input** (`Gameplay/OrbField.cs`): the game script reads the mouse and keyboard as any script does. A click on a control of the HUD goes to the control, a click anywhere else goes to the game. A menu covers the screen, so the game gets no click while one is shown. While the name text box has the focus, the game doesn't see the keys typed in it.
- **Showing and hiding views**: each view is an entity component the game enables and disables.

The scene is built in code and the field is drawn with a sprite batch, so the sample needs no assets and builds with the engine.

## Running it

From the repository root:

```bash
# Windows (Direct3D 11)
dotnet run --project sources/plugins/AvaloniaUI/Stride.AvaloniaUI.Sample
# Linux (Vulkan)
dotnet run --project sources/plugins/AvaloniaUI/Stride.AvaloniaUI.Sample -p:StridePlatforms=Linux -p:StrideGraphicsApis=Vulkan
```

Add `-- --native` to draw the UI with Stride's native renderer instead of Skia. The main menu says which renderer draws it, and whether on the GPU or the CPU.

Esc or P pauses a round.
