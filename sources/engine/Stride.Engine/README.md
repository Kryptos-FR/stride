# Stride.Engine

Core game engine assembly implementing the Entity-Component System (ECS) and scene management for Stride.

- `Entity` — game object container identified by GUID, holding an ordered list of components
- `EntityComponent` — base class for all components (`TransformComponent`, `ModelComponent`, `CameraComponent`, `ScriptComponent`, …)
- `EntityProcessor` — systems that track and update entities matching a given component signature
- `Scene` / `SceneInstance` — hierarchical scene graph with runtime lifecycle management
- Scripting model: `SyncScript` (per-frame Update), `AsyncScript` (async/await), `StartupScript` (one-shot init)
- `Game` — top-level entry point tying together all engine systems
