# Stride.Assets

Asset management and game configuration for the Stride game engine. Defines asset types, compilers, and factories used by the Game Studio and asset build pipeline.

- `GameSettingsAsset` — project-wide settings (resolution, physics, rendering quality)
- Material, sprite, font, skybox, and video asset definitions
- Default asset factories powering right-click → New Asset in Game Studio
- Asset compiler implementations that produce binary runtime data from YAML source assets
