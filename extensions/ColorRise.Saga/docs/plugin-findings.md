# Plugin model findings

What ColorRise.Saga met in Stride's plugin model (engine and cross-platform Game Studio) while being built as a third party.
A finding that blocks a feature leaves the feature out rather than changing Stride; Nicolas decides what goes upstream.

| # | Finding | What Saga tried | What happened | Workaround in Saga | Suggested change |
|---|---|---|---|---|---|
| 1 | A companion project with no `.sdpkg` of its own is packed with the engine's version in its `.sdpkg` | Packed `ColorRise.Saga.Editor` (0.1.0) and `.Editor.Avalonia` without an `.sdpkg` next to the project | The `.nupkg` is 0.1.0 and the runtime records the companions at 0.1.0, but the generated `stride/*.sdpkg` says `Meta.Version: 4.5.3-dev` (and adds `AssetFolders: Assets`) | Each companion project has its own `.sdpkg` with the right version | `PackAssets` could take the package's `$(PackageVersion)` when it makes the `.sdpkg` of a project that has none. Whether a host reads that version was not checked. |
| 2 | Against a local engine build, the extension builds only in the configuration the engine was built in | `dotnet pack` (Release by default) of the runtime, after a Debug engine build | `Stride AssetCompiler could not be found (.../bin/Release/net10.0/Stride.AssetCompiler.dll)`: the dev package stubs point at the engine's build output | Build and pack the extension with `-c Debug` | The dev stubs could fall back to the configuration that exists, or say which one to build in the message. Dev workflow only; real packages are not affected. |
