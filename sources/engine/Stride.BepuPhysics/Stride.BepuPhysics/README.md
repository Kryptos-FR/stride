# Stride.BepuPhysics

3D rigid body physics for the Stride game engine, powered by the [Bepu physics library](https://github.com/bepu/bepuphysics2).

- `BepuSimulation` — physics world with configurable gravity, substeps, and collision layers
- `BodyComponent` — dynamic or kinematic rigid body attached to an entity
- `StaticComponent` — immovable collision shape (terrain, walls)
- `CollidableComponent` — base for all shapes (box, sphere, capsule, mesh, compound)
- `CharacterComponent` — character controller with step handling and slope limits
- Collision filtering via layers and masks; contact event callbacks
