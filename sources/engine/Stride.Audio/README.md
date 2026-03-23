# Stride.Audio

Cross-platform audio engine for Stride. Supports 2D and 3D spatial audio playback on Windows, Linux, Android, and iOS.

- `AudioEngine` — device lifecycle management and master volume control
- `Sound` / `SoundInstance` — audio asset and stateful playback handle
- `AudioListener` / `AudioEmitter` — 3D positional audio with distance attenuation and Doppler
- Streaming audio for large files (`StreamedBufferSound`)
- Microphone input capture
