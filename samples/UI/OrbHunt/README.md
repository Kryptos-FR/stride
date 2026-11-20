# Orb Hunt: an Avalonia UI sample

A small game whose menus and HUD are Avalonia views written in XAML, drawn by Stride over the game. Orbs float in front of a planet: click them before they fade, the small ones are worth more, and the red ones cost a life.

To make a game from it, choose **Sample: Avalonia UI game** in the **Samples > UI** group of Game Studio's new project dialog, or run `dotnet new stride-ui-avalonia -n MyGame` once the Stride samples are installed (`dotnet new install Stride.Templates.Samples`).

## Quick start: Avalonia in a Stride game

`Stride.AvaloniaUI` lets a game write its UI with Avalonia: XAML, controls, styles and bindings. Stride draws it over the game. These are the steps this sample follows, in the order a new game needs them.

**1. Reference the packages.** The game library (`OrbHunt.Game.csproj`) references the plugin, its Skia renderer, and Avalonia itself for the XAML compiler and the theme. Avalonia must be the exact version the plugin is built with:

```xml
<PackageReference Include="Stride.AvaloniaUI" Version="..." />
<PackageReference Include="Stride.AvaloniaUI.Skia" Version="..." />
<PackageReference Include="Avalonia" Version="[12.1.1]" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="[12.1.1]" />
<PackageReference Include="Avalonia.Fonts.Inter" Version="[12.1.1]" />
```

**2. Set Avalonia up with the game's application.** This step is optional: without it, the game gets the Fluent theme, dark, with the Inter font. Orb Hunt has its own application (`OrbHuntApp.axaml`), which holds the theme, the colors and the styles the views share. The executable of each platform calls `OrbHuntGame.Run`, which sets Avalonia up on the game thread, before the game runs:

```csharp
var renderer = args.Contains("--native") ? AvaloniaUIRenderer.Native : AvaloniaUIRenderer.Skia;
AppBuilder.Configure<OrbHuntApp>()
    .UseStride(renderer)
    .UseSkiaFallback()
    .WithInterFont()
    .SetupWithoutStarting();

using var game = new OrbHuntGame(renderer);
game.Run();
```

**3. Write a view.** A view is a `UserControl` written in XAML, such as `Views/HudView.axaml`. `x:DataType` names its view model, so that its bindings are compiled:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:OrbHunt.ViewModels"
             x:Class="OrbHunt.Views.HudView"
             x:DataType="vm:GameViewModel">
  <TextBlock Text="{Binding Score}" />
</UserControl>
```

In Game Studio, the `.axaml` files are listed next to their code-behind as **Avalonia XAML** assets, and open in the code editor.

**4. Put the view in a scene.** Add an **Avalonia UI** component to an entity, and set its **View type** to the type of the view, such as `OrbHunt.Views.HudView, OrbHunt.Game`. **Resolution** and **Resolution Stretch** say how the view is scaled to the screen, as for a Stride UI component. When several views are shown, the one with the higher **Draw order** is drawn on top and gets the input first. A script can also show a view made in code, with the component's `Content`.

**5. Give the view its data.** The view is created when its entity enters the scene. A script gives it its view model once it exists (`Gameplay/OrbHuntScript.cs`):

```csharp
if (Hud.Content is { DataContext: null } hud)
    hud.DataContext = ViewModel;
```

**6. Show and hide views.** Each view is an entity component: the game enables it to show the view and disables it to hide it.

**7. Share the input.** A click on a control goes to the control, and a click anywhere else goes to the game. While the UI holds the mouse (the pointer is over one of its controls, or a menu covers the screen), the game's `Input` reports no click. While a text box has the focus, the game doesn't see the keys typed in it. So the game reads the input as any game does, and never sees what is meant for the UI.

## Two scenes: over a 3D scene, or a purely 2D game

The game has two scenes. They play the same game (`Gameplay/OrbHuntScript.cs`, `Gameplay/OrbSwarm.cs`) and draw the orbs in two ways:

- **`PlanetScene`, the default one.** The UI is drawn over a 3D scene: a planet that turns in front of a sky of stars, seen by a camera that circles it. The orbs are controls of the HUD (`Gameplay/HudOrbs.cs`). Each orb is a view model in a collection that the HUD lays out on a `Canvas`, and the game updates its position, size and opacity every frame. A press on an orb, a round button, runs its command.
- **`OrbFieldScene`, a purely 2D game.** The orbs and a field of moving stars are drawn with a sprite batch by a renderer of the graphics compositor (`Gameplay/OrbFieldRenderer.cs`), and the game script reads the clicks itself (`Gameplay/OrbField.cs`). This is the way for a game that is mostly UI.

To play the other scene, change the **Default scene** in the game settings. Both scenes use the same graphics compositor, so nothing else changes. The compositor renders the view of the camera (slot `Main`) with the forward renderer, then the `Orb field` renderer. Each one draws only what its scene has: the planet scene has no orb field, and the camera of the orb field scene sees nothing but the clear color, which is the night sky of the field. The tone map has a fixed exposure, so that this color stays the same.

## Two renderers: Skia or Stride's own

The renderer is chosen once, when Avalonia is set up (`UseStride(renderer)`). The main menu says which renderer draws the UI, and whether on the GPU or the CPU.

- **Skia**, the default here. Skia draws on the GPU on Vulkan, where it shares the game's graphics device. With the other graphics APIs, it draws on the CPU, and the game uploads each frame to a texture.
- **Stride's native renderer**, when the game starts with `--native`. Stride draws the UI itself, on the GPU, with Direct3D 11, Direct3D 12 and Vulkan. It draws shapes and clips itself, and hands what it can't draw yet, such as text, to Skia, which draws it in place. So the game references the Skia package with both renderers (`UseSkiaFallback`).

On Windows, the game uses Direct3D 11. To use another graphics API, set it in `OrbHunt.Windows.csproj`, for example `<StrideGraphicsApi>Vulkan</StrideGraphicsApi>`, or pass `-p:StrideGraphicsApi=Vulkan` to the build.

## The files of the game

Orb Hunt is laid out as Game Studio makes a game:

- `OrbHunt.Game`: the game library, with its package (`OrbHunt.Game.sdpkg`), its assets, the application, the scripts, the views and the view models.
- `OrbHunt.Windows`, `OrbHunt.Linux` and `OrbHunt.macOS`: the executables, which call `OrbHuntGame.Run`.
- `Assets/GameSettings.sdgamesettings`: the default scene, the graphics compositor and the resolution.
- `Assets/PlanetScene.sdscene`: the camera (`OrbitCamera`) with the sun, the stars (a background with the `Stars` cube map), the planet (`Spin`, on a tilted axis), the HUD, the menus and the game (`HudOrbs`).
- `Assets/OrbFieldScene.sdscene`: the camera, the HUD, the menus and the orb field (`OrbField`).
- `Assets/GraphicsCompositor.sdgfxcomp`: the camera's forward renderer, with post-effects, then the `Orb field` renderer.
- `Assets/Planet.sdpromodel`, `Planet Material.sdmat` and `PlanetSurface.sdtex`: the planet, a procedural sphere with a texture.
- `Resources`: the pictures of the textures, made for this sample: the surface of the planet and the cube map of the stars.

## Running it

From the folder of the game:

```bash
# Windows
dotnet run --project OrbHunt.Windows
# Linux
dotnet run --project OrbHunt.Linux
# macOS
dotnet run --project OrbHunt.macOS
```

Add `-- --native` to draw the UI with Stride's native renderer. Esc or P pauses a round.

The executables for iOS and Android that the template can add are the ones of a new game: they don't set Avalonia up with the game's application, so the views don't find its styles there.

### In the Stride repository

The sample references Stride at the version of its last upgrade. To build it in the repository, build the engine first (`Stride.slnx`, which also builds the plugin), then switch the samples to the packages of this checkout, and back before committing:

```bash
dotnet msbuild build/samples.proj -t:SamplesToDevEngine
dotnet msbuild build/samples.proj -t:SamplesToReleaseEngine
```

See [the templates README](../../../sources/templates/README.md) for how the samples become templates.
