# Existing Asset Editors

## Overview

The editor view models live in the toolkit-neutral `Stride.Assets.Editor` (`sources/editor/Stride.Assets.Editor/ViewModels/`), shared by both Game Studios. Each toolkit adds its views: `Stride.Assets.Editor.Wpf` (`AssetEditors/<EditorName>/Views/`) and `Stride.Assets.Editor.Avalonia` (`Views/`). The editors of plugin assets live in the plugin's `Editor` package: the UI page and library editors are in `Stride.UI.Editor` (view models), `Stride.UI.Editor.Wpf` and `Stride.UI.Editor.Avalonia` (views), under `sources/plugins/UI/`. Two editors are still WPF only, view models included: the Roslyn script editor and the visual script editor, in `Stride.Assets.Editor.Wpf/AssetEditors/`; the cross-platform editor has its own script editor and a placeholder for visual scripts. The table below is the entry point for locating any existing editor.

## Editors

| Editor | Asset type | Base class | Game viewport | View model | Views |
|---|---|---|---|---|---|
| `SpriteSheetEditorViewModel` | `SpriteSheetAsset` | `AssetEditorViewModel<SpriteSheetViewModel>` | No | `Stride.Assets.Editor` | WPF, Avalonia |
| `SceneEditorViewModel` | `SceneAsset` | `EntityHierarchyEditorViewModel` | Yes | `Stride.Assets.Editor` | WPF, Avalonia |
| `PrefabEditorViewModel` | `PrefabAsset` | `EntityHierarchyEditorViewModel` | Yes | `Stride.Assets.Editor` | WPF, Avalonia |
| `UIPageEditorViewModel` | `UIPageAsset` | `UIEditorBaseViewModel` | Yes | `Stride.UI.Editor` | WPF, Avalonia |
| `UILibraryEditorViewModel` | `UILibraryAsset` | `UIEditorBaseViewModel` | Yes | `Stride.UI.Editor` | WPF, Avalonia |
| `GraphicsCompositorEditorViewModel` | `GraphicsCompositorAsset` | `AssetEditorViewModel<GraphicsCompositorViewModel>` | No | `Stride.Assets.Editor` | WPF, Avalonia |
| `ScriptEditorViewModel` | Script assets | `AssetEditorViewModel` | No | `Stride.Assets.Editor.Wpf` (Roslyn) | WPF |
| `ScriptSourceFileEditorViewModel` | Script assets | `AssetEditorViewModel<ScriptSourceFileViewModel>` | No | `Stride.Assets.Editor` | Avalonia (text editor with C# highlighting, no Roslyn features) |
| `VisualScriptEditorViewModel` | `VisualScriptAsset` | `AssetEditorViewModel` | No | `Stride.Assets.Editor.Wpf` | WPF; Avalonia: placeholder that says visual scripts are not supported |

The curve editor is not an asset editor but a panel that edits the curves of a property (`EditCurveCommand` in the property grid): its view models are in `Stride.Assets.Editor/ViewModels/CurveEditor/` (`CurveEditorViewModel`), its views in `Stride.Assets.Editor.Wpf/CurveEditor/Views/` and `Stride.Assets.Editor.Avalonia/Views/CurveEditor/`.

### SpriteSheetEditorViewModel

**What it does:** Lets the user define sprites within a texture — regions, pivot points, borders, and animation frames. Renders a preview using its own lightweight `ViewportViewModel` rather than a full game instance.

**Key types:**

| Class | Role |
|---|---|
| `SpriteSheetEditorViewModel` | Editor ViewModel (`Stride.Assets.Editor/ViewModels/`) |
| `SpriteEditorView` / `SpriteSheetEditorView` | WPF view / Avalonia view |
| `ViewportViewModel` | Zoom and scroll of the image (`ViewModels/SpriteEditor/`), no game loop |

**Notable:**
- Uses `ViewportViewModel` and the view's image instead of `IEditorGameController` — lighter than a full game editor.
- Implements `IAddChildViewModel` to support dragging textures onto the editor to add new sprites.

### SceneEditorViewModel / PrefabEditorViewModel

**What it does:** Full 3D scene and prefab editing with an entity hierarchy tree, transform gizmos, camera controls, and a live game viewport. Scene and prefab share the same base infrastructure with thin concrete subclasses.

**Key types:**

| Class | Role |
|---|---|
| `EntityHierarchyEditorViewModel` | Shared base ViewModel (`Stride.Assets.Editor/ViewModels/`) |
| `SceneEditorViewModel` | Thin subclass for scenes |
| `PrefabEditorViewModel` | Thin subclass for prefabs |
| `EntityViewModel` | Part ViewModel for each entity in the hierarchy (`Stride.Assets.Presentation/ViewModels/`) |
| `EditorGameHost` | The editor game of the view (`Stride.Assets.Editor/EditorGame/`), exposed as `IGameEditorViewModel.GameHost` |
| `EditorViewToolbarViewModel`, `EditorViewGizmosViewModel`, `EditorViewOverlaysViewModel`, `SceneViewWidgetsViewModel` | The scene view's toolbar, gizmo, overlay and widget options of the cross-platform editor |
| `EditorViewLayoutViewModel` | The layout of the scene view's viewports (one or several views of the same game, saved per scene in `SceneViewsSettingsData`) |
| `EditorCameraViewModel` | Camera options (`Stride.Assets.Editor/AssetEditors/GameEditor/ViewModels/`) |
| `EntityPickerViewModel` | The entity and component picker of the property grid's Pick buttons (`IEntityPickerService`) |
| `EntityGizmosViewModel` | The WPF gizmo overlay (`Stride.Assets.Editor.Wpf/AssetEditors/`) |
| `EntityHierarchyEditorView` | Base view: WPF (abstract, with `SceneEditorView` / `PrefabEditorView`) and Avalonia (one view for both) |

**Notable:**
- Most logic is in `EntityHierarchyEditorViewModel`; the concrete subclasses are thin.
- Scene and prefab differ primarily in which hierarchy root they load and whether archetype (prefab base) linking is active.
- The WPF view code-behind injects the game host into the XAML panel: `SceneView.Content = hierarchyEditor.Controller.EditorHost`. The Avalonia view shows `GameHost.GameView`, an offscreen game view (`Stride.Editor.Avalonia/Engine/`), in an `EditorViewLayoutPanel`, which can split it into several views of the same game (`IEditorGameMultiViewport`, `Stride.Editor/Engine/`).

### UIPageEditorViewModel / UILibraryEditorViewModel

**What it does:** WYSIWYG editing of UI hierarchies — pages (full-screen layouts) and libraries (reusable component collections). Renders elements in a live game viewport with selection adorners, resize handles, and snap guidelines.

**Key types:**

| Class | Role |
|---|---|
| `UIEditorBaseViewModel` | Shared base ViewModel (`Stride.UI.Editor/ViewModels/`), an `AssetCompositeHierarchyEditorViewModel` and `IGameEditorViewModel` |
| `UIPageEditorViewModel` | Subclass for UI pages |
| `UILibraryEditorViewModel` | Subclass for UI libraries |
| `UIElementViewModel` | Part ViewModel for each UI element |
| `UIEditorView` | Abstract base view (`Views/` of `Stride.UI.Editor.Wpf` and of `Stride.UI.Editor.Avalonia`) |
| `UIPageEditorView` / `UILibraryEditorView` | Concrete views, one per toolkit |

**Notable:**
- The adorners (guidelines, resize handles) are drawn in the game (`Stride.UI.Editor/Adorners/`), not by the toolkit, so both views share them.
- `UIEditorBaseViewModel` handles element factories, zoom/pan state, and selection; subclasses add only asset-type-specific root handling.

### GraphicsCompositorEditorViewModel

**What it does:** Visual node-graph editor for the render pipeline. Nodes represent render features and render stages; edges represent data flow between them.

**Key types:**

| Class | Role |
|---|---|
| `GraphicsCompositorEditorViewModel` | Editor ViewModel (`Stride.Assets.Editor/ViewModels/`, its blocks in `ViewModels/GraphicsCompositorEditor/`) |
| `GraphicsCompositorEditorView` | XAML view |
| `SharedRendererFactoryViewModel` | Factory/list for shared renderer blocks |
| `RenderStageViewModel` | Node ViewModel for render stages |

**Notable:**
- The WPF view uses `Stride.Core.Presentation.Wpf.Graph` (GraphX) for the node-graph canvas. The Avalonia view uses the node graph control of `Stride.Core.Presentation.Avalonia` (`Controls/NodeGraph/`, with a tree layout), fed by `GraphicsCompositorGraph`.
- Does **not** use `IEditorGameController` — no live game instance; the compositor is a pure data-structure editor.

### ScriptEditorViewModel

**What it does:** Opens a script asset in an embedded code editor. Provides compilation feedback and basic IDE integration within GameStudio.

**Key types:**

| Class | Role |
|---|---|
| `ScriptEditorViewModel` | WPF editor ViewModel (`Stride.Assets.Editor.Wpf/AssetEditors/ScriptEditor/`), with RoslynPad |
| `ScriptSourceFileEditorViewModel` | Cross-platform editor ViewModel (`Stride.Assets.Editor/ViewModels/`): the file's text, saved by the editor; the Avalonia view uses AvaloniaEdit with TextMate C# highlighting |
| `ScriptEditorView` / `ScriptSourceFileEditorView` | WPF view / Avalonia view |

**Notable:**
- The lightest editor — mostly a shell around the embedded code editor control.
- Undo/redo is delegated to the code editor itself rather than `IUndoRedoService`; the standard transaction infrastructure does not apply here.

### VisualScriptEditorViewModel

**What it does:** Node-graph editor for visual scripting. Blocks represent operations; edges represent data and control flow between them.

**Key types:**

| Class | Role |
|---|---|
| `VisualScriptEditorViewModel` | Editor ViewModel (`Stride.Assets.Editor.Wpf/AssetEditors/VisualScriptEditor/`) |
| `VisualScriptMethodEditorViewModel` | Per-method graph editing |
| `VisualScriptBlockViewModel` | Node ViewModel for each block |
| `VisualScriptLinkViewModel` | Edge ViewModel for each connection |

**Notable:**
- Similar to `GraphicsCompositorEditorViewModel` in structure: a pure data-structure editor with no game instance.
- ViewModel files sit at the folder root (no `ViewModels/` subdir); views are in `Views/` as usual.
- WPF only. Visual scripting is not planned for the cross-platform editor: `Stride.Assets.Editor` has an empty `VisualScriptEditorViewModel` with an Avalonia view that says visual scripts are not supported, so the asset still opens.

## Shared Game Editor Infrastructure

All game-viewport editors (Scene, Prefab, UIPage, UILibrary) implement `IGameEditorViewModel` (its `GameHost` is the editor game shown by the view) and run a game instance via `IEditorGameController` (`Stride.Assets.Editor/AssetEditors/GameEditor/Services/`; base implementation `EditorGameController<TEditorGame>` in `Stride.Assets.Editor/EditorGame/`). The controller manages the game loop on a background thread; results are marshalled back to the ViewModel via `IDispatcherService`.

**Key implication for contributors:** property changes in these editors can originate from either the UI thread (user interaction) or the game thread (simulation update). Code that modifies ViewModel state from the game thread must dispatch to the UI thread via `Asset.Dispatcher` (`AssetEditorViewModel.Asset` inherits `Dispatcher` from `DispatcherViewModel`):

```csharp
Asset.Dispatcher.InvokeAsync(() =>
{
    // safe to update ViewModel properties here
});
```
