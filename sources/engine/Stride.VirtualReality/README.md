# Stride.VirtualReality

VR and XR device support for the Stride game engine. Provides a unified API over multiple VR runtimes and SDKs.

- Supported runtimes: OpenXR, OpenVR (SteamVR), Oculus OVR, Windows Mixed Reality
- `VRDevice` / `VRDeviceSystem` — device discovery, initialization, and per-frame pose updates  
- Stereo eye rendering with per-eye projection and view matrices
- `TouchController` — 6DOF controller tracking and button state
- `VROverlay` — compositor overlay rendering
