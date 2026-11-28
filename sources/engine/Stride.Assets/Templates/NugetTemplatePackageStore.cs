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
/// <param name="download">Downloads exactly a version of an id from one source into a file (see <see cref="NugetStore.DownloadPackageFile"/>).</param>
/// <param name="addFile">Adds a package file to the global packages folder as it is (see <see cref="NugetStore.AddPackageFile"/>).</param>
/// <param name="uninstall">Uninstalls packages from the global packages folder, without running their tools.</param>
public sealed class NugetTemplatePackageStore(
    Func<IEnumerable<NugetLocalPackage>> getAllInstalled,
    Func<IReadOnlyList<string>> localFolderSources,
    Func<string, CancellationToken, Task<IEnumerable<NugetServerPackage>>> findSourcePackagesById,
    Func<string, PackageVersion, string, string, CancellationToken, Task<bool>> download,
    Func<string, PackageVersion, string, CancellationToken, Task<PackageFileAddition>> addFile,
    Func<IReadOnlyList<NugetPackage>, Task> uninstall) : ITemplatePackageStore
{
    /// <summary>The template package store over <paramref name="store"/>.</summary>
    public static NugetTemplatePackageStore For(NugetStore store)
        => new(
            store.GetAllPackagesInstalled,
            () => store.LocalFolderSources,
            store.FindSourcePackagesById,
            store.DownloadPackageFile,
            store.AddPackageFile,
            packages => store.UninstallPackages(packages, progress: null, runPackageTools: false));

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
                // A source does not say the package type: TemplatePackageInstaller.FindByIdAsync reads it from the download
                IsTemplatePackage: null,
                (package.Tags ?? string.Empty).Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries).Contains(TemplatePackageRule.TemplateTag, StringComparer.OrdinalIgnoreCase),
                TemplatePackageRule.MarkerFloor(package.Dependencies),
                GameTemplates: null, AssetPacks: null, package.Source, FilePath: null))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task DownloadAsync(string id, PackageVersion version, string source, string destinationFile, CancellationToken cancellationToken)
    {
        if (!await download(id, version, source, destinationFile, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException($"{id} {version} could not be downloaded from {source}.");
    }

    /// <inheritdoc/>
    public async Task AddFileAsync(string id, PackageVersion version, string nupkgPath, CancellationToken cancellationToken)
    {
        if (await addFile(id, version, nupkgPath, cancellationToken).ConfigureAwait(false) == PackageFileAddition.InstalledWithOtherContent)
            throw new InvalidOperationException($"{id} {version} is installed already, with other content. Remove it first to add this file.");
    }

    /// <inheritdoc/>
    public Task UninstallAsync(IReadOnlyList<TemplatePackageCandidate> installed, CancellationToken cancellationToken)
    {
        var packages = getAllInstalled()
            .Where(package => installed.Any(candidate => candidate.LocalFolderSource is null
                && candidate.Id.Equals(package.Id, StringComparison.OrdinalIgnoreCase) && candidate.Version.Equals(package.Version)))
            .Cast<NugetPackage>()
            .ToList();
        return packages.Count == 0 ? Task.CompletedTask : UninstallAndTidyAsync(packages, installed);
    }

    private async Task UninstallAndTidyAsync(IReadOnlyList<NugetPackage> packages, IReadOnlyList<TemplatePackageCandidate> installed)
    {
        await uninstall(packages).ConfigureAwait(false);
        // NuGet leaves the id folder of the global packages folder (<id>/<version>) behind, empty
        foreach (var idFolder in installed.Select(candidate => Path.GetDirectoryName(candidate.InstallSource.TrimEnd('\\', '/'))).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (idFolder is not null && Directory.Exists(idFolder) && !Directory.EnumerateFileSystemEntries(idFolder).Any())
                    Directory.Delete(idFolder);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Tidying only
            }
        }
    }
}
