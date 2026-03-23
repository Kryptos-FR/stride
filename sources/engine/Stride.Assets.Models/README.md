# Stride.Assets.Models

3D model asset import and compilation for the Stride game engine. Handles meshes, skeletons, and animations from common interchange formats.

- `ModelAsset` — 3D mesh asset with LOD and material slot configuration
- `SkeletonAsset` — bone hierarchy for skinned mesh animation
- `AnimationAsset` — keyframe animation clip
- `PrefabModelAsset`, `ProceduralModelAsset` — programmatic mesh generation
- `ImportModelCommand` — underlying importer pipeline (Assimp-based)
