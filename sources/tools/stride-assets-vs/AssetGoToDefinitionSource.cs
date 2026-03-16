#pragma warning disable CS0649 // MEF fields assigned by composition container

using System;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace StrideAssets.VisualStudio
{
    // ─── Shared navigation helpers ───────────────────────────────────────────

    internal static class AssetNavigation
    {
        internal static readonly Regex GuidRegex = new Regex(
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.Compiled);

        /// <summary>
        /// Finds a GUID at <paramref name="col"/> in <paramref name="lineText"/> and navigates
        /// to its definition. Returns true if navigation was performed.
        /// Must be called on the UI thread.
        /// </summary>
        internal static bool TryNavigate(string lineText, int col)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // 1. Type tag: !TypeName,Assembly → C# source via Roslyn
            if (CSharpNavigation.TryNavigate(lineText, col))
                return true;

            // 2. GUID → asset file
            foreach (Match m in GuidRegex.Matches(lineText))
            {
                if (col < m.Index || col > m.Index + m.Length)
                    continue;

                var guid = m.Value.ToLowerInvariant();

                var asset = AssetIndex.Instance.LookupGuid(guid);
                if (asset != null) { NavigateTo(asset.FilePath, 0); return true; }

                var part = AssetIndex.Instance.LookupPart(guid);
                if (part != null) { NavigateTo(part.FilePath, part.Line - 1); return true; }

                break; // GUID at cursor but not in index
            }
            return false;
        }

        internal static void NavigateTo(string filePath, int line)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var sp = ServiceProvider.GlobalProvider;
            VsShellUtilities.OpenDocument(sp, filePath, VSConstants.LOGVIEWID.Code_guid,
                out _, out _, out var frame);

            var view = VsShellUtilities.GetTextView(frame);
            if (view != null)
            {
                view.SetCaretPos(line, 0);
                view.EnsureSpanVisible(new TextSpan
                {
                    iStartLine = line, iStartIndex = 0,
                    iEndLine = line, iEndIndex = 0
                });
            }
            frame?.Show();
        }
    }

    // ─── F12 / "Go to Definition" command filter ─────────────────────────────

    [Export(typeof(ITextViewCreationListener))]
    [ContentType("stride-asset")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class AssetGoToDefinitionProvider : ITextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService { get; set; } = null!;

        public void TextViewCreated(ITextView textView)
        {
            if (textView is not IWpfTextView wpfView) return;
            var vsView = AdapterService.GetViewAdapter(wpfView);
            if (vsView == null) return;

            var filter = new AssetGoToDefinitionFilter(wpfView);
            vsView.AddCommandFilter(filter, out var next);
            filter.NextTarget = next;
        }
    }

    internal sealed class AssetGoToDefinitionFilter : IOleCommandTarget
    {
        private readonly IWpfTextView _view;
        public IOleCommandTarget? NextTarget { get; set; }

        public AssetGoToDefinitionFilter(IWpfTextView view) => _view = view;

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97)
            {
                for (uint i = 0; i < cCmds; i++)
                {
                    if ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID == VSConstants.VSStd97CmdID.GotoDefn)
                    {
                        prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                        return VSConstants.S_OK;
                    }
                }
            }
            return NextTarget?.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText)
                ?? VSConstants.S_OK;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97
                && (VSConstants.VSStd97CmdID)nCmdID == VSConstants.VSStd97CmdID.GotoDefn)
            {
                var caretPos = _view.Caret.Position.BufferPosition;
                var line = caretPos.GetContainingLine();
                var lineText = line.GetText();
                var col = caretPos.Position - line.Start.Position;
                if (AssetNavigation.TryNavigate(lineText, col)
                    || CSharpNavigation.TryNavigateToProperty(lineText, col, caretPos.Snapshot, line.LineNumber))
                    return VSConstants.S_OK;
            }
            return NextTarget?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut)
                ?? VSConstants.S_OK;
        }
    }

    // ─── Ctrl+click mouse processor ──────────────────────────────────────────

    [Export(typeof(IMouseProcessorProvider))]
    [ContentType("stride-asset")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [Name("Stride Asset Ctrl+Click")]
    internal sealed class AssetMouseProcessorProvider : IMouseProcessorProvider
    {
        public IMouseProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            // Store HoverLinkState in the buffer so both this processor and the tagger
            // (created independently by MEF) can share it via buffer.Properties.
            var state = wpfTextView.TextBuffer.Properties.GetOrCreateSingletonProperty(
                typeof(HoverLinkState), () => new HoverLinkState());
            return new AssetMouseProcessor(wpfTextView, state);
        }
    }

    internal sealed class AssetMouseProcessor : MouseProcessorBase
    {
        private readonly IWpfTextView _view;
        private readonly HoverLinkState _hoverState;

        public AssetMouseProcessor(IWpfTextView view, HoverLinkState hoverState)
        {
            _view = view;
            _hoverState = hoverState;
        }

        public override void PreprocessMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                UpdateHover(e.GetPosition(_view.VisualElement));
            else
                ClearHover();
        }

        public override void PostprocessMouseLeave(System.Windows.Input.MouseEventArgs e)
            => ClearHover();

        public override void PostprocessMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

            var point = e.GetPosition(_view.VisualElement);
            var bufferPos = GetBufferPosition(point);
            if (bufferPos == null) return;

            var textLine = bufferPos.Value.GetContainingLine();
            var col = bufferPos.Value.Position - textLine.Start.Position;

            ThreadHelper.ThrowIfNotOnUIThread();
            var lineText2 = textLine.GetText();
            if (AssetNavigation.TryNavigate(lineText2, col)
                || CSharpNavigation.TryNavigateToProperty(lineText2, col, bufferPos.Value.Snapshot, textLine.LineNumber))
                e.Handled = true;
        }

        private void UpdateHover(System.Windows.Point point)
        {
            var bufferPos = GetBufferPosition(point);
            if (bufferPos == null) { ClearHover(); return; }

            var textLine = bufferPos.Value.GetContainingLine();
            var lineText = textLine.GetText();
            var col = bufferPos.Value.Position - textLine.Start.Position;

            foreach (System.Text.RegularExpressions.Match m in AssetNavigation.GuidRegex.Matches(lineText))
            {
                if (col < m.Index || col > m.Index + m.Length) continue;

                var guid = m.Value.ToLowerInvariant();
                bool inIndex = AssetIndex.Instance.LookupGuid(guid) != null
                            || AssetIndex.Instance.LookupPart(guid) != null;
                if (inIndex)
                {
                    var span = new SnapshotSpan(bufferPos.Value.Snapshot,
                        textLine.Start.Position + m.Index, m.Length);
                    _hoverState.Update(span);
                    _view.VisualElement.Cursor = System.Windows.Input.Cursors.Hand;
                    return;
                }
                break;
            }

            // Check for type tag: !TypeName,Assembly — only underline if the assembly is a local project
            foreach (System.Text.RegularExpressions.Match m in CSharpNavigation.TypeTagRegex.Matches(lineText))
            {
                if (col < m.Index || col > m.Index + m.Length) continue;
                if (!CSharpNavigation.IsLocalAssembly(m.Groups[3].Value)) break;
                var span = new SnapshotSpan(bufferPos.Value.Snapshot,
                    textLine.Start.Position + m.Index, m.Length);
                _hoverState.Update(span);
                _view.VisualElement.Cursor = System.Windows.Input.Cursors.Hand;
                return;
            }

            // Check for YAML property name — only underline if the enclosing type is a local project
            var pm = CSharpNavigation.PropertyNameRegex.Match(lineText);
            if (pm.Success)
            {
                var nameGroup = pm.Groups[1];
                if (col >= nameGroup.Index && col <= nameGroup.Index + nameGroup.Length
                    && nameGroup.Value != "Id"
                    && CSharpNavigation.TryGetEnclosingTypeTag(bufferPos.Value.Snapshot, textLine.LineNumber,
                        lineText, out _, out var propAssembly)
                    && CSharpNavigation.IsLocalAssembly(propAssembly))
                {
                    var span = new SnapshotSpan(bufferPos.Value.Snapshot,
                        textLine.Start.Position + nameGroup.Index, nameGroup.Length);
                    _hoverState.Update(span);
                    _view.VisualElement.Cursor = System.Windows.Input.Cursors.Hand;
                    return;
                }
            }

            ClearHover();
        }

        private void ClearHover()
        {
            _hoverState.Update(null);
            _view.VisualElement.Cursor = null; // restore I-beam
        }

        private SnapshotPoint? GetBufferPosition(System.Windows.Point point)
        {
            var line = _view.TextViewLines.GetTextViewLineContainingYCoordinate(
                point.Y + _view.ViewportTop);
            return line?.GetBufferPositionFromXCoordinate(point.X + _view.ViewportLeft);
        }
    }
}
