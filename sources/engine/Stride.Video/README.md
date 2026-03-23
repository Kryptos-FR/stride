# Stride.Video

Video playback for the Stride game engine. Decodes video files and exposes each frame as a GPU texture for use in the rendering pipeline.

- `VideoComponent` — entity component that attaches a video to a scene object
- `Video` / `VideoInstance` — asset and runtime playback state
- `VideoTexture` — decoded frame as a `Texture` for use in materials
- `VideoSystem` / `VideoProcessor` — engine subsystem coordinating decode and upload
- FFmpeg backend (desktop) and MediaCodec backend (Android)
