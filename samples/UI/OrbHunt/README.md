# Orb Hunt: an Avalonia UI sample

A small game whose menus and HUD are Avalonia views written in XAML, drawn by Stride over the game. Orbs float in front of a planet: click them before they fade, the small ones are worth more, and the red ones cost a life.

It shows how a game uses `Stride.AvaloniaUI`:

- **Setting Avalonia up with the game's own application** (`OrbHuntGame.Run`): `AppBuilder.Configure<OrbHuntApp>().UseStride(renderer).UseSkiaFallback().WithInterFont().SetupWithoutStarting()`, on the game thread, before the game runs. `OrbHuntApp.axaml` holds the theme (Fluent, dark, with the game's accent color), the colors and the styles the views share.
- **Views in a scene** (`Assets/PlanetScene.sdscene`): the HUD and the menus are Avalonia UI components that name their view by type (`ViewType`), with the menus drawn over the HUD (`DrawOrder`). The game script references both components and gives each view its view model once the view is created. A script can also set a view made in code (`Content`).
- **A UI over a 3D scene**: the views are drawn over whatever the graphics compositor drew, here a planet that turns in front of a sky of stars, seen by a camera that circles it.
- **Bindings to a view model** (`ViewModels/GameViewModel.cs`): compiled bindings (`x:DataType`), commands, a collection for the best scores, data templates, style classes set from bindings (`Classes.lost`, `Classes.latest`) and a transition.
- **Controls the game moves every frame** (`Gameplay/HudOrbs.cs`, `Views/HudView.axaml`): each orb is a view model in a collection that the HUD lays out on a `Canvas`. The game updates their position, size and opacity every frame, and a press on an orb, a round button, runs its command.
- **The game and the UI sharing the input**: a click on a control of the HUD goes to the control, a click anywhere else goes to the game. A menu covers the screen, so the game gets no click while one is shown. While the name text box has the focus, the game doesn't see the keys typed in it.
- **Showing and hiding views**: each view is an entity component the game enables and disables.

## Two scenes, one graphics compositor

The game has two scenes, which play the same game (`Gameplay/OrbHuntScript.cs`, `Gameplay/OrbSwarm.cs`) and draw the orbs differently:

- `PlanetScene`, the default one: the orbs are controls of the HUD (`HudOrbs`), over the 3D scene.
- `OrbFieldScene`: a purely 2D game. The orbs and a field of moving stars are drawn with a sprite batch by a renderer of the graphics compositor (`Gameplay/OrbFieldRenderer.cs`), and the game script reads the clicks itself (`OrbField`).

To play the other scene, change the default scene in the game settings: both scenes use the same graphics compositor. It renders the view of the camera (slot `Main`) with the forward renderer, then the `Orb field` renderer. Each one draws only what its scene has: the planet scene has no orb field, and the camera of the orb field scene sees nothing but the clear color, which is the night sky of the field. The tone map has a fixed exposure, so that color stays the same.

## The game

Orb Hunt is laid out as Game Studio makes a game, and opens in Game Studio (`OrbHunt.slnx`):

- `OrbHunt.Game`: the game library, with its package (`OrbHunt.Game.sdpkg`), its assets, the scripts, the views and the view models. It uses Stride and the plugin as packages (`Stride.Engine`, `Stride.AvaloniaUI`, `Stride.AvaloniaUI.Skia`), plus Avalonia itself for the XAML compiler and the theme.
- `OrbHunt.Windows` and `OrbHunt.Linux`: the executables, which call `OrbHuntGame.Run`.
- `Assets/GameSettings.sdgamesettings`: the default scene, the graphics compositor and the resolution.
- `Assets/PlanetScene.sdscene`: the camera (`OrbitCamera`) with the sun, the stars (a background with the `Stars` cube map), the planet (`Spin`, on a tilted axis), the HUD, the menus and the game (`HudOrbs`).
- `Assets/OrbFieldScene.sdscene`: the camera, the HUD, the menus and the orb field (`OrbField`).
- `Assets/GraphicsCompositor.sdgfxcomp`: the camera's forward renderer, with post-effects, then the `Orb field` renderer.
- `Assets/Planet.sdpromodel`, `Planet Material.sdmat` and `PlanetSurface.sdtex`: the planet, a procedural sphere with a texture.
- `Resources`: the pictures of the textures, made for this sample: the surface of the planet and the cube map of the stars.

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
