# Stride.Shaders

Shader asset definitions for the Stride game engine. Provides the data model for the SDSL (Stride Shading Language) shader asset system.

- `ShaderClassSource` — references a named SDSL shader class with template arguments
- `ShaderMixinSource` — composable shader mixin tree used to build effect permutations
- `EffectBytecode` — compiled shader bytecode paired with reflection metadata
- `EffectReflection` — parameter layout (constant buffers, textures, samplers)
- Used by `Stride.Rendering` to manage and cache shader effect permutations
