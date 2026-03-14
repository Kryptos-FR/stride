# Phase 4: Visual Studio Client

## Current Status

The VS extension (`sources/tools/stride-assets-vs/`) is functional with the following features working:

- **Syntax highlighting** via TextMate grammar (registered through pkgdef)
- **LSP server connection** via stdio transport (`node server.js --stdio`)
- **Go-to-definition** for entity references within files
- **Hover tooltips** (plaintext only — see Known Limitations)
- **Diagnostics** for broken asset references
- **C# symbol resolution** via Roslyn workspace (`stride/resolveCSharpSymbol`)
- **Settings** via Tools > Options > Stride > Asset Navigator
- **Output logging** to a dedicated "Stride Asset Navigator" output pane

## Architecture

```
Visual Studio (C# VSIX) — sources/tools/stride-assets-vs/
  └── StrideLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
        ├── Starts Node.js server process (stdio, --stdio flag required)
        ├── Registers "stride-asset" content type (BaseDefinition: CodeRemoteContentDefinition)
        ├── SettingsMiddleLayer intercepts workspace/configuration and textDocument/hover
        ├── CSharpSymbolHandler handles stride/resolveCSharpSymbol via Roslyn
        └── FindServerPath: env var → VSIX dir → extension dir walk → solution dir walk
```

## Key Files

| File | Purpose |
|------|---------|
| `StrideLanguageClient.cs` | ILanguageClient + ILanguageClientCustomMessage2 implementation |
| `ContentTypeDefinitions.cs` | MEF content type + 27 file extension mappings |
| `CSharpSymbolHandler.cs` | Roslyn-based C# symbol resolution (JsonRpcMethod) |
| `StrideAssetsPackage.cs` | AsyncPackage + TextMate registration attributes |
| `StrideOptionsPage.cs` | Tools > Options dialog page + static StrideSettings |
| `OutputLogger.cs` | Thread-safe VS Output window pane logging |
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

## Lessons Learned (Implementation Notes)

### Critical: Content type base definition
The content type MUST use `CodeRemoteContentDefinition.CodeRemoteContentTypeName` as base, NOT `"code"`. Using `"code"` prevents the ILanguageClient from activating. This is documented in Microsoft's official LSP sample but easy to miss.

### Critical: --stdio flag
The Node.js server must be launched with `--stdio` argument. Without it, `vscode-languageserver`'s `createConnection()` throws "Connection input stream is not set."

### Critical: VSSDK build tooling
VSIX/pkgdef generation requires full-framework MSBuild (not `dotnet build`). The csproj needs:
```xml
<CustomAfterMicrosoftCSharpTargets Condition="'$(VsSDKInstall)' != ''">
    $(VsSDKInstall)\Microsoft.VsSDK.targets
</CustomAfterMicrosoftCSharpTargets>
```

### RunOnContext attribute
`[RunOnContext(RunningContext.RunOnHost)]` is needed on the ILanguageClient class (per Microsoft's sample).

### Server discovery during F5
The extension DLL is deployed to `%LOCALAPPDATA%\...\Extensions\<random>\`, not the repo. `FindServerPath()` must walk up from the **solution directory** (cached on UI thread in `OnLoadedAsync`) to find the server in the repo tree.

### Workspace folders
VS may send only `rootUri` (not `workspaceFolders`) in the LSP initialize request. The server was updated to fall back to `rootUri` when `workspaceFolders` is empty.

### TextMate grammar
Registered via pkgdef (`$RootKey$\TextMate\Repositories` and `$RootKey$\TextMate\LanguageMapping\ContentTypeMapping`). The grammar JSON needs a `fileTypes` array for VS TextMate to match files by extension.

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

### CodeLens commands
The server emits `strideAssets.showReferences` commands for CodeLens. The VS client does not yet handle this command. Needs mapping to VS's `IFindAllReferencesService` or `IPeekResultPresentation`.

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
| Settings | workspace configuration (package.json) | SettingsMiddleLayer intercepts workspace/configuration |
| Settings UI | VS Code settings editor | Tools > Options > Stride > Asset Navigator |

## Open Questions

- **Node.js dependency**: Should the VSIX bundle Node.js or require it installed? Most Stride developers will have it, but bundling avoids support issues.
- **Distribution**: Ship as a standalone VSIX on the VS Marketplace, or bundle with Stride's VS integration.
- **VS 2022 vs 2026**: Currently targets `[17.0, 19.0)`. Tested primarily on VS 2026 (18.x). May need the "Visual Studio extension development" workload.

## References

- [VS LSP Client docs](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension)
- [ILanguageClient API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.languageserver.client.ilanguageclient)
- [Custom messages](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension#receive-custom-messages)
- [Microsoft LSP sample (FooLanguageClient)](https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/LanguageServerProtocol)
- [TextMate grammar sample](https://github.com/madskristensen/TextmateSample)
- [VS QuickInfo API](https://learn.microsoft.com/en-us/visualstudio/extensibility/walkthrough-displaying-quickinfo-tooltips)
