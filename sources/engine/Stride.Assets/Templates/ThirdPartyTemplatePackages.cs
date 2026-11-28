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

namespace Stride.Assets.Templates;

/// <summary>
/// A third-party template package found by <see cref="ThirdPartyTemplatePackages.ScanAsync"/>.
/// </summary>
/// <param name="Package">The package <see cref="TemplatePackageRule"/> selected.</param>
/// <param name="Installed">Whether the package is installed in the registry (its templates are shown).</param>
/// <param name="ConflictIdentity">The identity of the template that kept the package out, or null.</param>
/// <param name="ConflictPackage">The id of the package that serves that identity already, or null.</param>
/// <param name="TemplateIdentities">The identities of the package's templates, when it is installed.</param>
/// <param name="Diagnostics">The errors of a failed install.</param>
/// <param name="GameTemplates">The number of its game templates, when it is installed.</param>
/// <param name="AssetPacks">The number of its asset packs (item templates), when it is installed.</param>
public sealed record ThirdPartyTemplatePackage(
    TemplatePackageCandidate Package, bool Installed, string? ConflictIdentity, string? ConflictPackage,
    IReadOnlyList<string> TemplateIdentities, IReadOnlyList<string> Diagnostics, int GameTemplates = 0, int AssetPacks = 0);

/// <summary>
/// Keeps the third-party template packages of a template registry equal to what <see cref="TemplatePackageRule"/>
/// selects among the installed packages: installs the selected ones, removes the ones that are no longer selected,
/// and leaves out a package whose template identity another package serves (see
/// <see cref="DotNetNewTemplateRegistry.InstallThirdPartyPackageAsync"/>). The packages of the host itself (Stride's)
/// are never removed.
/// </summary>
/// <param name="registry">The registry to keep.</param>
/// <param name="engine">The engine version of the host.</param>
/// <param name="pinnedIds">The ids the host resolves itself, never discovered.</param>
/// <param name="builtInSources">The install sources of the host's own packages at the time of a scan.</param>
public sealed class ThirdPartyTemplatePackages(
    DotNetNewTemplateRegistry registry, PackageVersion engine, IReadOnlySet<string> pinnedIds, Func<IReadOnlyCollection<string>> builtInSources)
{
    /// <summary>The result of the last scan; empty before the first.</summary>
    public IReadOnlyList<ThirdPartyTemplatePackage> Packages { get; private set; } = [];

    /// <summary>
    /// Applies the rule to <paramref name="installed"/> and brings the registry in line with it. The packages are
    /// installed in id order, so of two packages with one template identity the first by id stays.
    /// </summary>
    /// <param name="installed">The installed packages (the store listing).</param>
    /// <param name="cancellationToken">Stops the scan between two registry calls.</param>
    /// <returns>One entry per selected package, in id order.</returns>
    public async Task<IReadOnlyList<ThirdPartyTemplatePackage>> ScanAsync(IReadOnlyList<TemplatePackageCandidate> installed, CancellationToken cancellationToken = default)
    {
        var selected = TemplatePackageRule.Select(installed, engine, pinnedIds);
        var builtIn = builtInSources();
        await registry.RemovePackagesExceptAsync([.. builtIn, .. selected.Select(candidate => candidate.InstallSource)], cancellationToken).ConfigureAwait(false);

        var result = new List<ThirdPartyTemplatePackage>();
        foreach (var candidate in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var install = await registry.InstallThirdPartyPackageAsync(candidate.InstallSource, cancellationToken).ConfigureAwait(false);
            var templates = install.Installed
                ? await registry.GetTemplatesOfAsync(candidate.InstallSource, cancellationToken).ConfigureAwait(false)
                : [];
            var assetPacks = templates.Count(template => template.TagsCollection.TryGetValue("type", out var type) && string.Equals(type, "item", StringComparison.OrdinalIgnoreCase));
            var conflictPackage = install.ConflictSource is { } source ? PackageIdOf(source, selected) : null;
            result.Add(new ThirdPartyTemplatePackage(
                candidate, install.Installed, install.ConflictIdentity, conflictPackage, templates.Select(template => template.Identity).ToList(), install.Diagnostics,
                templates.Count - assetPacks, assetPacks));
        }

        Packages = result;
        return result;
    }

    /// <summary>
    /// The id of the package mounted at <paramref name="mountPointUri"/>: a selected candidate's, else the id folder of
    /// NuGet's global packages folder layout (<c>&lt;id&gt;/&lt;version&gt;</c>, ids in lower case there).
    /// </summary>
    private static string PackageIdOf(string mountPointUri, IReadOnlyList<TemplatePackageCandidate> candidates)
    {
        var candidate = candidates.FirstOrDefault(candidate => DotNetNewTemplateRegistry.MountPointMatches(mountPointUri, candidate.InstallSource));
        if (candidate is not null)
            return candidate.Id;
        var trimmed = mountPointUri.TrimEnd('\\', '/');
        return Path.GetFileName(Path.GetDirectoryName(trimmed)) is { Length: > 0 } id ? id : trimmed;
    }
}
