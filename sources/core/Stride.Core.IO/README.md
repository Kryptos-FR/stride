# Stride.Core.IO

Virtual file system abstraction for the Stride game engine. Provides a unified I/O layer over multiple storage backends.

- `VirtualFileSystem` — mounts and routes file operations across providers
- `IVirtualFileProvider` — pluggable storage backends (disk, ZIP, in-memory)
- `ZipFileSystemProvider` — read assets from compressed archives
- `DirectoryWatcher` — monitors directories for file changes
- Platform-aware path handling and stream utilities
