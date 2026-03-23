# Stride.Rendering

High-level rendering pipeline for the Stride game engine. Sits above the graphics API and orchestrates scene rendering through composable render features.

- `RenderFeature` — modular rendering step (geometry, lighting, shadows, post-processing)
- `RenderPipeline` / `RenderView` — configurable per-camera rendering setup
- Graphics compositor system for layered scene and UI rendering
- Built-in features: forward lighting, shadows, environment probes, post-effects (bloom, depth of field, tone mapping)
- Render mesh sorting, culling, and batching infrastructure
