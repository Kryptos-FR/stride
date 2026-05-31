# Phase 2 (Asset Preview) — Implementation Notes & Resume Guide

> Branch: `feature/xplat-roadmap-phase-2`. This document captures the state of the Avalonia asset-preview
> work so it can be resumed without redoing the analysis. Last updated 2026-05-31.

## TL;DR

The editor-layer preview **pipeline is fully wired and works end-to-end**: selecting an asset drives the
preview service, builds the asset, and invokes the asset's renderer on the preview game thread. Two
**engine/native-level** blockers remain before pixels actually appear in the panel:

1. **Runtime shader resolution** — rendering fails with `Shader SpriteBatchShader could not be found`.
2. **Native window embedding** — the SDL preview window appears as a **separate floating top-level window**
   instead of being reparented into the "Asset preview" pane.

Both were observed live on `C:\Projects\Stride\Games\MyGame3` (Stride 4.4.0.1) on Windows/D3D11.

## Pipeline topology (verified)

```
AssetCollection.SelectedAssets.CollectionChanged
  → PreviewViewModel.SelectedAssetsCollectionChanged              (GameStudio.Avalonia/ViewModels/PreviewViewModel.cs)
  → IAssetPreviewService.SetAssetToPreview(asset)
  → GameStudioPreviewService.ChangePreviewAsset (preview game script thread)   (Stride.Editor.Avalonia/Preview/GameStudioPreviewService.cs)
      → IAssetPreview.Initialize → AssetPreview.ProvideView                     (Stride.Editor/Preview/AssetPreview.cs:216)
          → creates the registered IPreviewView (e.g. TexturePreviewView : StridePreviewView)
          → StridePreviewView.UpdateStrideView sets ContentPresenter.Content = builder.GetStrideView() == GameEngineHost
      → PreviewAssetUpdated event
  → PreviewViewModel.PreviewObject = GetCurrentPreviewView()       (marshalled to UI thread via InvokeAsync)
  → AssetPreviewView ContentPresenter binds Preview.PreviewObject  (GameStudio.Avalonia/Views/AssetPreviewView.axaml)
      → StridePreviewView template hosts GameEngineHost (NativeControlHost) → SDL window (EmbeddedGameForm)
```

Preview lifetime is owned by `MainViewModel.Preview` (created/disposed with the session). Rendering is
gated by the "Asset preview" `TabItem.IsSelected` → `PreviewViewModel.IsPreviewVisible` →
`OnShowPreview/OnHidePreview`.

## What this session changed (committed)

- `PreviewViewModel`: blocking `Dispatcher.Invoke` → `InvokeAsync` (the event is raised on the game
  thread; blocking it can deadlock the UI). Added `IsPreviewVisible`.
- Preview ownership moved from `AssetPreviewView` code-behind to `MainViewModel` (session-scoped). The
  code-behind previously disposed/recreated the whole preview game on every tab switch.
- `AssetPreviewView` is now pure declarative binding.
- Agnostic `StrideEditorPlugin`: registers `builderService.Database` (needed by `EditorContentLoader` and
  previews via the throwing `Get<GameStudioDatabase>()`).
- Avalonia `StrideEditorViewPlugin`: guards preview-service construction (clear error if prerequisites
  missing) instead of throwing opaquely.
- `GameStudioBuilderService`: **unblock `WaitForShaders()`** when `CreateSystemShaderBuildSteps` returns
  null — otherwise the preview/thumbnail game thread blocks forever. (Latent; not hit by MyGame3, which
  returns non-null, but a real bug for projects with no system shader assets.)
- `--project <path>` CLI arg (kept as a feature): opens a session on startup, deferred until the main
  window is shown (otherwise the session-load progress dialog has a non-visible owner and throws
  `Cannot show window with non-visible owner`). See `Program.cs` / `App.axaml.cs`.

## Blocker 1 — Runtime shader resolution (`SpriteBatchShader could not be found`)

Evidence (live run, selecting a texture):
```
[Phase2Diag]: CreateSystemShaderBuildSteps returned non-null; AllPackages=8
BuildStep UpdateImportShaderCacheBuildStep was successful.
[Preview]: Preview game thread: waiting for shaders…
[Preview]: Preview game thread: shaders ready, starting PreviewGame.
... (select Skybox texture) ...
[Skybox texture] TextureConvertParameters was successful.   ← asset DATA builds fine
[Preview]: Error: RenderSprite crashed ... Shader SpriteBatchShader could not be found
    at ShaderLoaderBase.LoadExternalBuffer (Stride.Shaders.Compilers/ShaderLoaderBase.cs:67/80)
    at ShaderMixer.EvaluateInheritanceAndCompositions
    at EffectCompiler.Compile
    at EffectSystem.LoadEffect (Stride.Rendering/Rendering/EffectSystem.cs:168)
    at TexturePreview.RenderSprite (Stride.Assets.Editor/Preview/TexturePreview.cs:205)
```

Analysis:
- Asset data compilation works; the failure is the **runtime effect compiler** in the preview game being
  unable to find the **source** of the built-in shader `SpriteBatchShader`.
- `SpriteBatchShader.sdsl` lives in `sources/engine/Stride.Graphics/Shaders/SpriteBatchShader.sdsl`.
- `EditorShadersD3D11.bundle` (precompiled editor shaders, loaded at
  `GameStudioBuilderService.cs:50-54` if present) is **absent** from the build output, so the preview
  relies on runtime compilation, which needs the shader **source** in the compiler's shader-source
  database.
- `GameStudioBuilderService.cs:56-60` explicitly notes the databases are separate: *"Shaders compiled on
  main thread won't actually be visible to MicroThread build engine (contentIndexMap are separate)."* The
  imported system shaders likely land in a different `DatabaseFileProvider` than the one the preview
  `EffectSystem`/`EffectCompiler` consults.

Likely fix directions (pick one, needs engine knowledge):
- (a) Ensure the preview `EffectSystem`'s effect compiler uses a shader-source provider that includes the
  imported system shaders (unify the databases / mount the shader source index the preview game reads).
- (b) Generate and ship `EditorShadersD3D11.bundle` for the dev build
  (`sources/editor/Stride.Assets.Presentation.Wpf/EditorPackage/generate-editor-package.bat`) so
  precompiled editor shaders are available; confirm whether the Avalonia editor build is expected to copy
  it to `/binary/editor/`. Cross-API bundles (Vulkan/Metal/OpenGL) would be future work.

Start at: `EffectSystem.LoadEffect` (Stride.Rendering), `ShaderLoaderBase.LoadExternalBuffer`
(Stride.Shaders.Compilers), and how `GameStudioBuilderService`/`PreviewGame` wire the effect compiler's
shader source database.

## Blocker 2 — Native window embedding (separate floating window)

Symptom: a second top-level window (gray rectangle, black before the device cleared) appears instead of
the preview being pinned inside the "Asset preview" pane.

Analysis:
- `GameEngineHost` (`Stride.Core.Presentation.Avalonia/Controls/GameEngineHost.cs`) was rewritten from the
  WPF `HwndHost` (with explicit `WS_CHILD` + `SetParent` + `SetWindowPos` reparenting, now all removed)
  into a `NativeControlHost` that returns the SDL window handle from `CreateNativeControlCore`.
- The SDL window (`EmbeddedGameForm : GameFormSDL`) is created as its own top-level window. Avalonia's
  `NativeControlHost` does not appear to be reparenting/pinning it (the WPF code also dropped
  `TopLevel = false`, see `GameStudioPreviewService.StrideUIThread`:
  `//gameForm = new EmbeddedGameForm { TopLevel = false, Visible = false };`).
- Needs platform-specific verification: on Windows, confirm Avalonia sets `WS_CHILD` and `SetParent` on the
  returned HWND, that SDL isn't fighting it (SDL owns its window/event loop and may reposition/show it),
  and that the host control's bounds drive the SDL window position/size. The dropped
  `UpdateWindowPosition` logic may need a cross-platform equivalent if Avalonia doesn't track bounds.

This requires iterative **visual** testing (only observable by a human running the editor).

## How to reproduce / resume

```
# Build
dotnet build sources/editor/Stride.GameStudio.Avalonia.Desktop/...csproj -c Debug
# Run against a real project (the --project switch is a kept feature)
.../Stride.GameStudio.Avalonia.Desktop.exe --project C:\Path\To\Game.sln
# Then select a single texture/model asset in the "Asset view" and watch the "Asset preview" tab.
```
Temporary diagnostics used during this session (env var `STRIDE_PHASE2_LOG` + `Phase2Diag`/`[Preview]`
logging) were removed before final commit; re-add similar logging around `GameStudioBuilderService` ctor,
`GameStudioPreviewService.StrideUIThread`, and `ChangePreviewAsset` if needed.

## Status vs Phase 2 "Requires"

- [x] `GameEngineHost` wired to a `PreviewGame` instance (constructs, runs; embedding not yet visible — Blocker 2)
- [x] `GameStudioPreviewService` connected to `StridePreviewView` (selection→render verified)
- [x] Asset preview views registered (thin `StridePreviewView` subclasses per asset type)
- [ ] Pixels rendered in-panel — blocked by Blocker 1 (shaders) and Blocker 2 (embedding)
- [ ] Thumbnail generation pipeline (still `StaticThumbnailService`; real `GameStudioThumbnailService`
      shares the same shader/builder path, so gated by Blocker 1)
