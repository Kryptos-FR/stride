#pragma warning disable VSEXTPREVIEW_CODELENS

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;

namespace StrideAssets.VisualStudio.CodeLens
{
    /// <summary>
    /// CodeLens that shows reference count for an asset/part GUID.
    /// Clicking invalidates (refreshes) the count.
    /// </summary>
    internal class StrideReferencesCodeLens : InvokableCodeLens
    {
        private readonly string _guid;
        private readonly BackReferenceScanner _scanner;

        public StrideReferencesCodeLens(string guid, BackReferenceScanner scanner)
        {
            _guid = guid;
            _scanner = scanner;
        }

        public override void Dispose()
        {
        }

        public override Task<CodeLensLabel> GetLabelAsync(
            CodeElementContext codeElementContext,
            CancellationToken token)
        {
            var refs = _scanner.GetBackRefs(_guid);
            var count = refs.Count;

            if (count == 0)
            {
                return Task.FromResult(new CodeLensLabel
                {
                    Text = "0 references",
                    Tooltip = "No other asset files reference this Id",
                });
            }

            var label = count == 1 ? "1 reference" : $"{count} references";
            var tooltip = $"{count} reference(s) to {_guid}";

            // Add first few references to tooltip
            for (int i = 0; i < refs.Count && i < 5; i++)
            {
                var r = refs[i];
                var fileName = Path.GetFileName(r.SourceFilePath);
                var context = string.IsNullOrEmpty(r.Context) ? "" : $" ({r.Context})";
                tooltip += $"\n  {fileName}:{r.Line + 1}{context}";
            }
            if (refs.Count > 5)
                tooltip += $"\n  ... and {refs.Count - 5} more";

            return Task.FromResult(new CodeLensLabel
            {
                Text = label,
                Tooltip = tooltip,
            });
        }

        public override Task ExecuteAsync(
            CodeElementContext codeElementContext,
            IClientContext clientContext,
            CancellationToken cancelToken)
        {
            // Refresh the CodeLens on click
            this.Invalidate();
            return Task.CompletedTask;
        }
    }
}
