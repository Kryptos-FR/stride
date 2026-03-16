#pragma warning disable CS0649 // MEF fields assigned by composition container

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Utilities;

namespace StrideAssets.VisualStudio
{
    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [Name("Stride Asset Quick Info Provider")]
    [ContentType("stride-asset")]
    [Order]
    internal sealed class StrideAssetQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        public IAsyncQuickInfoSource? TryCreateQuickInfoSource(ITextBuffer textBuffer)
            => new StrideAssetQuickInfoSource(textBuffer);
    }

    internal sealed class StrideAssetQuickInfoSource : IAsyncQuickInfoSource
    {
        private static readonly Regex GuidRegex = new Regex(
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.Compiled);

        private readonly ITextBuffer _buffer;

        public StrideAssetQuickInfoSource(ITextBuffer buffer) => _buffer = buffer;

        public Task<QuickInfoItem?> GetQuickInfoItemAsync(
            IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            var triggerPoint = session.GetTriggerPoint(_buffer.CurrentSnapshot);
            if (triggerPoint == null)
                return Task.FromResult<QuickInfoItem?>(null);

            var snapshot = _buffer.CurrentSnapshot;
            var line = triggerPoint.Value.GetContainingLine();
            var lineText = line.GetText();
            var col = triggerPoint.Value.Position - line.Start.Position;

            foreach (Match m in GuidRegex.Matches(lineText))
            {
                if (col < m.Index || col > m.Index + m.Length)
                    continue;

                var guid = m.Value.ToLowerInvariant();
                var span = snapshot.CreateTrackingSpan(
                    new Span(line.Start.Position + m.Index, m.Length),
                    SpanTrackingMode.EdgeInclusive);

                var asset = AssetIndex.Instance.LookupGuid(guid);
                if (asset != null)
                    return Task.FromResult<QuickInfoItem?>(
                        new QuickInfoItem(span, BuildAssetContent(asset)));

                var part = AssetIndex.Instance.LookupPart(guid);
                if (part != null)
                    return Task.FromResult<QuickInfoItem?>(
                        new QuickInfoItem(span, BuildPartContent(part)));

                break; // GUID found at cursor but not indexed — no tooltip
            }

            return Task.FromResult<QuickInfoItem?>(null);
        }

        public void Dispose() { }

        private static object BuildAssetContent(AssetEntry entry)
        {
            return new ContainerElement(
                ContainerElementStyle.Stacked,
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, entry.TypeName),
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.WhiteSpace, "    "),
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier,
                        Path.GetFileName(entry.FilePath))
                ),
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, entry.Id)
                ),
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.String, entry.FilePath)
                )
            );
        }

        private static object BuildPartContent(AssetPartEntry part)
        {
            var label = part.Name != null ? $"Entity: {part.Name}" : "Entity";
            return new ContainerElement(
                ContainerElementStyle.Stacked,
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, label),
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.WhiteSpace, "  (in "),
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Identifier,
                        Path.GetFileName(part.FilePath)),
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.WhiteSpace,
                        $", line {part.Line})")
                ),
                new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Comment, part.Id)
                )
            );
        }
    }
}
