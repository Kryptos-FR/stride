# Stride.FontCompiler

SpriteFont compilation tool for the Stride game engine. Converts system fonts or font files into pre-rasterized sprite font atlases consumed at runtime by `Stride.Graphics`.

- Rasterizes TrueType / OpenType fonts into texture atlases
- Configurable glyph ranges, sizes, anti-aliasing, and SDF (signed distance field) output
- Invoked by the asset build pipeline when compiling `SpriteFontAsset`
