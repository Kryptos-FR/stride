#pragma warning disable VSEXTPREVIEW_CODELENS
#pragma warning disable VSEXTPREVIEW_TAGGERS

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Threading;

namespace StrideAssets.VisualStudio.CodeLens
{
    /// <summary>
    /// Tags Id: GUID lines in Stride asset files as code elements for CodeLens.
    /// </summary>
    internal class StrideCodeLensTagger : TextViewTagger<CodeLensTag>
    {
        public static readonly CodeElementKind AssetIdKind = "StrideAssetId";

        private static readonly Regex IdLineRegex = new(
            @"^(\s*)Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly StrideCodeLensTaggerProvider _provider;
        private readonly Uri _documentUri;
        private readonly AsyncSemaphore _semaphore = new(1);

        private ITextDocumentSnapshot? _currentDocument;
        private bool _needsUpdate;
        private bool _updateRunning;

        public StrideCodeLensTagger(StrideCodeLensTaggerProvider provider, Uri documentUri)
        {
            _provider = provider;
            _documentUri = documentUri;
        }

        public override void Dispose()
        {
            _provider.RemoveTagger(_documentUri, this);
            _semaphore.Dispose();
            base.Dispose();
        }

        public async Task OnTextViewChangedAsync(
            ITextViewSnapshot textView,
            IReadOnlyList<TextEdit> edits,
            CancellationToken cancellationToken)
        {
            Log.Debug($"[Tagger] OnTextViewChangedAsync: {edits.Count} edit(s)");
            if (edits.Count == 0)
                return;

            using var semReleaser = await _semaphore.EnterAsync();

            var docAfter = textView.Document;
            if (_currentDocument == null || _currentDocument.RpcContract.Version < docAfter.RpcContract.Version)
            {
                _currentDocument = docAfter;
                Log.Debug("[Tagger] Document updated, scheduling tag creation");
#pragma warning disable CS4014 // fire-and-forget is intentional
                RunCreateTagsLoopAsync();
            }
        }

        protected override async Task RequestTagsAsync(
            NormalizedTextRangeCollection requestedRanges,
            bool recalculateAll,
            CancellationToken cancellationToken)
        {
            Log.Debug($"[Tagger] RequestTagsAsync: ranges={requestedRanges.Count}, recalcAll={recalculateAll}");
            if (requestedRanges.Count == 0 || requestedRanges.TextDocumentSnapshot == null)
            {
                Log.Debug("[Tagger] RequestTagsAsync: skipped (empty ranges or null snapshot)");
                return;
            }

            using var semReleaser = await _semaphore.EnterAsync();

            if (recalculateAll ||
                _currentDocument == null ||
                _currentDocument.RpcContract.Version < requestedRanges.TextDocumentSnapshot.RpcContract.Version)
            {
                _currentDocument = _currentDocument != null &&
                    _currentDocument.RpcContract.Version >= requestedRanges.TextDocumentSnapshot.RpcContract.Version
                    ? _currentDocument
                    : requestedRanges.TextDocumentSnapshot;
                Log.Debug("[Tagger] RequestTagsAsync: scheduling tag creation");
#pragma warning disable CS4014 // fire-and-forget is intentional
                RunCreateTagsLoopAsync();
            }
        }

        private async Task RunCreateTagsLoopAsync()
        {
            _needsUpdate = true;

            if (_updateRunning)
                return;

            _updateRunning = true;
            while (true)
            {
                ITextDocumentSnapshot document;
                using (var __ = await _semaphore.EnterAsync())
                {
                    if (!_needsUpdate || _currentDocument == null)
                    {
                        _updateRunning = false;
                        return;
                    }
                    _needsUpdate = false;
                    document = _currentDocument;
                }

                await CreateTagsAsync(document);
            }
        }

        private async Task CreateTagsAsync(ITextDocumentSnapshot document)
        {
            Log.Debug($"[Tagger] CreateTagsAsync: scanning {document.Uri}, length={document.Length}");
            var tags = new List<TaggedTrackingTextRange<CodeLensTag>>();

            foreach (var line in document.Lines)
            {
                var lineText = line.Text.CopyToString();
                var match = IdLineRegex.Match(lineText);
                if (!match.Success)
                    continue;

                var guid = match.Groups[2].Value.ToLowerInvariant();
                var isPartId = match.Groups[1].Value.Length > 0;

                Log.Debug($"[Tagger] Found Id line: {(isPartId ? "part" : "asset")} {guid}");
                tags.Add(new(
                    new(document, line.Text.Start, line.Text.Length,
                        TextRangeTrackingMode.ExtendForwardAndBackward),
                    new(AssetIdKind)
                    {
                        UniqueIdentifier = guid,
                        Description = isPartId ? $"Part {guid}" : $"Asset {guid}",
                        DisplayBeforeCreatingCodeLenses = true,
                        Properties = new()
                        {
                            ["Guid"] = guid,
                            ["IsPartId"] = isPartId.ToString(),
                        },
                    }));
            }

            Log.Debug($"[Tagger] Updating {tags.Count} tag(s)");
            await this.UpdateTagsAsync(
                [new(document, 0, document.Length)],
                tags,
                CancellationToken.None);
            Log.Debug("[Tagger] UpdateTagsAsync completed");
        }
    }
}
