// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stride.Core;
using Stride.Core.Packages;

namespace Stride.Assets.Templates;

/// <summary>
/// What <see cref="TemplatePackageRule"/> needs to know of an installed package, read once from its nuspec.
/// </summary>
/// <param name="Id">The package id.</param>
/// <param name="Version">The package version.</param>
/// <param name="IsTemplatePackage">Whether the package declares the NuGet <c>Template</c> package type.</param>
/// <param name="HasTemplateTag">Whether the package carries the <see cref="TemplatePackageRule.TemplateTag"/> tag.</param>
/// <param name="MarkerFloor">The engine version the package was built against (its <see cref="TemplatePackageRule.MarkerDependencyId"/> dependency floor), or null when it has none.</param>
/// <param name="InstallSource">What the template registry installs: the extracted folder, or the loose .nupkg of a local-folder source.</param>
/// <param name="LocalFolderSource">The local-folder source the loose .nupkg was found in, or null when the package is in the global packages folder.</param>
/// <param name="Authors">The package authors, as the nuspec writes them.</param>
/// <param name="License">The licence expression, else the licence URL, else null.</param>
/// <param name="Description">The package description.</param>
public sealed record TemplatePackageCandidate(
    string Id, PackageVersion Version, bool IsTemplatePackage, bool HasTemplateTag, PackageVersion? MarkerFloor,
    string InstallSource, string? LocalFolderSource, string Authors, string? License, string Description);

/// <summary>
/// The rule that finds the template packages of third parties among the installed packages. Shared by source between
/// the Game Studio bridge and the stride CLI, so both hosts show the same packages for the same engine.
/// </summary>
/// <remarks>
/// A package is a candidate when it declares the NuGet <c>Template</c> type and the <see cref="TemplateTag"/> tag. Per
/// id, the newest version whose marker (its <see cref="MarkerDependencyId"/> dependency floor, stamped by
/// Stride.Templates.Common.targets) names an engine no newer than the host's is taken. Unmarked packages have unknown
/// engine compatibility, so they are a last resort: any marked match wins over a newer unmarked one (otherwise a
/// co-installed newer dev package without the marker silently hijacks the choice). The packages the engine pins
/// (Stride's own template packages) are never discovered: the host resolves them at the version the engine names.
/// </remarks>
public static class TemplatePackageRule
{
    /// <summary>
    /// The tag that scopes discovery, so a template package from an unrelated local source (e.g. the VS offline
    /// packages folder) is not picked up.
    /// </summary>
    public const string TemplateTag = "stride-template";

    /// <summary>
    /// The dependency whose floor stamps the engine version a template package was built against.
    /// </summary>
    public const string MarkerDependencyId = "Stride.Core";

    /// <summary>
    /// Whether a template package marked with <paramref name="markerFloor"/> suits <paramref name="engine"/>: its marker
    /// names an engine no newer. An unmarked package has unknown compatibility, so it never qualifies here;
    /// <see cref="Select"/> keeps its own last-resort fallback.
    /// </summary>
    public static bool IsCompatible(PackageVersion? markerFloor, PackageVersion engine)
        => markerFloor is { } floor && floor.CompareTo(engine) <= 0;

    /// <summary>
    /// The marker floor among a package's dependencies (as a source lists them), or null when it has none.
    /// </summary>
    public static PackageVersion? MarkerFloor(IEnumerable<Tuple<string, PackageVersionRange>> dependencies)
        => dependencies
            .FirstOrDefault(dependency => string.Equals(dependency.Item1, MarkerDependencyId, StringComparison.OrdinalIgnoreCase))
            ?.Item2.MinVersion;

    /// <summary>
    /// The third-party template packages to show for <paramref name="engine"/>: one per id (ids compare without
    /// case), ordered by id.
    /// </summary>
    /// <param name="candidates">The installed packages.</param>
    /// <param name="engine">The engine version of the host.</param>
    /// <param name="pinnedIds">The ids the engine resolves itself, never discovered.</param>
    public static IReadOnlyList<TemplatePackageCandidate> Select(IEnumerable<TemplatePackageCandidate> candidates, PackageVersion engine, IReadOnlySet<string> pinnedIds)
        => candidates
            .Where(candidate => candidate.IsTemplatePackage && candidate.HasTemplateTag)
            .Where(candidate => !pinnedIds.Any(pinned => string.Equals(pinned, candidate.Id, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
                group.Where(candidate => IsCompatible(candidate.MarkerFloor, engine))
                    .OrderByDescending(candidate => candidate.Version)
                    .FirstOrDefault()
                ?? group.Where(candidate => candidate.MarkerFloor is null)
                    .OrderByDescending(candidate => candidate.Version)
                    .FirstOrDefault())
            .OfType<TemplatePackageCandidate>()
            .OrderBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// The candidate for an installed package.
    /// </summary>
    /// <param name="package">The installed package.</param>
    /// <param name="localFolderSources">The enabled local-folder sources of the store, to tell a loose .nupkg's source.</param>
    public static TemplatePackageCandidate FromLocalPackage(NugetLocalPackage package, IReadOnlyList<string> localFolderSources)
    {
        // Prefer the extracted directory (the registry replaces its entry on reinstall); fall back to the loose
        // .nupkg of a not-yet-mirrored local source. An extracted package reports the .nupkg inside its folder too,
        // so tell them apart by NuGet's extraction marker.
        var extracted = File.Exists(Path.Combine(package.Path, ".nupkg.metadata"));
        var installSource = extracted ? package.Path : package.NupkgPath ?? package.Path;
        var localFolderSource = extracted ? null : localFolderSources.FirstOrDefault(source => IsUnder(package.Path, source));
        return new TemplatePackageCandidate(
            package.Id, package.Version, package.IsTemplatePackage, package.HasTag(TemplateTag), package.GetDependencyFloor(MarkerDependencyId),
            installSource, localFolderSource, string.Join(", ", package.Authors.Where(author => !string.IsNullOrEmpty(author))),
            package.LicenseExpression ?? package.LicenseUrl?.ToString(), package.Description ?? string.Empty);
    }

    private static bool IsUnder(string path, string folder)
    {
        var fullFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var fullPath = Path.GetFullPath(path);
        return fullPath.Equals(fullFolder, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(fullFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
