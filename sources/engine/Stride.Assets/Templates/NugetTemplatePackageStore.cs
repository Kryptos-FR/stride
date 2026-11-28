// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stride.Core;
using Stride.Core.Packages;

namespace Stride.Assets.Templates;

/// <summary>
/// <see cref="ITemplatePackageStore"/> over a NuGet store: the CLI's <see cref="NugetStore"/> (<see cref="For(NugetStore)"/>), or
/// Game Studio's package store, which wraps one (see DotNetNewTemplateBridge.CreateTemplatePackageInstaller).
/// </summary>
/// <param name="getAllInstalled">Lists the installed packages, local-folder sources included.</param>
/// <param name="localFolderSources">The paths of the enabled local-folder sources.</param>
/// <param name="findSourcePackagesById">Lists the versions of an id in the configured sources.</param>
/// <param name="installFromSources">Installs exactly a version of an id from the configured sources.</param>
/// <param name="installFromFolder">Installs exactly a version of an id from the configured sources and a folder.</param>
/// <param name="uninstall">Uninstalls packages from the global packages folder.</param>
public sealed class NugetTemplatePackageStore(
    Func<IEnumerable<NugetLocalPackage>> getAllInstalled,
    Func<IReadOnlyList<string>> localFolderSources,
    Func<string, CancellationToken, Task<IEnumerable<NugetServerPackage>>> findSourcePackagesById,
    Func<string, PackageVersion, CancellationToken, Task> installFromSources,
    Func<string, PackageVersion, string, Task> installFromFolder,
    Func<IReadOnlyList<NugetPackage>, Task> uninstall) : ITemplatePackageStore
{
    /// <summary>The template package store over <paramref name="store"/>.</summary>
    public static NugetTemplatePackageStore For(NugetStore store)
        => new(
            store.GetAllPackagesInstalled,
            () => store.LocalFolderSources,
            store.FindSourcePackagesById,
            (id, version, cancellationToken) => store.InstallPackage(id, version, [], progress: null, cancellationToken),
            (id, version, folder) => RestoreWithFolderAsync(
                (outputPath, sources) => RestoreWithSources(store, id, version, outputPath, sources), id, version, folder),
            packages => store.UninstallPackages(packages, progress: null));

    /// <summary>
    /// Restores <paramref name="id"/> <paramref name="version"/> into the global packages folder with
    /// <paramref name="folder"/> as one more source, through <paramref name="restore"/> (output path, extra sources), in
    /// a temporary output folder deleted afterwards.
    /// </summary>
    public static async Task RestoreWithFolderAsync(Func<string, IReadOnlyList<string>, Task> restore, string id, PackageVersion version, string folder)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "stride-template-add", $"{id}.{version}.{Guid.NewGuid():N}");
        try
        {
            await restore(outputPath, [folder]).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (Directory.Exists(outputPath))
                    Directory.Delete(outputPath, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A temporary folder: best effort
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<TemplatePackageCandidate> GetInstalled()
    {
        var folders = localFolderSources();
        return getAllInstalled().Select(package => TemplatePackageRule.FromLocalPackage(package, folders)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TemplatePackageInfo>> FindInSourcesAsync(string id, CancellationToken cancellationToken)
    {
        var found = await findSourcePackagesById(id, cancellationToken).ConfigureAwait(false);
        return found
            .GroupBy(package => package.Version)
            .Select(group => group.First())
            .Select(package => new TemplatePackageInfo(
                package.Id, package.Version, string.Join(", ", package.Authors.Where(author => !string.IsNullOrEmpty(author))),
                package.LicenseExpression ?? package.LicenseUrl?.ToString(), package.Description ?? string.Empty,
                // A source does not say the package type: AddAsync checks it once the package is installed
                IsTemplatePackage: null,
                (package.Tags ?? string.Empty).Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries).Contains(TemplatePackageRule.TemplateTag, StringComparer.OrdinalIgnoreCase),
                TemplatePackageRule.MarkerFloor(package.Dependencies),
                GameTemplates: null, AssetPacks: null, package.Source, FilePath: null))
            .ToList();
    }

    /// <inheritdoc/>
    public Task InstallAsync(string id, PackageVersion version, string? extraSourceFolder, CancellationToken cancellationToken)
        => extraSourceFolder is null ? installFromSources(id, version, cancellationToken) : installFromFolder(id, version, extraSourceFolder);

    /// <inheritdoc/>
    public Task UninstallAsync(IReadOnlyList<TemplatePackageCandidate> installed, CancellationToken cancellationToken)
    {
        var packages = getAllInstalled()
            .Where(package => installed.Any(candidate => candidate.LocalFolderSource is null
                && candidate.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase) && candidate.Version.Equals(package.Version)))
            .Cast<NugetPackage>()
            .ToList();
        return packages.Count == 0 ? Task.CompletedTask : uninstall(packages);
    }

    // RestorePackage with extra sources is the one way to install from a folder that is not a configured source; it is
    // marked experimental (STREXP002) because its signature may still change. Its use stays here.
#pragma warning disable STREXP002
    private static Task RestoreWithSources(NugetStore store, string id, PackageVersion version, string outputPath, IReadOnlyList<string> sources)
        => store.RestorePackage(id, version, [], settingsRoot: null, outputPath, sources);
#pragma warning restore STREXP002
}
