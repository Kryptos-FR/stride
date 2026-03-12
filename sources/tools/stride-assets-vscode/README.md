# Stride Asset Navigator

Navigate [Stride](https://www.stride3d.net/) game engine asset files (`.sd*`) directly in VS Code with go-to-definition, hover tooltips, broken link detection, and C# script navigation.

## Features

### Asset Navigation (Ctrl+Click)

Click on any asset reference (`GUID:AssetName`) to jump to the referenced asset file. Works across all asset types — materials, textures, models, scenes, prefabs, and more.

### Hover Tooltips

Hover over a reference to see detailed information:

- **Asset references** — type, file path, source file, and asset ID
- **Entity references** (`ref!!`) — entity name, file, and line number
- **Source paths** — file type and existence status
- **Script types** — fully qualified type name, assembly, and whether source is available

### Broken Link Detection

The extension automatically detects broken references in open asset files:

| Reference Type | Severity | Example |
|---|---|---|
| Missing asset | Warning | `Asset not found: MyMaterial (guid)` |
| Missing source file | Error | `Source file not found: ../Textures/foo.png` |
| Asset name mismatch | Info | `Asset name mismatch: file is "X", reference says "Y"` |
| Missing script type | Warning | `Script type not found: MyNamespace.MyScript` |

Diagnostics update in real time as you edit and auto-resolve when broken links are fixed — including when external files (resources, C# scripts) are created or renamed.

### Source Path Navigation

Click on `Source:` paths to open the referenced resource file (textures, models, etc.). Broken paths are highlighted as errors.

### Quick Find

Use the command **Stride: Find Asset by Name** (`Ctrl+Shift+P`) to search and open any indexed asset by name, type, or file path.

### Syntax Highlighting

YAML-based syntax highlighting for all Stride asset file types with support for GUIDs, type tags, and reference patterns.

## Opt-in Features

These features are disabled by default and can be enabled in settings.

### Script Navigation

> Setting: `strideAssets.scriptNavigationEnabled`

Navigate from script/component type references (e.g. `!MyNamespace.MyScript,MyProject`) to their C# source files. Provides:

- **Ctrl+Click** to jump to the C# class definition
- **Hover tooltips** showing type name, assembly, and whether it's a local or framework type
- **Diagnostics** for missing script types (only for types in local `.csproj` projects, not framework/NuGet types)

Requires the [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) extension to be installed and active.

### Back-Links

> Setting: `strideAssets.backLinksEnabled`

Scan all asset files to discover which assets reference each other. Enables:

- **CodeLens** above `Id:` lines showing the number of incoming references
- **Hover tooltips** on `Id:` lines listing the referencing files
- Incremental updates via a file watcher — no need to rebuild manually

This feature scans the entire workspace on activation and may increase memory usage on large projects.

### Full Workspace Scan

> Setting: `strideAssets.scanWorkspaceForBrokenLinks`

Scan all asset files for broken references on startup. Useful for auditing a project. When disabled (default), only open files are checked.

## Commands

| Command | Description |
|---|---|
| `Stride: Rebuild Asset Index` | Clear and rebuild the asset index from scratch |
| `Stride: Find Asset by Name` | Search and open an asset by name |

## How It Works

The extension activates when a workspace contains a `.sdpkg` file (Stride package definition). On activation, it:

1. Discovers `.sdpkg` files and reads their `AssetFolders` to scope scanning
2. Indexes all `.sd*` asset files (reads only the first 500 bytes for the header)
3. Sets up file watchers for incremental index updates
4. Registers language providers for navigation, hover, diagnostics, and links

The asset index maps GUIDs to file paths, asset names, and types. It updates automatically when asset files are created, modified, or deleted.

## Supported File Types

All Stride asset extensions are supported: `.sdscene`, `.sdprefab`, `.sdmat`, `.sdtex`, `.sdm3d`, `.sdanim`, `.sdskel`, `.sdgfxcomp`, `.sdgamesettings`, `.sdpkg`, `.sdfnt`, `.sdsheet`, `.sdsky`, `.sdsnd`, `.sdvid`, `.sdrendertex`, `.sduipage`, `.sduilib`, `.sdnavmesh`, `.sdhmap`, `.sdphy`, and more.
