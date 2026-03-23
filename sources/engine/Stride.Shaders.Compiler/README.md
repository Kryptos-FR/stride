# Stride.Shaders.Compiler

Shader compilation pipeline for the Stride game engine. Translates SDSL shader mixins into native bytecode for each target graphics API.

- `EffectCompiler` — main entry point for compiling an effect permutation
- `IShaderCompiler` — backend interface with implementations for Direct3D (FXC/DXC), OpenGL (GLSL), and Vulkan (SPIR-V via glslang)
- On-disk and in-memory effect cache to avoid redundant recompilation
- Integrates with the asset build pipeline for offline shader pre-compilation
