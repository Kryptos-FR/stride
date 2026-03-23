# Stride.Graphics

Multi-API graphics abstraction layer for the Stride game engine. Provides a unified API over Direct3D 11, Direct3D 12, Vulkan, OpenGL, and OpenGL ES.

- `GraphicsDevice` — GPU device creation and resource management
- `CommandList` — GPU command recording (draw calls, dispatches, barriers)
- `Texture`, `Buffer` — GPU resource types (vertex, index, constant, structured, unordered-access)
- `PipelineState` — rasterizer, blend, depth-stencil, and input-layout state objects
- `SpriteBatch`, `SpriteFont` — built-in 2D rendering helpers
- `GraphicsOutput` / `SwapChain` — presentation and display management
