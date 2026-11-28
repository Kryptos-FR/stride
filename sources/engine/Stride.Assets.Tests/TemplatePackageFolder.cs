// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#nullable enable
using System.Text.Json.Nodes;

namespace Stride.Assets.Tests;

/// <summary>
/// Writes an extracted template package the way NuGet's global packages folder holds one: <c>root/id/version/</c> with
/// a <c>content/&lt;shortName&gt;/.template.config/template.json</c> per template, and nothing else (a package without
/// <c>templates.sdtpls</c> still registers).
/// </summary>
internal static class TemplatePackageFolder
{
    /// <summary>One template of a package: an item template is an asset pack.</summary>
    internal readonly record struct Template(string ShortName, string Identity, bool Item = false);

    /// <summary>Writes the package and returns its folder.</summary>
    internal static string Write(string root, string id, string version, params Template[] templates)
    {
        var folder = Path.Combine(root, id.ToLowerInvariant(), version);
        foreach (var template in templates)
        {
            var configFolder = Path.Combine(folder, "content", template.ShortName, ".template.config");
            Directory.CreateDirectory(configFolder);
            var json = new JsonObject
            {
                ["$schema"] = "http://json.schemastore.org/template",
                ["author"] = "Tests",
                ["identity"] = template.Identity,
                ["name"] = template.ShortName,
                ["shortName"] = template.ShortName,
                ["sourceName"] = "MyTemplate",
                ["classifications"] = template.Item ? new JsonArray("Stride", "AssetPacks") : new JsonArray("Stride"),
                ["tags"] = new JsonObject { ["language"] = "C#", ["type"] = template.Item ? "item" : "project" },
            };
            File.WriteAllText(Path.Combine(configFolder, "template.json"), json.ToJsonString());
            File.WriteAllText(Path.Combine(folder, "content", template.ShortName, "MyTemplate.txt"), "content");
        }
        return folder;
    }
}
