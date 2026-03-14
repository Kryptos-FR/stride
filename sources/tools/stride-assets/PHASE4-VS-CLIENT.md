# Phase 4: Visual Studio Client

## Current Status

The VS extension (`sources/tools/stride-assets-vs/`) is functional with the following features:

### Working
- **Syntax highlighting** via TextMate grammar (registered through pkgdef)
- **LSP server connection** via stdio transport (`node server.js --stdio`)
- **Go-to-definition** for entity references within files
- **Hover tooltips** (plaintext only — see Known Limitations)
- **Diagnostics** for broken asset references
- **C# symbol resolution** via Roslyn workspace (`stride/resolveCSharpSymbol`)
- **Script navigation** (Ctrl+click on `!Namespace.Type,Assembly` references to jump to C# source)
- **Settings** via Tools > Options > Stride > Asset Navigator (push-based, live updates)
- **Output logging** to a dedicated "Stride Asset Navigator" output pane
- **Debug logging** via `Log.Debug()` — stripped from Release builds via `[Conditional("DEBUG")]`

### Not Yet Working (In Progress)
- **CodeLens** showing reference counts above `Id:` lines — implementation complete (VisualStudio.Extensibility tagger + provider + BackReferenceScanner), but the Extensibility runtime does not activate reliably. See Troubleshooting below.
- **Back-links** — depends on CodeLens infrastructure working

## Architecture

```
Visual Studio (C# VSIX) — sources/tools/stride-assets-vs/
  ├── VSSDK Layer (ILanguageClient + MEF)
  │     ├── StrideLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
  │     │     ├── Starts Node.js server process (stdio, --stdio flag required)
  │     │     ├── PushSettingsToServer: pushes settings via workspace/didChangeConfiguration
  │     │     ├── SettingsMiddleLayer: strips markdown from textDocument/hover responses
  │     │     ├── CSharpSymbolHandler handles stride/resolveCSharpSymbol via Roslyn
  │     │     └── FindServerPath: env var → VSIX dir → extension dir walk → solution dir walk
  │     ├── StrideSettings: static settings holder with Changed event for live push
  │     ├── ContentTypeDefinitions: "stride-asset" (BaseDefinition: CodeRemoteContentDefinition)
  │     └── StrideAssetsPackage: TextMate grammar registration
  │
  └── VisualStudio.Extensibility Layer (CodeLens)
        ├── StrideCodeLensTaggerProvider: tags Id: GUID lines as code elements
        ├── StrideCodeLensTagger: produces CodeLensTag for each Id: line
        ├── StrideCodeLensProvider: creates CodeLens for tagged elements
        ├── StrideReferencesCodeLens: shows "N references" label
        └── BackReferenceScanner: C# port of backLinkScanner.ts (file scanning)
```

## Key Files

| File | Purpose |
|------|---------|
| `StrideLanguageClient.cs` | ILanguageClient + ILanguageClientCustomMessage2 implementation |
| `ContentTypeDefinitions.cs` | MEF content type + 27 file extension mappings |
| `CSharpSymbolHandler.cs` | Roslyn-based C# symbol resolution (JsonRpcMethod) |
| `StrideAssetsPackage.cs` | AsyncPackage + TextMate registration attributes |
| `StrideOptionsPage.cs` | Tools > Options dialog page + StrideSettings (static holder with Changed event) |
| `OutputLogger.cs` | Thread-safe VS Output window pane logging |
| `StrideExtension.cs` | VisualStudio.Extensibility entry point (in-proc) |
| `CodeLens/StrideCodeLensTaggerProvider.cs` | Tags Id: lines as CodeLens code elements |
| `CodeLens/StrideCodeLensTagger.cs` | Produces CodeLensTag for each Id: GUID line |
| `CodeLens/StrideCodeLensProvider.cs` | Creates CodeLens indicators for tagged elements |
| `CodeLens/StrideReferencesCodeLens.cs` | Shows "N references" label with tooltip details |
| `CodeLens/BackReferenceScanner.cs` | C# port of backLinkScanner.ts (workspace scanning) |
| `TextMate/stride-asset.tmLanguage.json` | TextMate grammar (shared with VS Code, plus fileTypes array) |

## Build & Debug

**Build** (requires VS 2026 MSBuild for .NET SDK 10.0):
```bash
"C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" \
    sources/tools/stride-assets-vs/StrideAssets.VisualStudio.csproj /t:Build /p:Configuration=Debug
```

**Debug**: F5 launches experimental VS instance (`/rootsuffix Exp /log`).

**Important**: After changes to MEF exports (content types, ILanguageClient), delete the MEF cache:
```
%LOCALAPPDATA%\Microsoft\VisualStudio\<version>Exp\ComponentModelCache\
```

## Troubleshooting

### Extension appears installed but inactive
If the extension shows in Extension Manager but nothing works (no output pane, no syntax highlighting):
1. **Clear the ComponentModelCache** — MEF caches assembly paths. If the deployment directory changed (VS uses random directory names like `tkajtqo4.rzb`), the cache points to a stale path and silently skips all MEF exports.
   ```
   Delete: %LOCALAPPDATA%\Microsoft\VisualStudio\<version>Exp\ComponentModelCache\
   ```
2. **Restart VS** — the cache is rebuilt on next startup.

### Extensibility runtime not activating (CodeLens not working)
If `%TEMP%\stride-vs-extension.log` is empty or `InitializeServices` never appears:
1. Check for duplicate extension deployments in `%LOCALAPPDATA%\...\Extensions\`. There should be exactly ONE directory containing `StrideAssets.VisualStudio.dll`.
2. Verify `DeployExtension` is NOT set to `true` in the csproj. The `Microsoft.VisualStudio.Extensibility.Build` package handles deployment via the `ExtensibilityProjectExtension` project capability. Setting `DeployExtension=true` causes VSSDK to create a second deployment, which conflicts.
3. Clear the ComponentModelCache (see above).
4. Clear stale metadata: delete `ExtensionMetadata2.0.mpack` and `ExtensionMetadataCache.mpack` from the Extensions directory.

### Settings not reaching the server
VS's `ILanguageClientMiddleLayer` does NOT intercept `workspace/configuration` requests (server→client). Settings must be **pushed** from client to server via `workspace/didChangeConfiguration` notification. The `StrideSettings.Changed` event triggers `PushSettingsToServer()` which sends embedded settings in `params.settings.strideAssets`. The server's `onDidChangeConfiguration` handler accepts both pushed settings (VS path) and pull-based refresh (VS Code path).

### Diagnostic file-based logging
`StrideExtension.cs` writes to `%TEMP%\stride-vs-extension.log` to verify the Extensibility runtime lifecycle, since the VS Output pane isn't available when the Extensibility runtime first activates.

## Lessons Learned (Implementation Notes)

### Critical: Content type base definition
The content type MUST use `CodeRemoteContentDefinition.CodeRemoteContentTypeName` as base, NOT `"code"`. Using `"code"` prevents the ILanguageClient from activating. This is documented in Microsoft's official LSP sample but easy to miss.

### Critical: --stdio flag
The Node.js server must be launched with `--stdio` argument. Without it, `vscode-languageserver`'s `createConnection()` throws "Connection input stream is not set."

### Critical: VSSDK build tooling
VSIX/pkgdef generation requires full-framework MSBuild (not `dotnet build`). The project uses `Microsoft.VisualStudio.Extensibility.Sdk` with `VssdkCompatibleExtension=true` to support both VSSDK and VisualStudio.Extensibility in one VSIX. The manifest uses `ExtensionType="VSSDK+VisualStudio.Extensibility"`.

### RunOnContext attribute
`[RunOnContext(RunningContext.RunOnHost)]` is needed on the ILanguageClient class (per Microsoft's sample).

### Server discovery during F5
The extension DLL is deployed to `%LOCALAPPDATA%\...\Extensions\<random>\`, not the repo. `FindServerPath()` must walk up from the **solution directory** (cached on UI thread in `OnLoadedAsync`) to find the server in the repo tree.

### Workspace folders
VS may send only `rootUri` (not `workspaceFolders`) in the LSP initialize request. The server was updated to fall back to `rootUri` when `workspaceFolders` is empty.

### TextMate grammar
Registered via pkgdef (`$RootKey$\TextMate\Repositories` and `$RootKey$\TextMate\LanguageMapping\ContentTypeMapping`). The grammar JSON needs a `fileTypes` array for VS TextMate to match files by extension.

### Critical: DeployExtension must be false (SDK default)
The `Microsoft.VisualStudio.Extensibility.Build` targets set `DeployExtension=false` by default. Do NOT override this to `true`. The Extensibility Build handles F5 deployment via the `ExtensibilityProjectExtension` project capability. Setting `DeployExtension=true` activates VSSDK's separate deployment mechanism, creating a duplicate copy that conflicts with the Extensibility deployment. The official MS hybrid extension sample (`CompositeExtension`) does not set either `DeployExtension` or `CreateVsixContainer`.

### Critical: Settings push (not pull) for VS
`ILanguageClientMiddleLayer.CanHandle()` only intercepts client→server messages and `textDocument/*` responses. It does NOT intercept `workspace/configuration` (a server→client request). Settings must be pushed via `workspace/didChangeConfiguration` with embedded settings. The server's handler checks `params.settings.strideAssets` first (push path), falling back to `workspace/configuration` pull (VS Code path).

### CPM opt-out
The VSIX project must set `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>` since it's not part of the main Stride build and needs its own package versions.

## Known Limitations

### Hover tooltips: no markdown rendering
VS's LSP client advertises `hover.contentFormat: ["plaintext"]` only. The `SettingsMiddleLayer` strips markdown formatting (bold, backticks, links, headings) from hover responses to produce clean plaintext.

**Future improvement**: The server should return **structured data** (asset type, name, path, ID, etc.) instead of pre-formatted markdown. Each client can then render appropriately:
- **VS Code**: Format as Markdown (`MarkupKind.Markdown`)
- **Visual Studio**: Use the native QuickInfo API (`ClassifiedTextRun`, `ContainerElement`, `ClassifiedTextElement`) for rich tooltips with bold text, colored syntax, and clickable links

This would require:
1. A new hover response format from the server (structured JSON instead of markdown string)
2. A custom `IAsyncQuickInfoSourceProvider` in the VS extension that intercepts hover data and renders with VS-native rich text
3. The VS Code client to format the structured data into markdown

### CodeLens: VS-native implementation (not via LSP)
VS's LSP client does NOT support `textDocument/codeLens`. CodeLens is implemented natively using VisualStudio.Extensibility APIs:
- `ITextViewTaggerProvider<CodeLensTag>` tags `Id: GUID` lines as code elements
- `ICodeLensProvider` creates `InvokableCodeLens` instances showing reference counts
- `BackReferenceScanner` (C# port of server's backLinkScanner.ts) scans workspace files independently

The server's `codeLensHandler.ts` and `strideAssets.showReferences` command are only used by the VS Code client. The VS extension does its own scanning in C#.

**Note**: The VisualStudio.Extensibility CodeLens APIs are currently marked as preview (`VSEXTPREVIEW_CODELENS`, `VSEXTPREVIEW_TAGGERS`). They may change in future VS versions.

### CodeLens: click behavior
Currently, clicking a CodeLens refreshes (invalidates) the reference count. Future improvement: open a peek view showing all references using `VisualCodeLens` with a `RemoteUserControl` details popup.

### Cross-asset navigation
Depends on correct workspace root being sent to the server. VS sends `rootUri` (solution directory) which the server now uses as fallback. Navigation works when all asset files are under the solution root.

### Settings: legacy DialogPage
The current settings use `DialogPage` (Tools > Options), which VS 2026 flags as "legacy" — it shows "These settings haven't been migrated yet" in the new unified settings UI. VS 2026 introduces a new settings API. The settings should be migrated to the new system for a native experience. The legacy approach still works but opens in the old Options dialog.

## Key Differences from VS Code Client

| Aspect | VS Code | Visual Studio |
|--------|---------|---------------|
| Language | TypeScript | C# |
| LSP library | vscode-languageclient | Microsoft.VisualStudio.LanguageServer.Client |
| Content type base | N/A (file selectors) | `CodeRemoteContentDefinition.CodeRemoteContentTypeName` |
| C# resolution | vscode.executeWorkspaceSymbolProvider | Roslyn workspace APIs (direct compilation) |
| Custom requests | client.onRequest() | ILanguageClientCustomMessage2 + JsonRpcMethod |
| Hover rendering | Markdown (native) | Plaintext only (middleware strips markdown) |
| CodeLens | LSP `textDocument/codeLens` | VisualStudio.Extensibility (native tagger + provider) |
| Back-link scanning | Server-side (backLinkScanner.ts) | Client-side (BackReferenceScanner.cs) |
| Settings | workspace configuration (pull via workspace/configuration) | Push-based via workspace/didChangeConfiguration |
| Settings UI | VS Code settings editor | Tools > Options > Stride > Asset Navigator |

## Open Questions

- **Node.js server relevance for VS**: With CodeLens back-link scanning now ported to C#, review whether keeping the Node.js LSP server for VS makes sense. If more features get ported to the C# side (hover, diagnostics, go-to-definition via Roslyn), the server dependency could be eliminated for VS, making the extension self-contained. The server would remain for VS Code. To be investigated.
- **Node.js dependency**: Should the VSIX bundle Node.js or require it installed? Most Stride developers will have it, but bundling avoids support issues. (Moot if the server is eliminated — see above.)
- **Distribution**: Ship as a standalone VSIX on the VS Marketplace, or bundle with Stride's VS integration.
- **VS 2022 vs 2026**: Currently targets `[17.0, 19.0)`. Tested primarily on VS 2026 (18.x). May need the "Visual Studio extension development" workload.

## Roadmap

### Immediate (blocking)
- [ ] **Debug CodeLens/Extensibility runtime activation** — the VisualStudio.Extensibility runtime does not activate reliably. Script navigation and VSSDK features work, but Extensibility parts (CodeLens tagger, provider) don't. Check `%TEMP%\stride-vs-extension.log` after launch. May require investigating the metadata cache (`ExtensionMetadata2.0.mpack`) and how VS discovers `.vsextension/extension.json` in deployed extensions.
- [ ] **RS0030 semaphore warnings** — `SemaphoreSlim` and `AsyncSemaphore` are banned by VS analyzers (UI thread deadlock risk). Replace with `ReentrantSemaphore` from `Microsoft.VisualStudio.Threading`. Requires threading `JoinableTaskContext` through the Extensibility framework.

### Short-term
- [ ] Test CodeLens with Stride sample projects (SpaceEscape, etc.) once activation works
- [ ] Add peek view for CodeLens click (VisualCodeLens + RemoteUserControl)
- [ ] Re-test VS Code extension after server-side changes (settings push path, etc.) to ensure no regressions

### Medium-term
- [ ] Implement rendering-agnostic server (structured data instead of markdown for hover)
- [ ] Migrate settings to VS 2026 new settings API
- [ ] Investigate eliminating Node.js server for VS (port remaining features to C#)

## References

- [VS LSP Client docs](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension)
- [ILanguageClient API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.languageserver.client.ilanguageclient)
- [Custom messages](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension#receive-custom-messages)
- [Microsoft LSP sample (FooLanguageClient)](https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/LanguageServerProtocol)
- [TextMate grammar sample](https://github.com/madskristensen/TextmateSample)
- [VS QuickInfo API](https://learn.microsoft.com/en-us/visualstudio/extensibility/walkthrough-displaying-quickinfo-tooltips)
