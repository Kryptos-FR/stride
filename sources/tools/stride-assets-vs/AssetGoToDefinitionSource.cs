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
                var col = caretPos.Position - line.Start.Position;
                if (AssetNavigation.TryNavigate(line.GetText(), col))
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
            => new AssetMouseProcessor(wpfTextView);
    }

    internal sealed class AssetMouseProcessor : MouseProcessorBase
    {
        private readonly IWpfTextView _view;

        public AssetMouseProcessor(IWpfTextView view) => _view = view;

        public override void PostprocessMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

            var point = e.GetPosition(_view.VisualElement);
            var line = _view.TextViewLines.GetTextViewLineContainingYCoordinate(point.Y + _view.ViewportTop);
            if (line == null) return;

            SnapshotPoint? bufferPos = line.GetBufferPositionFromXCoordinate(point.X + _view.ViewportLeft);
            if (bufferPos == null) return;

            var textLine = bufferPos.Value.GetContainingLine();
            var col = bufferPos.Value.Position - textLine.Start.Position;

            ThreadHelper.ThrowIfNotOnUIThread();
            if (AssetNavigation.TryNavigate(textLine.GetText(), col))
                e.Handled = true;
        }
    }
}
