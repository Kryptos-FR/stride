// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Compression;
using System.Security;
using Stride.Assets.Templates;
using Stride.Core;

namespace Stride.Launcher.Tests.Helpers;

/// <summary>
/// The packages of the template package tests: store entries, and .nupkg files written as zip archives with a nuspec
/// (what NuGet's <c>PackageArchiveReader</c> reads).
/// </summary>
internal static class TemplatePackageFixtures
{
    public static TemplatePackageCandidate Candidate(string id, string version, bool isTemplate = true, string? localFolder = null)
        => new(id, new PackageVersion(version), isTemplate, HasTemplateTag: true, new PackageVersion("4.5.0"), $"/store/{id}/{version}", localFolder, "Someone", "MIT", "Templates");

    public static string WriteNupkg(string folder, string id, string version, string packageType = "Template", string tags = "stride-template games", string? marker = "4.5.0",
        (string ShortName, string Type)[]? templates = null)
    {
        var path = Path.Combine(folder, $"{id}.{version}.nupkg");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var dependencies = marker is null ? "" : $"""<dependencies><group targetFramework="net10.0"><dependency id="Stride.Core" version="{marker}" /></group></dependencies>""";
        var nuspec = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{SecurityElement.Escape(id)}</id>
                <version>{version}</version>
                <authors>Color-Rise</authors>
                <license type="expression">MIT</license>
                <description>Templates for the tests</description>
                <tags>{tags}</tags>
                <packageTypes><packageType name="{packageType}" /></packageTypes>
                {dependencies}
              </metadata>
            </package>
            """;
        using (var writer = new StreamWriter(zip.CreateEntry($"{id}.nuspec").Open()))
            writer.Write(nuspec);
        foreach (var (shortName, type) in templates ?? [("game", "project")])
        {
            using (var writer = new StreamWriter(zip.CreateEntry($"content/{shortName}/.template.config/template.json").Open()))
                writer.Write($$"""{ "identity": "{{id}}.{{shortName}}", "name": "{{shortName}}", "shortName": "{{shortName}}", "sourceName": "MyTemplate", "classifications": ["Stride"], "tags": { "language": "C#", "type": "{{type}}" } }""");
            using (var writer = new StreamWriter(zip.CreateEntry($"content/{shortName}/MyTemplate.txt").Open()))
                writer.Write("content");
        }
        return path;
    }
}

/// <summary>
/// A package store in memory: what is installed, what the sources hold, and the installs and uninstalls asked of it.
/// </summary>
internal sealed class FakeTemplatePackageStore : ITemplatePackageStore
{
    public List<TemplatePackageCandidate> Installed { get; } = [];

    public Dictionary<string, IReadOnlyList<TemplatePackageInfo>> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The .nupkg files of the sources, by "id version": what <see cref="DownloadAsync"/> copies.</summary>
    public Dictionary<string, string> SourceFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The downloads asked of the store, as "id version".</summary>
    public List<string> Downloads { get; } = [];

    /// <summary>The package files added, with the file each came from.</summary>
    public List<(string Id, PackageVersion Version, string File)> Installs { get; } = [];

    public List<string> Uninstalls { get; } = [];

    public Func<string, PackageVersion, TemplatePackageCandidate> InstalledAs { get; set; } = (id, version) => TemplatePackageFixtures.Candidate(id, version.ToString());

    /// <summary>When set, <see cref="AddFileAsync"/> throws it (a failed install).</summary>
    public Exception? InstallFailure { get; set; }

    public IReadOnlyList<TemplatePackageCandidate> GetInstalled() => Installed;

    public Task<IReadOnlyList<TemplatePackageInfo>> FindInSourcesAsync(string id, CancellationToken cancellationToken)
        => Task.FromResult(Sources.TryGetValue(id, out var found) ? found : (IReadOnlyList<TemplatePackageInfo>)[]);

    public Task DownloadAsync(string id, PackageVersion version, string source, string destinationFile, CancellationToken cancellationToken)
    {
        Downloads.Add($"{id} {version}");
        if (!SourceFiles.TryGetValue($"{id} {version}", out var file))
            throw new InvalidOperationException($"{id} {version} could not be downloaded from {source}.");
        File.Copy(file, destinationFile);
        return Task.CompletedTask;
    }

    public Task AddFileAsync(string id, PackageVersion version, string nupkgPath, CancellationToken cancellationToken)
    {
        if (InstallFailure is { } failure)
            throw failure;
        Installs.Add((id, version, nupkgPath));
        Installed.Add(InstalledAs(id, version));
        return Task.CompletedTask;
    }

    public Task UninstallAsync(IReadOnlyList<TemplatePackageCandidate> installed, CancellationToken cancellationToken)
    {
        Uninstalls.AddRange(installed.Select(candidate => $"{candidate.Id} {candidate.Version}"));
        Installed.RemoveAll(installed.Contains);
        return Task.CompletedTask;
    }
}
