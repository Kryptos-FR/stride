// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using Stride.Core;

namespace Stride.Assets.Templates;

/// <summary>
/// The package store as <see cref="TemplatePackageInstaller"/> sees it: the installed packages, the packages of the
/// configured sources, installing and uninstalling. Over a NuGet store in both hosts (see
/// <see cref="NugetTemplatePackageStore"/>), over a fake in the tests.
/// </summary>
public interface ITemplatePackageStore
{
    /// <summary>The installed packages: the global packages folder, and the loose .nupkg files of the local-folder sources.</summary>
    IReadOnlyList<TemplatePackageCandidate> GetInstalled();

    /// <summary>Every version of <paramref name="id"/> in the configured sources, prereleases included.</summary>
    Task<IReadOnlyList<TemplatePackageInfo>> FindInSourcesAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads exactly <paramref name="version"/> of <paramref name="id"/> from the package source <paramref name="source"/>
    /// into <paramref name="destinationFile"/>, without installing it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The source does not have that version.</exception>
    Task DownloadAsync(string id, PackageVersion version, string source, string destinationFile, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the package file <paramref name="nupkgPath"/> (<paramref name="id"/> <paramref name="version"/>) to the global
    /// packages folder as it is: no dependency is restored and nothing in the package runs. Nothing changes when that
    /// version is installed already from the same file.
    /// </summary>
    /// <exception cref="InvalidOperationException">That version is installed already, with other content.</exception>
    Task AddFileAsync(string id, PackageVersion version, string nupkgPath, CancellationToken cancellationToken);

    /// <summary>Uninstalls <paramref name="installed"/> from the global packages folder; nothing in the packages runs.</summary>
    Task UninstallAsync(IReadOnlyList<TemplatePackageCandidate> installed, CancellationToken cancellationToken);
}

/// <summary>
/// A template package a user asked to add, before it is installed: read from a .nupkg file, or found in the sources.
/// </summary>
/// <param name="Id">The package id.</param>
/// <param name="Version">The package version.</param>
/// <param name="Authors">The authors, as the nuspec writes them.</param>
/// <param name="License">The licence expression, else the licence URL, else null.</param>
/// <param name="Description">The package description.</param>
/// <param name="IsTemplatePackage">Whether it declares the NuGet <c>Template</c> package type; null when not known yet (a source does not say).</param>
/// <param name="HasTemplateTag">Whether it has the <see cref="TemplatePackageRule.TemplateTag"/> tag.</param>
/// <param name="MarkerFloor">The engine version it was built against, or null when it names none.</param>
/// <param name="GameTemplates">The number of its game templates; null when not known before the download.</param>
/// <param name="AssetPacks">The number of its asset packs; null when not known before the download.</param>
/// <param name="Origin">Where it comes from: the file path, or the source.</param>
/// <param name="FilePath">
/// The .nupkg file it was read from: the user's file, or the download of a package of the sources; null for a package of
/// the sources not downloaded (refused before the download).
/// </param>
public sealed record TemplatePackageInfo(
    string Id, PackageVersion Version, string Authors, string? License, string Description, bool? IsTemplatePackage, bool HasTemplateTag,
    PackageVersion? MarkerFloor, int? GameTemplates, int? AssetPacks, string Origin, string? FilePath);

/// <summary>What <see cref="TemplatePackageInstaller.RemoveAsync"/> does for an id.</summary>
public enum TemplatePackageRemovalKind
{
    /// <summary>No package of that id is installed: nothing to do.</summary>
    NotInstalled,

    /// <summary>One of Stride's own template packages: not removed.</summary>
    BuiltIn,

    /// <summary>Only found in one of the user's local-folder sources, whose files Stride does not delete: not removed.</summary>
    OnlyInLocalFolder,

    /// <summary>Every version in the global packages folder is uninstalled.</summary>
    Uninstall,
}

/// <summary>
/// The removal of a template package, planned by <see cref="TemplatePackageInstaller.PlanRemoval"/> so a host can show
/// it before <see cref="TemplatePackageInstaller.RemoveAsync"/>.
/// </summary>
/// <param name="Id">The id asked for.</param>
/// <param name="Kind">What the removal does.</param>
/// <param name="Message">The text for the user: the reason nothing is removed, or what is.</param>
/// <param name="Installed">The versions to uninstall (for <see cref="TemplatePackageRemovalKind.Uninstall"/>).</param>
/// <param name="LocalFolder">The local-folder source that holds the package, when one does.</param>
public sealed record TemplatePackageRemoval(string Id, TemplatePackageRemovalKind Kind, string Message, IReadOnlyList<TemplatePackageCandidate> Installed, string? LocalFolder);

/// <summary>
/// The checks, the choice of version, the install and the removal of a third-party template package, shared by
/// Game Studio's <em>Template packages</em> window and <c>stride template</c>, so both say and do the same.
/// </summary>
/// <param name="store">The package store.</param>
/// <param name="engine">The engine version of the host.</param>
/// <param name="builtInIds">The ids of Stride's own template packages, never added nor removed here.</param>
public sealed class TemplatePackageInstaller(ITemplatePackageStore store, PackageVersion engine, IReadOnlySet<string> builtInIds)
{
    private const string TemplatePackageType = "Template";

    /// <summary>
    /// Reads a .nupkg file: its nuspec, and the number of game templates and asset packs it holds (by the <c>type</c> tag
    /// of each <c>content/&lt;template&gt;/.template.config/template.json</c>; <c>item</c> is an asset pack).
    /// </summary>
    /// <exception cref="InvalidOperationException">The file cannot be read as a package.</exception>
    public TemplatePackageInfo InspectFile(string nupkgPath)
    {
        var fullPath = Path.GetFullPath(nupkgPath);
        try
        {
            using var reader = new PackageArchiveReader(fullPath);
            var nuspec = reader.NuspecReader;
            var gameTemplates = 0;
            var assetPacks = 0;
            foreach (var file in reader.GetFiles())
            {
                var parts = file.Split('/', '\\');
                if (parts.Length != 4 || !parts[0].Equals("content", StringComparison.OrdinalIgnoreCase)
                    || parts[2] != ".template.config" || !parts[3].Equals("template.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                using var stream = reader.GetStream(file);
                if (IsItemTemplate(stream))
                    assetPacks++;
                else
                    gameTemplates++;
            }

            var marker = nuspec.GetDependencyGroups()
                .SelectMany(group => group.Packages)
                .FirstOrDefault(dependency => dependency.Id.Equals(TemplatePackageRule.MarkerDependencyId, StringComparison.OrdinalIgnoreCase))
                ?.VersionRange.MinVersion;
            var tags = (nuspec.GetTags() ?? string.Empty).Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries);
            return new TemplatePackageInfo(
                nuspec.GetId(), new PackageVersion(nuspec.GetVersion().ToString()), nuspec.GetAuthors() ?? string.Empty,
                nuspec.GetLicenseMetadata()?.License ?? NullIfEmpty(nuspec.GetLicenseUrl()), nuspec.GetDescription() ?? string.Empty,
                nuspec.GetPackageTypes().Any(type => type.Name == TemplatePackageType),
                tags.Contains(TemplatePackageRule.TemplateTag, StringComparer.OrdinalIgnoreCase),
                marker is null ? null : new PackageVersion(marker.ToString()),
                gameTemplates, assetPacks, fullPath, fullPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NuGet.Packaging.Core.PackagingException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidOperationException($"{fullPath} cannot be read as a NuGet package: {e.Message}", e);
        }
    }

    /// <summary>
    /// Finds <paramref name="id"/> in the configured sources: exactly <paramref name="version"/> when given, else the
    /// newest version whose marker is not above the engine, prereleases included (else the newest unmarked one, else the
    /// newest, which <see cref="Refusal"/> then refuses with both versions). Unless what the source lists is refused
    /// already, the package file is downloaded (not installed) and read as <see cref="InspectFile"/> reads a file, so the
    /// question names its contents and <see cref="Refusal"/> checks its type; <see cref="Discard"/> deletes the download.
    /// </summary>
    /// <exception cref="InvalidOperationException">No such id, or no such version, in the sources; or the download failed.</exception>
    public async Task<TemplatePackageInfo> FindByIdAsync(string id, PackageVersion? version, CancellationToken cancellationToken = default)
    {
        var found = await store.FindInSourcesAsync(id, cancellationToken).ConfigureAwait(false);
        if (found.Count == 0)
            throw new InvalidOperationException($"No package {id} was found in the package sources.");
        TemplatePackageInfo chosen;
        if (version is not null)
        {
            chosen = found.FirstOrDefault(package => package.Version.Equals(version))
                ?? throw new InvalidOperationException($"{id} {version} was not found in the package sources.");
        }
        else
        {
            var newest = found.OrderByDescending(package => package.Version).ToList();
            chosen = newest.FirstOrDefault(package => TemplatePackageRule.IsCompatible(package.MarkerFloor, engine))
                ?? newest.FirstOrDefault(package => package.MarkerFloor is null)
                ?? newest[0];
        }
        if (Refusal(chosen) is not null)
            return chosen;

        var folder = Path.Combine(StagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"{chosen.Id}.{chosen.Version}.nupkg");
        try
        {
            await store.DownloadAsync(chosen.Id, chosen.Version, chosen.Origin, file, cancellationToken).ConfigureAwait(false);
            return InspectFile(file) with { Origin = chosen.Origin };
        }
        catch
        {
            DeleteStaging(file);
            throw;
        }
    }

    /// <summary>
    /// Deletes the download of a package of the sources (see <see cref="FindByIdAsync"/>); nothing for a user's file. Call
    /// it once the package is added, refused or declined.
    /// </summary>
    public static void Discard(TemplatePackageInfo package)
    {
        if (package.FilePath is { } file)
            DeleteStaging(file);
    }

    /// <summary>Where <see cref="FindByIdAsync"/> downloads, one folder per download.</summary>
    private static string StagingRoot => Path.Combine(Path.GetTempPath(), "stride-template-packages");

    private static void DeleteStaging(string file)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(file));
        if (folder is null || !string.Equals(Path.GetDirectoryName(folder), Path.GetFullPath(StagingRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.Ordinal))
            return;
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A temporary folder: best effort
        }
    }

    /// <summary>
    /// Why <paramref name="package"/> cannot be added, or null when it can. A package without a marker can be added:
    /// discovery takes it as a last resort.
    /// </summary>
    public string? Refusal(TemplatePackageInfo package)
    {
        if (builtInIds.Any(id => id.Equals(package.Id, StringComparison.OrdinalIgnoreCase)))
            return $"{package.Id} is one of Stride's own template packages: it comes with Stride.";
        if (package.IsTemplatePackage == false)
            return NotTemplateType(package.Id, package.Version);
        if (!package.HasTemplateTag)
            return $"{package.Id} {package.Version} is not a Stride template package: it does not have the tag {TemplatePackageRule.TemplateTag}.";
        if (package.MarkerFloor is { } marker && !TemplatePackageRule.IsCompatible(marker, engine))
            return $"{package.Id} {package.Version} is made for Stride {marker}; this is Stride {engine}.";
        return null;
    }

    /// <summary>The question asked before <paramref name="package"/> is installed.</summary>
    public string Confirmation(TemplatePackageInfo package)
    {
        var text = new StringBuilder();
        text.AppendLine($"Install the template package {package.Id} {package.Version}?");
        text.AppendLine();
        text.AppendLine($"Authors: {(string.IsNullOrWhiteSpace(package.Authors) ? "not given" : package.Authors)}");
        text.AppendLine($"Licence: {package.License ?? "not given"}");
        text.AppendLine($"From: {package.Origin}");
        text.AppendLine(package.MarkerFloor is { } marker ? $"Made for: Stride {marker}" : "Made for: engine version unknown (the package names none)");
        if (package.GameTemplates is { } games && package.AssetPacks is { } packs)
            text.AppendLine($"Contents: {DescribeContents(games, packs)}");
        text.AppendLine();
        text.Append("Templates contain project files and code that run when a project made from them is built. Install only packages you trust.");
        return text.ToString();
    }

    /// <summary>"1 game template, 2 asset packs".</summary>
    public static string DescribeContents(int gameTemplates, int assetPacks)
        => $"{gameTemplates} game template{(gameTemplates == 1 ? "" : "s")}, {assetPacks} asset pack{(assetPacks == 1 ? "" : "s")}";

    /// <summary>
    /// Installs <paramref name="package"/> from its file (the user's, or the download of <see cref="FindByIdAsync"/>) into
    /// the global packages folder, as it is: its dependencies are not restored (the marker only names an engine version)
    /// and nothing in it runs.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The package is refused (see <see cref="Refusal"/>), has no file, or that version is installed with other content.
    /// </exception>
    public async Task AddAsync(TemplatePackageInfo package, CancellationToken cancellationToken = default)
    {
        if (Refusal(package) is { } refusal)
            throw new InvalidOperationException(refusal);
        if (package.FilePath is not { } file)
            throw new InvalidOperationException($"{package.Id} {package.Version} was not downloaded.");
        await store.AddFileAsync(package.Id, package.Version, file, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>What removing <paramref name="id"/> would do.</summary>
    public TemplatePackageRemoval PlanRemoval(string id)
    {
        if (builtInIds.Any(builtIn => builtIn.Equals(id, StringComparison.OrdinalIgnoreCase)))
            return new TemplatePackageRemoval(id, TemplatePackageRemovalKind.BuiltIn, $"{id} is one of Stride's own template packages and cannot be removed.", [], null);

        var installed = store.GetInstalled().Where(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList();
        var inStore = installed.Where(candidate => candidate.LocalFolderSource is null).OrderBy(candidate => candidate.Version).ToList();
        var localFolder = installed.Select(candidate => candidate.LocalFolderSource).FirstOrDefault(folder => folder is not null);
        if (inStore.Count == 0 && localFolder is not null)
            return new TemplatePackageRemoval(id, TemplatePackageRemovalKind.OnlyInLocalFolder, LocalFolderMessage(id, localFolder), [], localFolder);
        if (inStore.Count == 0)
            return new TemplatePackageRemoval(id, TemplatePackageRemovalKind.NotInstalled, $"{id} is not installed.", [], null);

        var versions = string.Join(", ", inStore.Select(candidate => candidate.Version.ToString()));
        var message = $"Remove {id} ({versions}) from the global packages folder?";
        if (localFolder is not null)
            message += " " + $"The copy in {localFolder} stays: Stride does not delete files from a package source.";
        return new TemplatePackageRemoval(id, TemplatePackageRemovalKind.Uninstall, message, inStore, localFolder);
    }

    /// <summary>The line of a package found in a local-folder source, which cannot be removed here.</summary>
    public static string LocalFolderMessage(string id, string folder)
        => $"{id} is found in the package source {folder}, which Stride does not change. Take its file out of that folder to remove it.";

    /// <summary>Uninstalls what <paramref name="removal"/> planned; nothing for the other kinds.</summary>
    public Task RemoveAsync(TemplatePackageRemoval removal, CancellationToken cancellationToken = default)
        => removal.Kind == TemplatePackageRemovalKind.Uninstall ? store.UninstallAsync(removal.Installed, cancellationToken) : Task.CompletedTask;

    /// <summary>The third-party template packages the rule selects among the installed ones, by id.</summary>
    public IReadOnlyList<TemplatePackageCandidate> Listed() => TemplatePackageRule.Select(store.GetInstalled(), engine, builtInIds);

    private static string NotTemplateType(string id, PackageVersion version)
        => $"{id} {version} is not a template package: it does not declare the NuGet package type {TemplatePackageType}.";

    private static bool IsItemTemplate(Stream templateJson)
    {
        try
        {
            using var document = JsonDocument.Parse(templateJson, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return document.RootElement.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Object
                && tags.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "item", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // A template.json the template engine cannot read either: counted as a game template, the engine reports it
            return false;
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
