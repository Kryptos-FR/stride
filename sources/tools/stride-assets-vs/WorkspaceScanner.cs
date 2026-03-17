using System;
using System.IO;
using System.Threading.Tasks;

namespace StrideAssets.VisualStudio
{
    /// <summary>
    /// Discovers and indexes all Stride asset files (.sd*) under the solution root.
    /// Initial scan runs on the thread pool; file changes are tracked via FileSystemWatcher.
    ///
    /// Note: VS VSSDK also provides IVsFileChangeEx.AdviseDirChange for directory watching,
    /// which uses VS's own notification infrastructure and avoids the FSW internal buffer.
    /// FileSystemWatcher is used here for simplicity; the buffer is doubled to 64 KB to
    /// reduce the chance of missed events on busy asset trees. Migrate to IVsFileChangeEx
    /// only if buffer overflow becomes a real issue.
    /// </summary>
    internal sealed class WorkspaceScanner
    {
        public static WorkspaceScanner Instance { get; } = new WorkspaceScanner();

        private FileSystemWatcher? _watcher;

        private WorkspaceScanner() { }

        public async Task StartAsync(string rootPath)
        {
            try
            {
                Log.Write($"Stride Asset Navigator: Scanning {rootPath}...");
                await Task.Run(() => ScanRoot(rootPath));

                SetupWatcher(rootPath);

                var (assets, parts, backRefs) = AssetIndex.Instance.GetStats();
                Log.Write($"Indexed {assets} assets, {parts} parts, {backRefs} back-references");
            }
            catch (Exception ex)
            {
                Log.Error($"Workspace scan failed: {ex.Message}");
            }
        }

        public void Stop()
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        private void ScanRoot(string rootPath)
        {
            try
            {
                foreach (var filePath in EnumerateAssetFiles(rootPath))
                    IndexFile(filePath);
            }
            catch (Exception ex)
            {
                Log.Error($"Error during initial scan: {ex.Message}");
            }
        }

        /// <summary>
        /// Enumerates all .sd* files under rootPath, skipping bin/, obj/, .git/ directories.
        /// </summary>
        private static System.Collections.Generic.IEnumerable<string> EnumerateAssetFiles(string rootPath)
        {
            if (!Directory.Exists(rootPath))
                yield break;

            var queue = new System.Collections.Generic.Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                var dir = queue.Dequeue();

                // Skip noise directories
                var dirName = Path.GetFileName(dir);
                if (string.Equals(dirName, "bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dirName, "obj", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dirName, ".git", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dirName, "node_modules", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] files;
                try { files = Directory.GetFiles(dir, "*.sd*"); }
                catch { continue; }

                foreach (var f in files)
                    yield return f;

                string[] subdirs;
                try { subdirs = Directory.GetDirectories(dir); }
                catch { continue; }

                foreach (var sub in subdirs)
                    queue.Enqueue(sub);
            }
        }

        /// <summary>
        /// Header-only index: reads first 1000 chars to extract asset Id and type.
        /// Used during the initial scan to avoid reading every file in full.
        /// </summary>
        private static void IndexFile(string filePath)
        {
            if (!IsStrideAsset(filePath))
                return;
            try
            {
                string content;
                using (var reader = new StreamReader(filePath))
                {
                    var buf = new char[1000];
                    int read = reader.Read(buf, 0, buf.Length);
                    content = new string(buf, 0, read);
                }

                var entry = AssetParser.ParseHeader(content, filePath);
                if (entry != null)
                    AssetIndex.Instance.AddAsset(entry);

                ReindexPartsOnly(filePath);
            }
            catch (Exception ex)
            {
                Log.Debug($"IndexFile failed for {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Full re-index: reads the entire file, updates assets, parts, and back-references.
        /// Used by the file watcher on change events.
        /// </summary>
        private static void ReindexFile(string filePath)
        {
            if (!IsStrideAsset(filePath))
                return;
            try
            {
                var content = File.ReadAllText(filePath);
                var parsed = AssetParser.ParseFull(content, filePath);
                if (parsed == null)
                {
                    AssetIndex.Instance.RemoveFile(filePath);
                    return;
                }

                var entry = new AssetEntry(parsed.Id, parsed.TypeName, filePath, parsed.SourcePath);
                var parts = new System.Collections.Generic.List<AssetPartEntry>(parsed.Parts);
                var backRefs = new System.Collections.Generic.List<(string, BackReference)>();

                foreach (var (guid, name) in parsed.References)
                {
                    backRefs.Add((guid, new BackReference(filePath, 0, 0, name)));
                }

                AssetIndex.Instance.UpdateFile(filePath, entry, parts, backRefs);
            }
            catch (Exception ex)
            {
                Log.Debug($"ReindexFile failed for {filePath}: {ex.Message}");
            }
        }

        private static void ReindexPartsOnly(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var parts = AssetParser.ParseParts(content, filePath);
                foreach (var p in parts)
                    AssetIndex.Instance.AddPart(p);
            }
            catch (Exception ex)
            {
                Log.Debug($"ReindexPartsOnly failed for {filePath}: {ex.Message}");
            }
        }

        private static void RemoveFile(string filePath)
        {
            if (IsStrideAsset(filePath))
            {
                AssetIndex.Instance.RemoveFile(filePath);
                Log.Debug($"Removed: {Path.GetFileName(filePath)}");
            }
        }

        private void SetupWatcher(string rootPath)
        {
            if (!Directory.Exists(rootPath))
                return;

            _watcher = new FileSystemWatcher(rootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                InternalBufferSize = 65536  // 64 KB, doubled from the 32 KB default
            };

            _watcher.Created += (_, e) => IndexFile(e.FullPath);
            _watcher.Changed += (_, e) => ReindexFile(e.FullPath);
            _watcher.Deleted += (_, e) => RemoveFile(e.FullPath);
            _watcher.Renamed += (_, e) =>
            {
                RemoveFile(e.OldFullPath);
                IndexFile(e.FullPath);
            };
            _watcher.Error += (_, e) =>
                Log.Error($"FileSystemWatcher error: {e.GetException().Message}");

            _watcher.EnableRaisingEvents = true;
            Log.Debug($"Watching {rootPath} for .sd* changes");
        }

        private static bool IsStrideAsset(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Length > 3 && ext.StartsWith(".sd", StringComparison.OrdinalIgnoreCase);
        }
    }
}
