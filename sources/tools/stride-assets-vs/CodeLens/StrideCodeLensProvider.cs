#pragma warning disable VSEXTPREVIEW_CODELENS

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using EditorCodeLens = Microsoft.VisualStudio.Extensibility.Editor.CodeLens;

namespace StrideAssets.VisualStudio.CodeLens
{
    /// <summary>
    /// Provides CodeLens indicators showing reference counts for asset/part GUIDs.
    /// </summary>
    [VisualStudioContribution]
    internal class StrideCodeLensProvider : ExtensionPart, ICodeLensProvider
    {
        // Shared scanner instance — lazy-initialized on first use
        private static BackReferenceScanner? _scanner;
        private static string? _scannedRoot;
        private static readonly SemaphoreSlim _scanLock = new(1, 1);

        public TextViewExtensionConfiguration TextViewExtensionConfiguration => new()
        {
            AppliesTo =
            [
                DocumentFilter.FromGlobPattern("**/*.sdscene", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdprefab", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdmat", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdtex", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdm3d", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdanim", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdskel", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdgfxcomp", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdgamesettings", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdsheet", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdfnt", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdpkg", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdsky", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdsnd", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdvid", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdrendertex", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sduipage", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sduilib", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdnavmesh", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdhmap", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdphy", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdpcfnt", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdeffectlog", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdhull", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdpromodel", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdprefabmodel", relativePath: true),
                DocumentFilter.FromGlobPattern("**/*.sdtpl", relativePath: true),
            ],
        };

        public CodeLensProviderConfiguration CodeLensProviderConfiguration =>
            new("Stride Asset References") { Priority = 300 };

        public async Task<EditorCodeLens?> TryCreateCodeLensAsync(
            CodeElement codeElement,
            CodeElementContext codeElementContext,
            CancellationToken token)
        {
            Log.Debug($"[CodeLensProvider] TryCreateCodeLensAsync: kind={codeElement.Kind}, id={codeElement.UniqueIdentifier}");

            // Only handle our custom code element kind
            if (codeElement.Kind != StrideCodeLensTagger.AssetIdKind)
            {
                Log.Debug($"[CodeLensProvider] Skipping — kind mismatch (expected {StrideCodeLensTagger.AssetIdKind})");
                return null;
            }

            // Get the GUID from the tag properties
            var guid = codeElement.UniqueIdentifier;
            if (string.IsNullOrEmpty(guid))
            {
                Log.Debug("[CodeLensProvider] Skipping — empty GUID");
                return null;
            }

            // Get file path from the code element context range
            var filePath = GetFilePathFromUri(codeElementContext.Range.Document.Uri);
            Log.Debug($"[CodeLensProvider] File path: {filePath ?? "(null)"}, URI: {codeElementContext.Range.Document.Uri}");
            if (filePath == null)
                return null;

            var workspaceRoot = FindWorkspaceRoot(filePath);
            Log.Debug($"[CodeLensProvider] Workspace root: {workspaceRoot ?? "(not found)"}");
            if (workspaceRoot == null)
                return null;

            // Ensure back-references are scanned
            var scanner = await GetOrCreateScannerAsync(workspaceRoot, token);
            var refs = scanner.GetBackRefs(guid);
            Log.Debug($"[CodeLensProvider] Created CodeLens for {guid}: {refs.Count} reference(s)");

            return new StrideReferencesCodeLens(guid, scanner);
        }

        private static string? GetFilePathFromUri(Uri uri)
        {
            if (uri.IsFile)
                return uri.LocalPath;
            // Handle file:/// URIs that may not be properly parsed
            if (uri.Scheme == "file")
                return Uri.UnescapeDataString(uri.AbsolutePath).Replace('/', '\\');
            return null;
        }

        private static async Task<BackReferenceScanner> GetOrCreateScannerAsync(
            string workspaceRoot, CancellationToken ct)
        {
            await _scanLock.WaitAsync(ct);
            try
            {
                if (_scanner != null && string.Equals(_scannedRoot, workspaceRoot, StringComparison.OrdinalIgnoreCase))
                    return _scanner;

                var scanner = new BackReferenceScanner();
                await scanner.ScanWorkspaceAsync(workspaceRoot, ct);
                _scanner = scanner;
                _scannedRoot = workspaceRoot;
                return scanner;
            }
            finally
            {
                _scanLock.Release();
            }
        }

        /// <summary>
        /// Walk up from the file to find the workspace root (directory containing .sdpkg or .sln).
        /// </summary>
        private static string? FindWorkspaceRoot(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            for (int i = 0; i < 15 && dir != null; i++)
            {
                if (Directory.GetFiles(dir, "*.sdpkg").Length > 0 ||
                    Directory.GetFiles(dir, "*.sln").Length > 0 ||
                    Directory.GetFiles(dir, "*.slnx").Length > 0)
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
