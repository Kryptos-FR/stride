#pragma warning disable CS0649 // MEF fields assigned by composition container

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace StrideAssets.VisualStudio
{
    // ─── Provider ────────────────────────────────────────────────────────────

    [Export(typeof(ITaggerProvider))]
    [ContentType("stride-asset")]
    [TagType(typeof(IErrorTag))]
    internal sealed class AssetDiagnosticsTaggerProvider : ITaggerProvider
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactory { get; set; } = null!;

        public ITagger<T>? CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (typeof(T) != typeof(IErrorTag)) return null;
            TextDocumentFactory.TryGetTextDocument(buffer, out var textDoc);
            return buffer.Properties.GetOrCreateSingletonProperty(
                typeof(AssetDiagnosticsTagger),
                () => new AssetDiagnosticsTagger(buffer, textDoc?.FilePath)) as ITagger<T>;
        }
    }

    // ─── Tagger ──────────────────────────────────────────────────────────────

    internal sealed class AssetDiagnosticsTagger : ITagger<IErrorTag>, IDisposable
    {
        // ── Strict patterns (existing) ────────────────────────────────────────

        // GUID:Name — group 1 = GUID (strict, 8-4-4-4-12)
        private static readonly Regex AssetRefRegex = new Regex(
            @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})" +
            @":[^\s,}\]]+",
            RegexOptions.Compiled);

        // ref!! GUID — group 1 = GUID (strict)
        private static readonly Regex PartRefRegex = new Regex(
            @"ref!!\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
            RegexOptions.Compiled);

        // ── Malformed-GUID patterns (new) ─────────────────────────────────────

        // Lenient GUID:Name — captures hex+dash sequences in GUID slot, incl. malformed ones
        private static readonly Regex LenientAssetRefRegex = new Regex(
            @"([0-9a-fA-F]{5,45}(?:-[0-9a-fA-F]+){1,5}):[^\s,}\]]+",
            RegexOptions.Compiled);

        // ref!! <token> — captures any non-whitespace token after ref!!
        private static readonly Regex PartRefTokenRegex = new Regex(
            @"ref!!\s+(\S+)",
            RegexOptions.Compiled);

        // Strict GUID validation (full-string match)
        private static readonly Regex StrictGuidRegex = new Regex(
            @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ── Source link pattern (new) ─────────────────────────────────────────

        // Source: <path> — group 1 = path value
        private static readonly Regex SourceLineRegex = new Regex(
            @"^\s*Source:\s+(.+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // ─────────────────────────────────────────────────────────────────────

        private readonly ITextBuffer _buffer;
        private readonly string? _filePath;
        private readonly System.Timers.Timer _timer;

        public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

        public AssetDiagnosticsTagger(ITextBuffer buffer, string? filePath)
        {
            _buffer = buffer;
            _filePath = filePath;
            _timer = new System.Timers.Timer(500) { AutoReset = false };
            _timer.Elapsed += (_, _) => FireTagsChanged();

            buffer.Changed += OnBufferChanged;
            AssetIndex.IndexUpdated += OnIndexUpdated;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
            => Restart(500);

        private void OnIndexUpdated()
            => Restart(200);

        private void Restart(double ms)
        {
            _timer.Stop();
            _timer.Interval = ms;
            _timer.Start();
        }

        private void FireTagsChanged()
        {
            var snapshot = _buffer.CurrentSnapshot;
            TagsChanged?.Invoke(this,
                new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0) yield break;

            var snapshot = spans[0].Snapshot;
            var text = snapshot.GetText();

            // Skip the asset's own Id value (definition, not a reference)
            var ownEntry = AssetParser.ParseHeader(text, string.Empty);
            var ownId = ownEntry?.Id; // already lowercase

            // ── Pass 1: GUID:Name — unresolved (warning) ─────────────────────

            foreach (Match m in AssetRefRegex.Matches(text))
            {
                var guid = m.Groups[1].Value.ToLowerInvariant();
                if (guid == ownId) continue;
                if (AssetIndex.Instance.LookupGuid(guid) != null) continue;
                if (AssetIndex.Instance.LookupPart(guid) != null) continue;

                var guidSpan = new SnapshotSpan(snapshot, m.Groups[1].Index, m.Groups[1].Length);
                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(guidSpan))) continue;

                yield return new TagSpan<IErrorTag>(guidSpan,
                    new ErrorTag(PredefinedErrorTypeNames.Warning,
                        $"Asset reference not found: {guid}"));
            }

            // ── Pass 2: ref!! GUID — unresolved (warning) ────────────────────

            foreach (Match m in PartRefRegex.Matches(text))
            {
                var guid = m.Groups[1].Value.ToLowerInvariant();
                if (guid == ownId) continue;
                if (AssetIndex.Instance.LookupGuid(guid) != null) continue;
                if (AssetIndex.Instance.LookupPart(guid) != null) continue;

                var guidSpan = new SnapshotSpan(snapshot, m.Groups[1].Index, m.Groups[1].Length);
                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(guidSpan))) continue;

                yield return new TagSpan<IErrorTag>(guidSpan,
                    new ErrorTag(PredefinedErrorTypeNames.Warning,
                        $"Part reference not found: {guid}"));
            }

            // ── Pass 3: GUID:Name — malformed GUID slot (error) ──────────────

            foreach (Match m in LenientAssetRefRegex.Matches(text))
            {
                var slot = m.Groups[1].Value;
                if (StrictGuidRegex.IsMatch(slot)) continue; // valid — already handled above

                var slotSpan = new SnapshotSpan(snapshot, m.Groups[1].Index, m.Groups[1].Length);
                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(slotSpan))) continue;

                yield return new TagSpan<IErrorTag>(slotSpan,
                    new ErrorTag(PredefinedErrorTypeNames.SyntaxError,
                        "Malformed GUID (expected 8-4-4-4-12 hex digits)"));
            }

            // ── Pass 4: ref!! <token> — malformed (error) ────────────────────

            foreach (Match m in PartRefTokenRegex.Matches(text))
            {
                var token = m.Groups[1].Value;
                if (StrictGuidRegex.IsMatch(token)) continue; // valid — already handled above

                var tokenSpan = new SnapshotSpan(snapshot, m.Groups[1].Index, m.Groups[1].Length);
                if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(tokenSpan))) continue;

                yield return new TagSpan<IErrorTag>(tokenSpan,
                    new ErrorTag(PredefinedErrorTypeNames.SyntaxError,
                        "Malformed GUID (expected 8-4-4-4-12 hex digits)"));
            }

            // ── Pass 5: Source: <path> — file not found (error) ──────────────

            if (_filePath != null)
            {
                var assetDir = Path.GetDirectoryName(_filePath);
                foreach (Match m in SourceLineRegex.Matches(text))
                {
                    var rawPath = m.Groups[1].Value.Trim();
                    if (rawPath.StartsWith("!", StringComparison.Ordinal)) continue; // YAML type tag
                    if (string.Equals(rawPath, "null", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.IsNullOrEmpty(rawPath)) continue;

                    var resolved = Path.GetFullPath(Path.Combine(assetDir!, rawPath));
                    if (File.Exists(resolved)) continue;

                    var pathSpan = new SnapshotSpan(snapshot, m.Groups[1].Index, m.Groups[1].Length);
                    if (!spans.IntersectsWith(new NormalizedSnapshotSpanCollection(pathSpan))) continue;

                    yield return new TagSpan<IErrorTag>(pathSpan,
                        new ErrorTag(PredefinedErrorTypeNames.SyntaxError,
                            $"Source file not found: {rawPath}"));
                }
            }
        }

        public void Dispose()
        {
            _buffer.Changed -= OnBufferChanged;
            AssetIndex.IndexUpdated -= OnIndexUpdated;
            _timer.Dispose();
        }
    }
}
