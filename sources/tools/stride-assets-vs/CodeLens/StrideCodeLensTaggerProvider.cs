#pragma warning disable VSEXTPREVIEW_TAGGERS

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace StrideAssets.VisualStudio.CodeLens
{
    /// <summary>
    /// Provides CodeLens tags for Id: GUID lines in Stride asset files.
    /// Tags tell VS where to place CodeLens indicators.
    /// </summary>
    [VisualStudioContribution]
    internal class StrideCodeLensTaggerProvider : ExtensionPart,
        ITextViewTaggerProvider<CodeLensTag>,
        ITextViewChangedListener
    {
        private readonly object _lock = new();
        private readonly Dictionary<Uri, List<StrideCodeLensTagger>> _taggers = new();

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

        public Task<TextViewTagger<CodeLensTag>> CreateTaggerAsync(
            ITextViewSnapshot textView,
            CancellationToken cancellationToken)
        {
            Log.Debug($"[TaggerProvider] CreateTaggerAsync called for {textView.Document.Uri}");
            var tagger = new StrideCodeLensTagger(this, textView.Document.Uri);
            lock (_lock)
            {
                if (!_taggers.TryGetValue(textView.Document.Uri, out var list))
                {
                    list = new List<StrideCodeLensTagger>();
                    _taggers[textView.Document.Uri] = list;
                }
                list.Add(tagger);
            }

            Log.Debug($"[TaggerProvider] Tagger created, total taggers for this doc: {_taggers[textView.Document.Uri].Count}");
            return Task.FromResult<TextViewTagger<CodeLensTag>>(tagger);
        }

        public async Task TextViewChangedAsync(
            TextViewChangedArgs args,
            CancellationToken cancellationToken)
        {
            Log.Debug($"[TaggerProvider] TextViewChangedAsync: {args.AfterTextView.Uri}, {args.Edits.Count} edit(s)");
            var tasks = new List<Task>();
            lock (_lock)
            {
                if (_taggers.TryGetValue(args.AfterTextView.Uri, out var taggers))
                {
                    foreach (var tagger in taggers)
                    {
                        tasks.Add(tagger.OnTextViewChangedAsync(
                            args.AfterTextView, args.Edits, cancellationToken));
                    }
                }
            }

            await Task.WhenAll(tasks);
        }

        internal void RemoveTagger(Uri documentUri, StrideCodeLensTagger tagger)
        {
            lock (_lock)
            {
                if (_taggers.TryGetValue(documentUri, out var list))
                {
                    list.Remove(tagger);
                    if (list.Count == 0)
                        _taggers.Remove(documentUri);
                }
            }
        }
    }
}
