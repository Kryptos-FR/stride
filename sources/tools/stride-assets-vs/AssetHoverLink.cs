#pragma warning disable CS0649 // MEF fields assigned by composition container

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace StrideAssets.VisualStudio
{
    // ─── Shared per-view hover state ─────────────────────────────────────────

    /// <summary>
    /// Stored in ITextView.Properties so the mouse processor and tagger can share it.
    /// </summary>
    internal sealed class HoverLinkState
    {
        private SnapshotSpan? _span;

        public SnapshotSpan? HoveredSpan => _span;
        public event EventHandler? SpanChanged;

        public void Update(SnapshotSpan? span)
        {
            if (_span == span) return;
            _span = span;
            SpanChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ─── Classification type + format ────────────────────────────────────────

    internal static class GuidLinkClassificationType
    {
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("stride-guid-link")]
        internal static ClassificationTypeDefinition? Definition;
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "stride-guid-link")]
    [Name("stride-guid-link")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class GuidLinkFormatDefinition : ClassificationFormatDefinition
    {
        public GuidLinkFormatDefinition()
        {
            DisplayName = "Stride GUID Link";
            ForegroundColor = Color.FromRgb(0x56, 0x9C, 0xD6); // VS blue
            TextDecorations = new System.Windows.TextDecorationCollection
            {
                System.Windows.TextDecorations.Underline
            };
        }
    }

    // ─── Tagger ──────────────────────────────────────────────────────────────

    [Export(typeof(ITaggerProvider))]
    [ContentType("stride-asset")]
    [TagType(typeof(ClassificationTag))]
    internal sealed class AssetHoverLinkTaggerProvider : ITaggerProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry { get; set; } = null!;

        public ITagger<T>? CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            // The tagger needs the view to access HoverLinkState, but ITaggerProvider
            // only receives the buffer. We defer to a buffer property so the mouse
            // processor (which has the view) can push state in when it's created.
            return buffer.Properties.GetOrCreateSingletonProperty(
                typeof(AssetHoverLinkTagger),
                () => new AssetHoverLinkTagger(buffer, ClassificationRegistry)) as ITagger<T>;
        }
    }

    internal sealed class AssetHoverLinkTagger : ITagger<ClassificationTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly ClassificationTag _tag;
        private HoverLinkState? _state;

        public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

        public AssetHoverLinkTagger(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            _buffer = buffer;
            var type = registry.GetClassificationType("stride-guid-link");
            _tag = new ClassificationTag(type);
        }

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            // Lazily attach to HoverLinkState once the mouse processor has created it.
            if (_state == null && _buffer.Properties.TryGetProperty(
                    typeof(HoverLinkState), out HoverLinkState found))
            {
                _state = found;
                _state.SpanChanged += OnSpanChanged;
            }

            var hovered = _state?.HoveredSpan;
            if (hovered == null) yield break;

            foreach (var span in spans)
            {
                if (span.IntersectsWith(hovered.Value))
                    yield return new TagSpan<ClassificationTag>(hovered.Value, _tag);
            }
        }

        private void OnSpanChanged(object sender, EventArgs e)
        {
            var span = _state?.HoveredSpan;
            if (span.HasValue)
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span.Value));
            else
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                    new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
        }
    }
}
