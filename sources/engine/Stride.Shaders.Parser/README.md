# Stride.Shaders.Parser

SDSL (Stride Shading Language) parser and semantic analyser for the Stride game engine. Resolves shader class hierarchies and mixin compositions before compilation.

- `ShaderMixinParser` — parses and validates SDSL mixin trees
- `ShaderLinker` — resolves cross-shader references and inheritance chains
- `ShaderNavigation` — symbol lookup for tooling and the Game Studio shader editor
- Produces a fully resolved `ParsingResult` consumed by `Stride.Shaders.Compiler`
