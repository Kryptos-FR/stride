# Stride.Games

Game loop and window management for the Stride game engine. Provides the host infrastructure that drives each frame of a running game.

- `GameBase` — core game loop (update / draw lifecycle)
- `GameWindow` — cross-platform window creation and management
- `GameContext` / `GamePlatform` — platform-specific initialization (Windows, Linux, Android, iOS, UWP)
- `GameSystemBase` — base class for engine subsystems with ordered initialization and update
- `GameTime` — frame timing and elapsed time tracking
