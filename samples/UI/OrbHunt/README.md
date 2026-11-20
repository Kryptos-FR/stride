# Orb Hunt: an Avalonia UI sample

A small game whose menus and HUD are Avalonia views written in XAML, drawn by Stride over the game. Orbs float over a field of stars: click them before they fade, the small ones are worth more, and the red ones cost a life.

It shows how a game uses `Stride.AvaloniaUI`:

- **Setting Avalonia up with the game's own application** (`OrbHuntGame.Run`): `AppBuilder.Configure<OrbHuntApp>().UseStride(renderer).UseSkiaFallback().WithInterFont().SetupWithoutStarting()`, on the game thread, before the game runs. `OrbHuntApp.axaml` holds the theme (Fluent, dark, with the game's accent color), the colors and the styles the views share.
- **Views in a scene** (`Assets/MainScene.sdscene`): the HUD and the menus are Avalonia UI components that name their view by type (`ViewType`), with the menus drawn over the HUD (`DrawOrder`). The game script references both components and gives each view its view model once the view is created. A script can also set a view made in code (`Content`).
- **Bindings to a view model** (`ViewModels/GameViewModel.cs`): compiled bindings (`x:DataType`), commands, a collection for the best scores, data templates, style classes set from bindings (`Classes.lost`, `Classes.latest`) and a transition.
- **The game and the UI sharing the input** (`Gameplay/OrbField.cs`): the game script reads the mouse and keyboard as any script does. A click on a control of the HUD goes to the control, a click anywhere else goes to the game. A menu covers the screen, so the game gets no click while one is shown. While the name text box has the focus, the game doesn't see the keys typed in it.
- **Showing and hiding views**: each view is an entity component the game enables and disables.

## The game

Orb Hunt is laid out as Game Studio makes a game, and opens in Game Studio (`OrbHunt.slnx`):

- `OrbHunt.Game`: the game library, with its package (`OrbHunt.Game.sdpkg`), its assets, the scripts, the views and the view models. It uses Stride and the plugin as packages (`Stride.Engine`, `Stride.AvaloniaUI`, `Stride.AvaloniaUI.Skia`), plus Avalonia itself for the XAML compiler and the theme.
- `OrbHunt.Windows` and `OrbHunt.Linux`: the executables, which call `OrbHuntGame.Run`.
- `Assets/GameSettings.sdgamesettings`: the main scene, the graphics compositor and the resolution.
- `Assets/MainScene.sdscene`: the entities of the HUD, the menus and the orb field (the `OrbField` script).
- `Assets/GraphicsCompositor.sdgfxcomp`: clears the screen and draws the field with the `Orb field` renderer (`Gameplay/OrbFieldRenderer.cs`). The field is drawn with a sprite batch, so the game needs no models, materials or effects.

The plugin isn't published yet, so its packages come from a build of this repository: build the engine and the plugin first (`Stride.slnx`), which makes the development packages in `bin/packages`. They have the version of the build (`4.5.4-dev` here), so the game references that version. Game Studio offers to upgrade the references when the version of the repository changes.

## Running it

From this folder, once the engine and the plugin are built:

```bash
# Windows (Direct3D 11)
dotnet run --project OrbHunt.Windows
# Linux (Vulkan)
dotnet run --project OrbHunt.Linux
```

Add `-- --native` to draw the UI with Stride's native renderer instead of Skia. The main menu says which renderer draws it, and whether on the GPU or the CPU.

Esc or P pauses a round.
