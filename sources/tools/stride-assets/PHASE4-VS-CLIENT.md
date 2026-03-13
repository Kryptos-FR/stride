# Phase 4: Visual Studio Client

## Goal

Create a Visual Studio extension (VSIX) that connects to the existing LSP language server (`packages/server`) to provide Stride asset navigation in Visual Studio.

## Prerequisites

- The LSP server (`packages/server/out/server.js`) runs as a standalone Node.js process via stdio transport
- Node.js must be available on the machine (or bundled)
- The server already handles all features: definition, hover, diagnostics, document links, CodeLens, workspace symbol

## Architecture

```
Visual Studio (C# VSIX)
  └── ILanguageClient implementation
        ├── Starts Node.js server process (stdio)
        ├── Registers content type for .sd* files
        ├── Handles stride/resolveCSharpSymbol via Roslyn APIs
        └── Maps strideAssets.showReferences to VS equivalent
```

## Implementation Steps

### 1. Project Setup

- Create `sources/tools/stride-assets-vs/` as a C# VSIX project
- Target Visual Studio 2022+ (17.x)
- Reference `Microsoft.VisualStudio.LanguageServer.Client` NuGet package
- Include the compiled server JS files as embedded resources or require them adjacent

### 2. Content Type Registration

- Register a new content type for Stride asset files
- Map `.sdscene`, `.sdprefab`, `.sdmat`, `.sdtex`, `.sdm3d`, `.sdfnt`, `.sdsheet`, `.sdgfxcomp`, `.sdgamesettings`, `.sdskel`, `.sdanim`, `.sdeffectlog`, `.sduilib`, `.sduipage`, `.sdpkg`, `.sdtpl` extensions
- Copy or reference the TextMate grammar from `packages/vscode-client/syntaxes/` if VS supports it, otherwise provide basic colorization

### 3. Language Client

Implement `ILanguageClient`:

```csharp
[ContentType("stride-asset")]
[Export(typeof(ILanguageClient))]
public class StrideLanguageClient : ILanguageClient
{
    public string Name => "Stride Asset Navigator";

    // Start server.js via Node.js with stdio transport
    public async Task<Connection> ActivateAsync(CancellationToken token)
    {
        var serverPath = Path.Combine(extensionDir, "server", "out", "server.js");
        var process = new Process { StartInfo = { FileName = "node", Arguments = serverPath } };
        // Configure stdio streams
    }
}
```

### 4. C# Symbol Resolution (Roslyn Bridge)

Handle the custom `stride/resolveCSharpSymbol` request from the server:

- Use `ILanguageClientCustomMessage2` to register a handler
- Query the Roslyn workspace for the requested type/member
- Return the file URI and position

This replaces the VS Code C# bridge (`csharpBridge.ts`) with native Roslyn APIs, which should be more reliable since Visual Studio has direct access to the C# compilation.

### 5. CodeLens Command Mapping

The server emits `strideAssets.showReferences` commands. The VS client needs to:

- Register a handler for this command
- Convert the arguments to Visual Studio's reference peek UI
- Or use `IFindAllReferencesService` / `IPeekResultPresentation`

### 6. Settings

Map the `strideAssets.*` settings to Visual Studio's options system:
- `diagnosticsEnabled`, `scriptNavigationEnabled`, `backLinksEnabled`
- Expose via Tools > Options > Stride Assets

## Key Differences from VS Code Client

| Aspect | VS Code | Visual Studio |
|--------|---------|---------------|
| Language | TypeScript | C# |
| LSP library | vscode-languageclient | Microsoft.VisualStudio.LanguageServer.Client |
| C# resolution | vscode.executeWorkspaceSymbolProvider | Roslyn workspace APIs |
| Custom requests | client.onRequest() | ILanguageClientCustomMessage2 |
| Status bar | vscode.StatusBarItem | IVsStatusbar |
| Settings | workspace configuration | IVsOptions / Tools > Options |
| File watching | Already handled server-side (chokidar) | Same - no client work needed |

## Open Questions

- **Node.js dependency**: Should the VSIX bundle Node.js or require it installed? Most Stride developers will have it, but bundling avoids support issues.
- **TextMate grammar**: Visual Studio 2022+ supports TextMate grammars via `ITextMateGrammarRegistration`. Verify this works with our `.tmLanguage.json`.
- **Testing**: The SpaceEscape sample project can be used for manual testing (same as VS Code client).
- **Distribution**: Ship as a standalone VSIX on the VS Marketplace, or bundle with Stride's VS integration.

## References

- [VS LSP Client docs](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension)
- [ILanguageClient API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.languageserver.client.ilanguageclient)
- [Custom messages](https://learn.microsoft.com/en-us/visualstudio/extensibility/adding-an-lsp-extension#receive-custom-messages)
