# VS Extension Rewrite — Comprehensive Roadmap

## Tooling Reality (Lessons Learned)

### Official Sample Landscape

| Sample | Project Format | TFM | Model | VSIX Creation |
|--------|---------------|-----|-------|---------------|
| TextmateGrammar (VSSDK) | Old-style csproj | net472 | Pure VSSDK | `Microsoft.VSSDK.BuildTools` |
| CodeLensSample | SDK-style csproj | net8.0-windows | Pure Extensibility | `Extensibility.Build` |
| CompositeExtension | SDK-style csproj | net472 | Hybrid | `Extensibility.Build` |
| InProcFeatureGallery | SDK-style csproj | net472 | Hybrid | `Extensibility.Build` |

### What Works and What Doesn't

**SDK-style csproj + Extensibility.Sdk + Extensibility.Build** (hybrid model):
- ✅ VSIX creation (handled by `Extensibility.Build`)
- ✅ Extensibility runtime activation (test command, CodeLens services)
- ✅ `extension.json` generation from `[VisualStudioContribution]` attributes
- ✅ F5 deployment via `ExtensibilityProjectExtension` capability
- ✅ MEF component discovery (content types, file extensions)
- ❌ pkgdef processing — the Extensibility deployment does NOT merge pkgdef into the VS private registry
- ❌ TextMate grammar registration — requires pkgdef, which isn't processed
- ❓ `GeneratePkgDefFile=true` with a Package class — untested in hybrid mode

**SDK-style csproj + Microsoft.VSSDK.BuildTools** (pure VSSDK):
- ❌ VSIX creation broken — `CreateVsixContainer` and `GeneratePkgDef` targets don't chain into the SDK-style `Build` target
- ❌ Requires manual `<Import>` of `Microsoft.VsSDK.targets` and path fixups
- ❌ The official TextmateGrammar sample uses OLD-style csproj, not SDK-style
- ⚠️ The VSSDK build tooling was designed for old-style projects; SDK-style support is incomplete

**Old-style csproj + Microsoft.VSSDK.BuildTools** (pure VSSDK, legacy):
- ✅ Everything works (VSIX, pkgdef, F5, TextMate)
- ❌ Requires `packages.config` instead of `PackageReference`
- ❌ Can't use `[VisualStudioContribution]` or Extensibility SDK features
- ❌ Verbose, hard to maintain

### Deployment Mechanisms

Two separate deployment mechanisms exist in VS, and they don't fully cooperate:

| Mechanism | Triggered By | Deploys To | Processes |
|-----------|-------------|------------|-----------|
| **VSSDK** | `DeployExtension=true` | `stride3d\Stride Asset Navigator\0.1.0\` | pkgdef → private registry, MEF |
| **Extensibility** | `ExtensibilityProjectExtension` capability | Random dir (e.g., `rb1dkbif.gr2\`) | `extension.json` → metadata cache |

With hybrid (`VssdkCompatibleExtension=true`):
- `Extensibility.Build` sets `DeployExtension=false` (disables VSSDK deployment)
- Only the Extensibility deployment runs
- pkgdef files are deployed (copied to the directory) but NOT processed (not merged into registry)
- `extension.json` IS deployed inside `.vsextension/` and IS processed

**Key insight**: Having BOTH deployments active causes duplicate VSIX ID conflicts. The Extensibility.Build explicitly disables VSSDK deployment to prevent this. But this means pkgdef-dependent features (TextMate) don't work out of the box.

### Extensibility Runtime Activation

The Extensibility runtime only activates when there are concrete contributions to discover:
- ✅ A `[VisualStudioContribution]` command with a menu placement → runtime activates
- ❌ Only CodeLens services (no commands) → runtime does NOT activate
- The empty `"commands": []` in `extension.json` is not enough

**Proven**: Adding a `TestCommand` with `CommandPlacement.KnownPlacements.ToolsMenu` caused the runtime to activate and `InitializeServices` to be called.

### Content Type Base Definition

| Base Definition | Constant | TextMate? | LSP? | Notes |
|----------------|----------|-----------|------|-------|
| `"code"` | — | Yes (if grammar registered) | No | Standard code editor. |
| `"code-languageserver-base"` | `CodeRemoteBaseTypeName` | **No** | Yes | **Suppresses TextMate.** LSP only. |
| `"code-languageserver"` | `CodeRemoteContentTypeName` | **Yes** | Yes | Both TextMate and LSP. Does NOT require ILanguageClient. |

**Always use `CodeRemoteContentDefinition.CodeRemoteContentTypeName`** (`"code-languageserver"`) for Stride asset content types. This enables TextMate colorization and also supports LSP if added later. Available via `Microsoft.VisualStudio.SDK` (no separate `LanguageServer.Client` package needed).

**Critical lesson**: The two constants `CodeRemoteContentTypeName` and `CodeRemoteBaseTypeName` have very similar names but opposite TextMate behavior. We wasted hours using `"code-languageserver-base"` (the wrong one) which suppresses TextMate.

### TextMate Grammar Registration

Requires two pkgdef entries:
```
[$RootKey$\TextMate\Repositories]
"StrideAssets"="$PackageFolder$\TextMate"

[$RootKey$\TextMate\LanguageMapping\ContentTypeMapping]
"stride-asset"="source.stride-asset"
```

Generated automatically from `RegistrationAttribute` subclasses on an `AsyncPackage`:
- `ProvideTextMateRepositoryAttribute` — maps an ID to a grammar directory
- `ProvideTextMateContentTypeMappingAttribute` — maps a content type name to a TextMate scope

The grammar JSON file needs a `fileTypes` array and `scopeName: "source.stride-asset"`.

### CodeLens Architecture (Official Sample vs Our Approach)

**Official CodeLensSample** (pure Extensibility, out-of-proc):
- `ICodeLensProvider.TryCreateCodeLensAsync(CodeElement, ...)` — receives VS-provided code elements
- Filters by `CodeElementKind.KnownValues.Method` (built-in kinds)
- NO custom tagger — VS provides the code elements

**Our previous approach** (hybrid, in-proc):
- Custom `ITextViewTaggerProvider<CodeLensTag>` creates `CodeLensTag` for `Id: GUID` lines
- Custom `ICodeLensProvider` matches tags by `codeElement.Kind == AssetIdKind`
- This approach requires the tagger to create custom code elements, which is a non-standard pattern

**Decision for Phase 6**: Follow the official sample pattern. Investigate whether VS provides code elements for custom content types, or if we need to define custom code element kinds. If the tagger approach is needed, it should follow the InProcFeatureGallery patterns.

### Stale Cache / Registry Issues

Switching between deployment modes or VSIX Identity IDs causes persistent problems:

| Cache | Location | When to Clear |
|-------|----------|---------------|
| ComponentModelCache | `%LOCALAPPDATA%\...\18.0_2b202606Exp\ComponentModelCache\` | After changing MEF exports |
| Private registry | `%LOCALAPPDATA%\...\18.0_2b202606Exp\privateregistry.bin` | After changing pkgdef content (may need VS closed) |
| Extension metadata | `%LOCALAPPDATA%\...\18.0_2b202606Exp\Extensions\ExtensionMetadata*.mpack` | After changing extension.json |
| Deployed extensions | `%LOCALAPPDATA%\...\18.0_2b202606Exp\Extensions\<random-dir>\` | When seeing stale deployments |
| Diagnostic log | `%TEMP%\stride-vs-extension.log` | Before each test run |

**Never change the VSIX Identity Id** — this creates a second extension instead of replacing the first.

## Roadmap

### Phase 0: Extension Shell + Syntax Highlighting ✅ DONE

**Goal**: `.sd*` files open with TextMate syntax coloring. Extension loads in VS.

**Approach**: Hybrid Extensibility format (proven working for runtime activation) + `GeneratePkgDefFile=true` + minimal Package class for TextMate pkgdef.

**csproj pattern** (matches CompositeExtension sample + pkgdef generation):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <VssdkCompatibleExtension>true</VssdkCompatibleExtension>
    <GeneratePkgDefFile>true</GeneratePkgDefFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.VisualStudio.Extensibility.Sdk" Version="17.14.40608" PrivateAssets="all" />
    <PackageReference Include="Microsoft.VisualStudio.Extensibility.Build" Version="17.14.40608" PrivateAssets="all" />
    <PackageReference Include="Microsoft.VisualStudio.SDK" Version="17.6.36389" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

**vsixmanifest pattern** (hybrid, with VsPackage asset for pkgdef):
```xml
<Installation ExtensionType="VSSDK+VisualStudio.Extensibility">
<Assets>
  <Asset Type="Microsoft.VisualStudio.VsPackage" d:Source="Project"
         d:ProjectName="%CurrentProject%" Path="|%CurrentProject%;PkgdefProjectOutputGroup|" />
  <Asset Type="Microsoft.VisualStudio.MefComponent" d:Source="Project"
         d:ProjectName="%CurrentProject%" Path="|%CurrentProject%|" />
</Assets>
```

**Files**:
| File | Purpose |
|------|---------|
| `StrideAssets.VisualStudio.csproj` | Hybrid Extensibility + pkgdef |
| `source.extension.vsixmanifest` | VSSDK+Extensibility, VsPackage + MefComponent assets |
| `StrideExtension.cs` | Extensibility entry point (`Extension` class) |
| `TestCommand.cs` | Minimal command to ensure Extensibility runtime activates |
| `.vsextension/string-resources.json` | Localized string for test command |
| `StrideAssetsPackage.cs` | Minimal AsyncPackage + TextMate RegistrationAttributes |
| `ContentTypeDefinitions.cs` | MEF content type `"stride-asset"` + 28 file extension mappings |
| `OutputLogger.cs` | VS Output pane logging |
| `TextMate/stride-asset.tmLanguage.json` | Syntax highlighting grammar |

**Open question**: Will `GeneratePkgDefFile=true` with an AsyncPackage work in the hybrid deployment mode? The pkgdef will be generated and included in the VSIX, but the Extensibility deployment may not process it into the private registry. If this doesn't work, we need to investigate how to force pkgdef processing.

**Test**:
1. F5 → VS Exp launches
2. Check `%TEMP%\stride-vs-extension.log` for "InitializeServices called"
3. Tools menu → "Stride: Test Extension" command exists
4. Open `.sdscene` file → syntax coloring visible

### Phase 1: Asset Parsing Engine ✅ DONE

**Goal**: In-memory index of all Stride assets, with live updates.

**New files**:
- `AssetParser.cs` — regex extraction of Id, references, parts, type refs from one file
- `AssetIndex.cs` — thread-safe dictionaries: guid→asset, guid→part, guid→backRefs, with reverse file indexes
- `WorkspaceScanner.cs` — recursive file discovery, `FileSystemWatcher` (64 KB buffer)

**Integration**: `StrideAssetsPackage.InitializeAsync` → `Log.Initialize` → `IVsSolution.GetSolutionInfo` → `JoinableTaskFactory.RunAsync` + `FileAndForget` for background scan.

**Key design decisions**:
- `[GeneratedRegex]` not available in net472 → use `new Regex(..., RegexOptions.Compiled)`
- `lock` not `SemaphoreSlim` → index operations are purely synchronous, no await inside lock
- `FileSystemWatcher` not `IVsFileChangeEx` → simpler, no COM; buffer doubled to 64 KB to reduce missed events
- Fire-and-forget via `JoinableTaskFactory.RunAsync(...).FileAndForget(...)` → VS tracks the task for shutdown coordination and surfaces exceptions

**Regex patterns** (ported from TypeScript referencePattern.ts):
```
Id line:    ^\s*Id:\s+([0-9a-fA-F]{8}-...-[0-9a-fA-F]{12})
Asset ref:  (GUID):([^\s,}\]]+(?:\s+[^\s,}\]]+)*)
Type tag:   ^!([A-Za-z_][\w.]*(?:,\s*[A-Za-z_][\w.]*)?)
Source:     ^\s*Source:\s+(.+)
```

**Test**: Output pane → "Indexed N assets, M parts, K back-references".

### Phase 2: Hover Tooltips ✅ DONE

**Goal**: Hover on GUID references shows asset info.

- `IAsyncQuickInfoSourceProvider` + `IAsyncQuickInfoSource` (MEF, VSSDK)
- Detect context at cursor position (GUID ref, type ref, Id line)
- Look up in AssetIndex
- Return VS-native rich tooltip (`ClassifiedTextRun`, `ContainerElement`)

**Test**: Hover on `GUID:AssetName` → tooltip with asset type, name, file path.

### Phase 3: Go-to-Definition ✅ DONE

**Goal**: F12 and Ctrl+click on GUID references navigate to the target asset or entity.

- `ITextViewCreationListener` + `IOleCommandTarget` filter for F12 (`GotoDefn` command)
- `IMouseProcessorProvider` + `MouseProcessorBase` for Ctrl+click (`PostprocessMouseLeftButtonUp`)
- Shared `AssetNavigation` helper: GUID regex, `TryNavigate`, `NavigateTo` (`VsShellUtilities.OpenDocument` + `SetCaretPos`)
- Top-level assets → open file at line 0; entity parts → jump to exact `Id:` line
- WPF assemblies added to csproj: `PresentationCore`, `PresentationFramework`, `System.Xaml`, `WindowsBase`

**Bonus**: Ctrl+hover shows underline + hand cursor (`ITagger<ClassificationTag>` + `HoverLinkState` per-buffer, lazily wired via `ITextBuffer.Properties`).

**Future**: Peek Definition (Alt+F12) via `IPeekableItemSourceProvider` — deferred, more complex.

**Test**: F12 or Ctrl+click on `GUID:AssetName` → jumps to target file at `Id:` line.

### Phase 4: Script Navigation (Roslyn) ✅ DONE

**Goal**: F12 and Ctrl+click on `!Namespace.Type,Assembly` jump to C# source.

**New files**:
- `CSharpNavigation.cs` — `TypeTagRegex`, `TryNavigate`, `ResolveAndNavigate` via `IComponentModel → VisualStudioWorkspace → TryGetCompilation → GetTypeByMetadataName`

**Modified files**:
- `AssetGoToDefinitionSource.cs` — `AssetNavigation.TryNavigate` delegates to `CSharpNavigation.TryNavigate` first; `AssetMouseProcessor.UpdateHover` also underlines type tags on Ctrl+hover
- `StrideAssets.VisualStudio.csproj` — added `Microsoft.VisualStudio.LanguageServices 4.6.0`

**Key design decisions**:
- `TryGetCompilation` — synchronous cached compilation, no await needed; gracefully skips if not yet compiled
- Type tags checked before GUIDs in `TryNavigate` (`!` prefix makes them unambiguous)
- Ctrl+hover underlines all `!TypeName` tags regardless of whether Roslyn has resolved them (avoids false negatives on uncompiled projects)

**Test**: F12 or Ctrl+click on `!SpaceEscape.CharacterScript,SpaceEscape.Game` → opens `CharacterScript.cs` at class declaration.

### Phase 5: Diagnostics

**Goal**: Squiggly underlines on broken asset references.

- `ITaggerProvider<IErrorTag>` (MEF, VSSDK)
- Scan document for GUID references, check against AssetIndex
- Debounce on document changes

**Test**: Reference to non-existent GUID → yellow warning squiggle.

### Phase 6: CodeLens

**Goal**: "N references" above `Id:` lines.

Follow the official [CodeLensSample](https://github.com/microsoft/VSExtensibility/tree/main/New_Extensibility_Model/Samples/CodeLensSample) pattern:
- `ICodeLensProvider` (Extensibility API, already available in hybrid mode)
- Investigate how to provide custom code elements for non-standard file types
- Back-reference counting from AssetIndex

**Test**: `Id: GUID` lines show "N references" CodeLens above them.

### Phase 7: Settings

**Goal**: User-configurable feature toggles.

- `DialogPage` (Tools > Options > Stride > Asset Navigator)
- 4 boolean toggles: diagnostics, scriptNav, backLinks, scanWorkspace
- Wire to enable/disable features at runtime

### Phase 8: Polish & Testing

- Test with SpaceEscape sample project
- Test with large Stride projects (performance)
- Error handling and graceful degradation
- Remove `TestCommand.cs` once CodeLens works independently
- Update documentation

## Build & Debug

**Build** (requires VS 2026 MSBuild):
```bash
"C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" \
    sources/tools/stride-assets-vs/StrideAssets.VisualStudio.csproj /t:Build /p:Configuration=Debug
```

**Debug**: F5 launches experimental VS instance.

**Cache clearing** (run with VS Exp closed):
```bash
# ComponentModelCache (MEF)
rm -rf "%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_2b202606Exp\ComponentModelCache"

# Deployed extensions
# Find and delete directories containing StrideAssets.VisualStudio.dll

# Extensibility metadata
rm -f "%LOCALAPPDATA%\...\Extensions\ExtensionMetadata2.0.mpack"
rm -f "%LOCALAPPDATA%\...\Extensions\ExtensionMetadataCache.mpack"

# Diagnostic log
rm -f "%TEMP%\stride-vs-extension.log"
```
