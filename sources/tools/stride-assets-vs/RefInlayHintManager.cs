#pragma warning disable CS0649 // MEF fields assigned by composition container

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace StrideAssets.VisualStudio
{
    // ─── Adornment layer definition ──────────────────────────────────────────

    internal static class RefHintsLayerDefinition
    {
        [Export(typeof(AdornmentLayerDefinition))]
        [Name("StrideRefHints")]
        [Order(After = PredefinedAdornmentLayers.Text)]
        internal static AdornmentLayerDefinition? Definition;
    }

    // ─── View creation listener ───────────────────────────────────────────────

    [Export(typeof(ITextViewCreationListener))]
    [ContentType("stride-asset")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class RefInlayHintViewCreationListener : ITextViewCreationListener
    {
        public void TextViewCreated(ITextView textView)
        {
            if (textView is IWpfTextView wpfView)
                new RefInlayHintManager(wpfView);
        }
    }

    // ─── Per-view manager ─────────────────────────────────────────────────────

    internal sealed class RefInlayHintManager
    {
        // Opt-in flag — defaults to true, wired to DialogPage in Phase 7.
        internal static bool Enabled = true;

        private static readonly Regex RefRegex = new Regex(
            @"ref!!\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
            RegexOptions.Compiled);

        private readonly IWpfTextView _view;
        private readonly IAdornmentLayer _layer;

        public RefInlayHintManager(IWpfTextView view)
        {
            _view = view;
            _layer = view.GetAdornmentLayer("StrideRefHints");
            view.LayoutChanged += OnLayoutChanged;
            view.Closed += OnViewClosed;
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            _view.LayoutChanged -= OnLayoutChanged;
            _view.Closed -= OnViewClosed;
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (!Enabled)
            {
                _layer.RemoveAllAdornments();
                return;
            }

            // Only re-render new or reformatted lines — preserves adornments on scroll.
            foreach (var line in e.NewOrReformattedLines)
                RenderLine(line);
        }

        private void RenderLine(ITextViewLine line)
        {
            var snapshot = line.Snapshot;
            var lineText = line.Extent.GetText();

            foreach (System.Text.RegularExpressions.Match m in RefRegex.Matches(lineText))
            {
                var guid = m.Groups[1].Value.ToLowerInvariant();

                // Entity part → Name; component part → OwnerName (enclosing entity); asset → file name.
                var part = AssetIndex.Instance.LookupPart(guid);
                string? name = part?.Name ?? part?.OwnerName;
                if (name == null)
                {
                    var asset = AssetIndex.Instance.LookupGuid(guid);
                    if (asset != null)
                        name = Path.GetFileNameWithoutExtension(asset.FilePath);
                }
                if (name == null) continue;

                // Position the hint at the right edge of the last GUID character.
                int lastCharPos = line.Start.Position + m.Index + m.Length - 1;
                if (lastCharPos >= snapshot.Length) continue;
                var lastCharPoint = new SnapshotPoint(snapshot, lastCharPos);
                if (!line.ContainsBufferPosition(lastCharPoint)) continue;

                var bounds = line.GetCharacterBounds(lastCharPoint);

                var hint = CreateHint(name);
                Canvas.SetLeft(hint, bounds.Right);
                Canvas.SetTop(hint, bounds.TextTop);

                // Anchor to the ref!! span so VS auto-removes this adornment when the text changes.
                var refSpan = new SnapshotSpan(snapshot, line.Start.Position + m.Index, m.Length);
                _layer.AddAdornment(
                    AdornmentPositioningBehavior.TextRelative,
                    refSpan,
                    null,
                    hint,
                    null);
            }
        }

        private static UIElement CreateHint(string name)
        {
            return new TextBlock
            {
                Text = $" ⟨{name}⟩",
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
    }
}
