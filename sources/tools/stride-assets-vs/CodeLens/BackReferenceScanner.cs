using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StrideAssets.VisualStudio.CodeLens
{
    /// <summary>
    /// Represents a location where a GUID is referenced from another file.
    /// </summary>
    internal sealed class BackReference
    {
        public string SourceFilePath { get; }
        public int Line { get; }
        public int Column { get; }
        public string Context { get; }

        public BackReference(string sourceFilePath, int line, int column, string context)
        {
            SourceFilePath = sourceFilePath;
            Line = line;
            Column = column;
            Context = context;
        }
    }

    /// <summary>
    /// Scans workspace .sd* files for cross-asset and part references.
    /// C# port of the server's backLinkScanner.ts.
    /// </summary>
    internal sealed class BackReferenceScanner
    {
        // Cross-asset reference: GUID:AssetName
        private static readonly Regex AssetReferenceRegex = new(
            @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}):([^\s,}\]]+(?:\s+[^\s,}\]:]+)*)",
            RegexOptions.Compiled);

        // Internal part reference: ref!! GUID
        private static readonly Regex PartReferenceRegex = new(
            @"ref!!\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
            RegexOptions.Compiled);

        // Asset header: first Id line (no indentation)
        private static readonly Regex AssetIdRegex = new(
            @"^Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // YAML key context extraction
        private static readonly Regex KeyContextRegex = new(
            @"^\s*(?:[0-9a-fA-F]+~)?(\w[\w\s]*?):\s",
            RegexOptions.Compiled);

        // back-reference index: target GUID -> list of referencing locations
        private readonly ConcurrentDictionary<string, List<BackReference>> _backRefs = new();

        // file -> list of GUIDs it references (for efficient cleanup on rescan)
        private readonly ConcurrentDictionary<string, List<string>> _backRefsByFile = new();

        private readonly object _lock = new();

        public IReadOnlyList<BackReference> GetBackRefs(string guid)
        {
            var key = guid.ToLowerInvariant();
            lock (_lock)
            {
                return _backRefs.TryGetValue(key, out var refs)
                    ? refs.ToList()
                    : Array.Empty<BackReference>();
            }
        }

        public async Task ScanWorkspaceAsync(string workspaceRoot, CancellationToken ct)
        {
            Log.Debug($"[BackRefScanner] Scanning workspace: {workspaceRoot}");
            var files = FindSdFiles(workspaceRoot);
            Log.Debug($"[BackRefScanner] Found {files.Count} .sd* file(s)");
            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;
                await ScanFileAsync(file, ct);
            }
            Log.Debug($"[BackRefScanner] Scan complete — {_backRefs.Count} unique GUID(s) referenced");
        }

        public async Task ScanFileAsync(string filePath, CancellationToken ct)
        {
            try
            {
                var content = await ReadFileAsync(filePath);
                if (string.IsNullOrEmpty(content)) return;

                // Find the asset's own Id to exclude self-references
                string? ownGuid = null;
                var lines = content.Split('\n');
                for (int i = 0; i < Math.Min(lines.Length, 3); i++)
                {
                    var idMatch = AssetIdRegex.Match(lines[i]);
                    if (idMatch.Success)
                    {
                        ownGuid = idMatch.Groups[1].Value.ToLowerInvariant();
                        break;
                    }
                }

                // Clear old back-refs from this file
                ClearBackRefsFromFile(filePath);

                var referencedGuids = new List<string>();

                // Scan for asset references (GUID:Name)
                for (int lineNum = 0; lineNum < lines.Length; lineNum++)
                {
                    if (ct.IsCancellationRequested) return;
                    var line = lines[lineNum];

                    foreach (Match match in AssetReferenceRegex.Matches(line))
                    {
                        var guid = match.Groups[1].Value.ToLowerInvariant();
                        if (guid == ownGuid) continue;

                        var context = GetLineContext(line);
                        AddBackRef(guid, new BackReference(filePath, lineNum, match.Index, context));
                        referencedGuids.Add(guid);
                    }

                    foreach (Match match in PartReferenceRegex.Matches(line))
                    {
                        var guid = match.Groups[1].Value.ToLowerInvariant();
                        var context = line.Contains("ref!!") ? "ref!!" : GetLineContext(line);
                        AddBackRef(guid, new BackReference(filePath, lineNum, match.Index, context));
                        referencedGuids.Add(guid);
                    }
                }

                _backRefsByFile[filePath] = referencedGuids;
            }
            catch
            {
                // Skip unreadable files
            }
        }

        private void AddBackRef(string guid, BackReference backRef)
        {
            lock (_lock)
            {
                if (!_backRefs.TryGetValue(guid, out var list))
                {
                    list = new List<BackReference>();
                    _backRefs[guid] = list;
                }
                list.Add(backRef);
            }
        }

        private void ClearBackRefsFromFile(string filePath)
        {
            lock (_lock)
            {
                if (_backRefsByFile.TryRemove(filePath, out var guids))
                {
                    foreach (var guid in guids)
                    {
                        if (_backRefs.TryGetValue(guid, out var list))
                        {
                            list.RemoveAll(r => string.Equals(r.SourceFilePath, filePath, StringComparison.OrdinalIgnoreCase));
                            if (list.Count == 0)
                                _backRefs.TryRemove(guid, out _);
                        }
                    }
                }
            }
        }

        private static string GetLineContext(string line)
        {
            var match = KeyContextRegex.Match(line);
            if (match.Success)
                return match.Groups[1].Value.Trim();
            if (line.Contains("ref!!"))
                return "ref!!";
            return "";
        }

        private static async Task<string> ReadFileAsync(string path)
        {
            using var reader = new StreamReader(path, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return await reader.ReadToEndAsync();
        }

        private static List<string> FindSdFiles(string root)
        {
            var results = new List<string>();
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.sd*", SearchOption.AllDirectories))
                {
                    var dir = Path.GetDirectoryName(file) ?? "";
                    if (dir.Contains(@"\bin\") || dir.Contains(@"\obj\") || dir.Contains(@"\.git\"))
                        continue;
                    if (Regex.IsMatch(Path.GetExtension(file), @"^\.sd\w+$", RegexOptions.IgnoreCase))
                        results.Add(file);
                }
            }
            catch
            {
                // Skip inaccessible directories
            }
            return results;
        }
    }
}
