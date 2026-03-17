using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StrideAssets.VisualStudio
{
    internal sealed class AssetEntry
    {
        public AssetEntry(string id, string typeName, string filePath, string? sourcePath)
        {
            Id = id;
            TypeName = typeName;
            FilePath = filePath;
            SourcePath = sourcePath;
        }

        public string Id { get; }
        public string TypeName { get; }
        public string FilePath { get; }
        public string? SourcePath { get; }
    }

    internal sealed class AssetPartEntry
    {
        public AssetPartEntry(string id, string filePath, int line, string? name, string? ownerName = null)
        {
            Id = id;
            FilePath = filePath;
            Line = line;
            Name = name;
            OwnerName = ownerName;
        }

        public string Id { get; }
        public string FilePath { get; }
        public int Line { get; }
        /// <summary>Entity name, set for entity-level parts.</summary>
        public string? Name { get; }
        /// <summary>Owning entity name, set for component-level parts.</summary>
        public string? OwnerName { get; }
    }

    internal sealed class BackReference
    {
        public BackReference(string sourceFilePath, int line, int column, string context)
        {
            SourceFilePath = sourceFilePath;
            Line = line;
            Column = column;
            Context = context;
        }

        public string SourceFilePath { get; }
        public int Line { get; }
        public int Column { get; }
        public string Context { get; }
    }

    internal sealed class ParsedAsset
    {
        public ParsedAsset(string id, string typeName, string? sourcePath,
            IReadOnlyList<(string guid, string name)> references,
            IReadOnlyList<AssetPartEntry> parts)
        {
            Id = id;
            TypeName = typeName;
            SourcePath = sourcePath;
            References = references;
            Parts = parts;
        }

        public string Id { get; }
        public string TypeName { get; }
        public string? SourcePath { get; }
        public IReadOnlyList<(string guid, string name)> References { get; }
        public IReadOnlyList<AssetPartEntry> Parts { get; }
    }

    /// <summary>
    /// Regex-based parser for Stride YAML asset files (.sd*).
    /// Ported from the TypeScript assetParser.ts / referencePattern.ts in stride-assets.
    /// </summary>
    internal static class AssetParser
    {
        // Top-level asset type tag: first non-empty line, e.g. "!MaterialAsset"
        private static readonly Regex TypeTagRegex = new Regex(
            @"^!([A-Za-z_][\w.]*(?:,\s*[A-Za-z_][\w.]*)?)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Top-level Id line, e.g. "Id: f16ed18b-ed49-4471-ab10-bb4ee742bdbb"
        private static readonly Regex IdLineRegex = new Regex(
            @"^\s*Id:\s+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Asset reference: GUID:AssetName (may contain spaces in asset name)
        private static readonly Regex AssetRefRegex = new Regex(
            @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}):([^\s,}\]]+(?:\s+[^\s,}\]]+)*)",
            RegexOptions.Compiled);

        // Source: path (raw resource path for textures, models, etc.)
        private static readonly Regex SourcePathRegex = new Regex(
            @"^\s*Source:\s+(.+)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private const int HeaderReadLength = 1000;

        /// <summary>
        /// Fast header parse — examines at most the first 1000 chars.
        /// Extracts type tag (line 0), Id (within first ~10 lines), optional Source.
        /// Returns null if no Id found (not a proper Stride asset).
        /// </summary>
        public static AssetEntry? ParseHeader(string content, string filePath)
        {
            var header = content.Length > HeaderReadLength
                ? content.Substring(0, HeaderReadLength)
                : content;

            var typeMatch = TypeTagRegex.Match(header);
            if (!typeMatch.Success)
                return null;

            var idMatch = IdLineRegex.Match(header);
            if (!idMatch.Success)
                return null;

            var id = idMatch.Groups[1].Value.ToLowerInvariant();
            var typeName = typeMatch.Groups[1].Value;

            string? sourcePath = null;
            var sourceMatch = SourcePathRegex.Match(header);
            if (sourceMatch.Success)
                sourcePath = sourceMatch.Groups[1].Value.Trim();

            return new AssetEntry(id, typeName, filePath, sourcePath);
        }

        /// <summary>
        /// Full file parse: extracts all asset references (excluding the asset's own Id)
        /// and all part definitions (entities/elements in composite assets).
        /// </summary>
        public static ParsedAsset? ParseFull(string content, string filePath)
        {
            var entry = ParseHeader(content, filePath);
            if (entry == null)
                return null;

            var refs = new List<(string guid, string name)>();
            foreach (Match m in AssetRefRegex.Matches(content))
            {
                var guid = m.Groups[1].Value.ToLowerInvariant();
                if (guid == entry.Id)
                    continue;
                refs.Add((guid, m.Groups[2].Value.Trim()));
            }

            var parts = ParseParts(content, filePath);

            string? sourcePath = null;
            var sourceMatch = SourcePathRegex.Match(content);
            if (sourceMatch.Success)
                sourcePath = sourceMatch.Groups[1].Value.Trim();

            return new ParsedAsset(entry.Id, entry.TypeName, sourcePath, refs, parts);
        }

        /// <summary>
        /// Extracts part definitions from composite assets (scenes, prefabs, UI pages).
        /// Parts are identified by indented Id: lines (2+ leading spaces).
        /// Looks ahead up to 5 lines for a sibling Name: field at the same indentation.
        /// </summary>
        public static List<AssetPartEntry> ParseParts(string content, string filePath)
        {
            var parts = new List<AssetPartEntry>();
            var lines = content.Split('\n');

            // Track the current entity context to assign OwnerName to component-level parts.
            // The first Id: indent seen is the entity level; deeper indents are components.
            int? entityIndent = null;
            string? currentEntityName = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Must be indented (part Id lines have 2+ leading spaces, not top-level)
                if (line.Length < 2 || line[0] != ' ' || line[1] != ' ')
                    continue;

                var idMatch = IdLineRegex.Match(line);
                if (!idMatch.Success)
                    continue;

                var partId = idMatch.Groups[1].Value.ToLowerInvariant();
                var indent = line.Length - line.TrimStart().Length;

                bool isEntity = (entityIndent == null || indent <= entityIndent);
                if (isEntity)
                {
                    entityIndent = indent;

                    // Look ahead for sibling Name: at the same indentation
                    string? name = null;
                    for (int j = i + 1; j < Math.Min(i + 6, lines.Length); j++)
                    {
                        var next = lines[j];
                        if (string.IsNullOrWhiteSpace(next))
                            continue;
                        var nextIndent = next.Length - next.TrimStart().Length;
                        if (nextIndent != indent)
                            break;
                        var trimmed = next.TrimStart();
                        if (trimmed.StartsWith("Name:", StringComparison.Ordinal))
                        {
                            name = trimmed.Substring(5).Trim();
                            break;
                        }
                        break; // different field at same indent — no Name sibling
                    }

                    currentEntityName = name;
                    parts.Add(new AssetPartEntry(partId, filePath, i + 1, name, ownerName: null));
                }
                else
                {
                    // Component-level Id — no Name: field exists in YAML for components.
                    parts.Add(new AssetPartEntry(partId, filePath, i + 1, name: null, ownerName: currentEntityName));
                }
            }

            return parts;
        }
    }
}
