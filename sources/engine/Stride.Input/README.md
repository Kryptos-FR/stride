# Stride.Input

Cross-platform input handling for the Stride game engine. Abstracts keyboards, mice, gamepads, touch screens, and motion sensors behind a unified API.

- `InputManager` — central input state and event aggregator
- `IKeyboardDevice`, `IMouseDevice`, `IPointerDevice` — typed device interfaces
- `IGamePadDevice`, `GameControllerDeviceBase` — gamepad and generic controller support
- Touch input with multi-touch tracking
- Motion sensors: `AccelerometerSensor`, `GyroscopeSensor`, `CompassSensor`, `UserAccelerationSensor`
- Virtual button mapping for action-based input abstraction
