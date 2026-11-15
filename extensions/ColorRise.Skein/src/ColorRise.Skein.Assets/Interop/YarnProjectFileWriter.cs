using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ColorRise.Skein.Assets.Compilation;
using Stride.Core.IO;

namespace ColorRise.Skein.Assets.Interop;

/// <summary>
/// Writes the <c>.yarnproject</c> file of a Yarn project asset, which Yarn Spinner's own tools read.
/// </summary>
/// <remarks>
/// The asset is the truth for what it holds: source and excluded files, languages and their strings files, the
/// definitions file and the severities. Every other key of a file that is already there stays as it is, so what
/// another tool keeps in the file survives. Comments of the file do not.
/// </remarks>
public static class YarnProjectFileWriter
{
    /// <summary>
    /// The extension of the file, with its dot.
    /// </summary>
    public const string FileExtension = ".yarnproject";

    /// <summary>
    /// The version of the format a new file says it has: the one of Yarn Spinner 3.
    /// </summary>
    public const int FileVersion = 4;

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        IndentSize = 4,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonDocumentOptions Lenient = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Gives the text of the project file for an asset, from the file that is there when there is one.
    /// </summary>
    /// <param name="existingJson">The text of the file as it is, or null when there is none.</param>
    /// <param name="asset">The asset the file is of.</param>
    /// <param name="projectDirectory">The folder the file is in: the paths of the strings files are written from there.</param>
    /// <param name="definitionsFile">The definitions file, as a path from the folder of the project file; empty to leave the definitions as they are.</param>
    /// <exception cref="FormatException">The file that is there is not a JSON object.</exception>
    public static string Merge(string? existingJson, YarnProjectAsset asset, string projectDirectory, string definitionsFile)
    {
        var root = Parse(existingJson);

        root["projectFileVersion"] ??= FileVersion;
        root["sourceFiles"] = List(asset.SourceFiles);
        SetOrRemove(root, "excludeFiles", asset.ExcludedFiles.Count > 0 ? List(asset.ExcludedFiles) : null);
        root["baseLanguage"] = asset.BaseLanguage;
        SetOrRemove(root, "localisation", Localisation(root["localisation"] as JsonObject, asset, projectDirectory));
        if (definitionsFile.Length > 0)
        {
            root["definitions"] = Definitions(root["definitions"], definitionsFile);
        }

        SetOrRemove(root, "compilerOptions", CompilerOptions(root["compilerOptions"] as JsonObject, asset));

        return root.ToJsonString(Indented) + "\n";
    }

    private static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonNode.Parse(json, documentOptions: Lenient) as JsonObject
                ?? throw new FormatException("The Yarn project file is not a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new FormatException($"The Yarn project file is not valid JSON: {exception.Message}", exception);
        }
    }

    private static JsonArray List(IEnumerable<string> values) => [.. values.Select(v => (JsonNode)v)];

    // Sets in place, which keeps the position of a key the file already has
    private static void SetOrRemove(JsonObject parent, string key, JsonNode? value)
    {
        if (value is null)
        {
            parent.Remove(key);
        }
        else
        {
            parent[key] = value;
        }
    }

    private static JsonObject? Localisation(JsonObject? existing, YarnProjectAsset asset, string projectDirectory)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var localisation in asset.Localisations)
        {
            if (localisation.Language.Length > 0 && localisation.Strings is { } file)
            {
                strings[localisation.Language] = file.IsAbsolute ? file.MakeRelative(new UDirectory(projectDirectory)).FullPath : file.FullPath;
            }
        }

        var result = new JsonObject();

        // The languages the file already has come first, in their order, with what else they hold
        foreach (var (language, value) in existing ?? [])
        {
            var entry = value?.DeepClone() as JsonObject ?? [];
            if (strings.Remove(language, out var path))
            {
                entry["strings"] = path;
            }
            else
            {
                entry.Remove("strings");
            }

            if (entry.Count > 0)
            {
                result[language] = entry;
            }
        }

        foreach (var localisation in asset.Localisations)
        {
            if (strings.Remove(localisation.Language, out var path))
            {
                result[localisation.Language] = new JsonObject { ["strings"] = path };
            }
        }

        return result.Count > 0 ? result : null;
    }

    // Yarn writes one definitions file as a string and several as a list
    private static JsonNode Definitions(JsonNode? existing, string definitionsFile)
    {
        var files = existing switch
        {
            JsonArray array => array.Select(v => v?.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null).OfType<string>().ToList(),
            JsonValue value when value.GetValueKind() == JsonValueKind.String => [value.GetValue<string>()],
            _ => [],
        };

        if (!files.Contains(definitionsFile, StringComparer.Ordinal))
        {
            files.Add(definitionsFile);
        }

        return files.Count == 1 ? files[0] : List(files);
    }

    private static JsonObject? CompilerOptions(JsonObject? existing, YarnProjectAsset asset)
    {
        var result = existing?.DeepClone() as JsonObject ?? [];
        var severities = new JsonObject();
        foreach (var (code, severity) in asset.DiagnosticSeverities.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            severities[code] = severity switch
            {
                YarnSeverity.Error => "Error",
                YarnSeverity.Warning => "Warning",
                YarnSeverity.Info => "Info",
                _ => "None",
            };
        }

        SetOrRemove(result, "diagnosticsSeverity", severities.Count > 0 ? severities : null);
        return result.Count > 0 ? result : null;
    }
}
