using System;
using System.Collections.Generic;

namespace StrideAssets.VisualStudio
{
    /// <summary>
    /// Thread-safe in-memory index of all Stride asset files in the workspace.
    /// All public methods are protected by a single lock; I/O must be completed
    /// before calling index methods so no await occurs while the lock is held.
    /// </summary>
    internal sealed class AssetIndex
    {
        public static AssetIndex Instance { get; } = new AssetIndex();

        private readonly object _lock = new object();

        // Primary asset lookup: guid → asset entry
        private readonly Dictionary<string, AssetEntry> _assetsByGuid =
            new Dictionary<string, AssetEntry>(StringComparer.OrdinalIgnoreCase);

        // Part lookup: part guid → part entry (entities, UI elements inside composite assets)
        private readonly Dictionary<string, AssetPartEntry> _partsByGuid =
            new Dictionary<string, AssetPartEntry>(StringComparer.OrdinalIgnoreCase);

        // Reverse index: file path → list of asset guids defined in that file
        private readonly Dictionary<string, List<string>> _guidsByFile =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Reverse index: file path → list of part guids defined in that file
        private readonly Dictionary<string, List<string>> _partsByFile =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // Back-references: target guid → list of locations that reference it
        private readonly Dictionary<string, List<BackReference>> _backRefs =
            new Dictionary<string, List<BackReference>>(StringComparer.OrdinalIgnoreCase);

        // Reverse back-ref index: source file → set of target guids it references
        private readonly Dictionary<string, HashSet<string>> _backRefsByFile =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fired (outside the lock) after <see cref="UpdateFile"/> or <see cref="RemoveFile"/>
        /// completes so that open document taggers can refresh their diagnostics.
        /// </summary>
        internal static event Action? IndexUpdated;

        private AssetIndex() { }

        public void AddAsset(AssetEntry entry)
        {
            lock (_lock)
            {
                _assetsByGuid[entry.Id] = entry;
                if (!_guidsByFile.TryGetValue(entry.FilePath, out var list))
                    _guidsByFile[entry.FilePath] = list = new List<string>();
                if (!list.Contains(entry.Id))
                    list.Add(entry.Id);
            }
        }

        public void AddPart(AssetPartEntry part)
        {
            lock (_lock)
            {
                _partsByGuid[part.Id] = part;
                if (!_partsByFile.TryGetValue(part.FilePath, out var list))
                    _partsByFile[part.FilePath] = list = new List<string>();
                if (!list.Contains(part.Id))
                    list.Add(part.Id);
            }
        }

        public void AddBackRef(string targetGuid, BackReference backRef)
        {
            lock (_lock)
            {
                var key = targetGuid.ToLowerInvariant();
                if (!_backRefs.TryGetValue(key, out var list))
                    _backRefs[key] = list = new List<BackReference>();
                list.Add(backRef);

                if (!_backRefsByFile.TryGetValue(backRef.SourceFilePath, out var guids))
                    _backRefsByFile[backRef.SourceFilePath] = guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                guids.Add(key);
            }
        }

        public void RemoveFile(string filePath)
        {
            lock (_lock)
                RemoveFileUnsafe(filePath);
            IndexUpdated?.Invoke();
        }

        /// <summary>
        /// Atomically replaces all data for a file: removes stale entries then adds new ones.
        /// </summary>
        public void UpdateFile(string filePath, AssetEntry? entry, List<AssetPartEntry> parts,
            List<(string targetGuid, BackReference backRef)> backRefs)
        {
            lock (_lock)
            {
                RemoveFileUnsafe(filePath);

                if (entry != null)
                {
                    _assetsByGuid[entry.Id] = entry;
                    if (!_guidsByFile.TryGetValue(filePath, out var list))
                        _guidsByFile[filePath] = list = new List<string>();
                    list.Add(entry.Id);
                }

                if (parts.Count > 0)
                {
                    var partIds = new List<string>(parts.Count);
                    foreach (var p in parts)
                    {
                        _partsByGuid[p.Id] = p;
                        partIds.Add(p.Id);
                    }
                    _partsByFile[filePath] = partIds;
                }

                foreach (var (targetGuid, backRef) in backRefs)
                {
                    var key = targetGuid.ToLowerInvariant();
                    if (!_backRefs.TryGetValue(key, out var refList))
                        _backRefs[key] = refList = new List<BackReference>();
                    refList.Add(backRef);

                    if (!_backRefsByFile.TryGetValue(filePath, out var guids))
                        _backRefsByFile[filePath] = guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    guids.Add(key);
                }
            }
            IndexUpdated?.Invoke();
        }

        public AssetEntry? LookupGuid(string guid)
        {
            lock (_lock)
            {
                _assetsByGuid.TryGetValue(guid, out var entry);
                return entry;
            }
        }

        public AssetPartEntry? LookupPart(string guid)
        {
            lock (_lock)
            {
                _partsByGuid.TryGetValue(guid, out var entry);
                return entry;
            }
        }

        public IReadOnlyList<BackReference> GetBackRefs(string guid)
        {
            lock (_lock)
            {
                return _backRefs.TryGetValue(guid.ToLowerInvariant(), out var list)
                    ? list.AsReadOnly()
                    : Array.Empty<BackReference>();
            }
        }

        public (int assets, int parts, int backRefs) GetStats()
        {
            lock (_lock)
            {
                int backRefCount = 0;
                foreach (var list in _backRefs.Values)
                    backRefCount += list.Count;
                return (_assetsByGuid.Count, _partsByGuid.Count, backRefCount);
            }
        }

        // Must be called while _lock is held.
        private void RemoveFileUnsafe(string filePath)
        {
            if (_guidsByFile.TryGetValue(filePath, out var guids))
            {
                foreach (var g in guids)
                    _assetsByGuid.Remove(g);
                _guidsByFile.Remove(filePath);
            }

            if (_partsByFile.TryGetValue(filePath, out var partGuids))
            {
                foreach (var g in partGuids)
                    _partsByGuid.Remove(g);
                _partsByFile.Remove(filePath);
            }

            if (_backRefsByFile.TryGetValue(filePath, out var targetGuids))
            {
                foreach (var target in targetGuids)
                {
                    if (_backRefs.TryGetValue(target, out var refs))
                    {
                        refs.RemoveAll(r => string.Equals(r.SourceFilePath, filePath, StringComparison.OrdinalIgnoreCase));
                        if (refs.Count == 0)
                            _backRefs.Remove(target);
                    }
                }
                _backRefsByFile.Remove(filePath);
            }
        }
    }
}
