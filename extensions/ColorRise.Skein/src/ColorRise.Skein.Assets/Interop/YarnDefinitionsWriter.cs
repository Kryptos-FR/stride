using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorRise.Skein.Assets.Interop;

/// <summary>
/// Writes the definitions file of a game's commands and functions, which Yarn Spinner's tools read to complete and
/// check them in scripts.
/// </summary>
public static class YarnDefinitionsWriter
{
    /// <summary>
    /// The extension of the file, with its dots.
    /// </summary>
    public const string FileExtension = ".ysls.json";

    /// <summary>
    /// The version of the format the file says it has.
    /// </summary>
    public const int FileVersion = 2;

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        IndentSize = 4,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Gives the text of the definitions file for the actions of a game, ordered by name.
    /// </summary>
    public static string Write(IReadOnlyList<YarnActionDescription> actions)
    {
        var commands = new JsonArray();
        var functions = new JsonArray();
        foreach (var action in actions.OrderBy(a => a.Name, StringComparer.Ordinal).ThenBy(a => a.Method, StringComparer.Ordinal))
        {
            var entry = new JsonObject
            {
                ["yarnName"] = action.Name,
                ["definitionName"] = action.Method,
                ["language"] = "csharp",
            };
            if (!string.IsNullOrEmpty(action.Documentation))
            {
                entry["documentation"] = action.Documentation;
            }

            entry["async"] = action.IsAsync;
            entry["parameters"] = Parameters(action);

            if (action.Kind == YarnActionKind.Function)
            {
                entry["return"] = new JsonObject { ["type"] = action.ReturnType ?? "any" };
                functions.Add(entry);
            }
            else
            {
                commands.Add(entry);
            }
        }

        var root = new JsonObject
        {
            ["version"] = FileVersion,
            ["commands"] = commands,
            ["functions"] = functions,
        };
        return root.ToJsonString(Indented) + "\n";
    }

    private static JsonArray Parameters(YarnActionDescription action)
    {
        var parameters = new JsonArray();

        // The object a command runs on is the first word scripts write after its name
        if (action.TargetType is { } target)
        {
            parameters.Add(new JsonObject
            {
                ["name"] = "target",
                ["type"] = "instance",
                ["subtype"] = target,
                ["documentation"] = $"The name of the {target} the command runs on.",
            });
        }

        foreach (var parameter in action.Parameters)
        {
            var entry = new JsonObject
            {
                ["name"] = parameter.Name,
                ["type"] = parameter.Type,
            };
            if (parameter.Choices.Count > 0)
            {
                entry["documentation"] = $"One of: {string.Join(", ", parameter.Choices)}.";
            }

            // The tools take a parameter as optional when it has a default value, whatever it is
            if (parameter.IsOptional)
            {
                entry["defaultValue"] = parameter.DefaultValue ?? "";
            }

            if (parameter.IsParams)
            {
                entry["isParamsArray"] = true;
            }

            parameters.Add(entry);
        }

        return parameters;
    }
}
