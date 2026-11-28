// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.IDE;

namespace Stride.Assets.Templates;

/// <summary>
/// Thin wrapper over <see cref="Bootstrapper"/> that installs Stride template nupkgs,
/// enumerates the dotnet new templates they ship, and instantiates them on demand. Owns a single
/// in-process <see cref="Bootstrapper"/> instance whose settings live under a dedicated host
/// settings dir (so we don't pollute the user's global dotnet new installation state).
/// </summary>
/// <remarks>
/// Construction is cheap; the first call into one of the async methods triggers component load
/// + package install. <see cref="InstallPackageAsync"/> is idempotent — repeat calls with the
/// same path reinstall over the same managed package (engine version bumps work this way).
/// </remarks>
public sealed class DotNetNewTemplateRegistry : IDisposable
{
    private const string HostIdentifier = "Stride.GameStudio";

    private readonly Bootstrapper bootstrapper;
    private bool disposed;

    public DotNetNewTemplateRegistry(string hostVersion, string profileDir)
    {
        // virtualizeConfiguration=false so installed package state survives across editor
        // sessions (the user shouldn't have to reinstall on every startup). loadDefaultComponents
        // pulls in the Edge providers/installers + the RunnableProjects generator that handles
        // template.json — without it Install/Create are no-ops.
        //
        // Custom IEnvironment redirects USERPROFILE / HOME to a Stride-specific path so the
        // bootstrapper's `{UserProfile}/.templateengine/` settings tree (packages.json,
        // downloaded packages, host settings) lands under our own dir — keeping dotnet new CLI's
        // global state and ours completely isolated. The Bootstrapper API doesn't accept
        // IPathInfo directly (only EngineEnvironmentSettings does); redirecting via IEnvironment
        // is the cleanest hook into DefaultPathInfo's path derivation.
        // "dotnetcli" fallback so our host reads each template's dotnetcli.host.json (isVisible etc.).
        RemoveDuplicateEntries(profileDir);
        var host = new DefaultTemplateEngineHost(HostIdentifier, hostVersion, fallbackHostTemplateConfigNames: new[] { "dotnetcli" });
        var environment = new StrideTemplateEngineEnvironment(profileDir);
        bootstrapper = new Bootstrapper(
            host,
            virtualizeConfiguration: false,
            loadDefaultComponents: true,
            environment: environment);
    }

    /// <summary>
    /// Keeps one entry per mount point in the persisted <c>packages.json</c> (the last one, the latest install): a
    /// forced reinstall of a <c>.nupkg</c> adds an entry each time.
    /// </summary>
    private static void RemoveDuplicateEntries(string profileDir)
    {
        var packagesJson = Path.Combine(profileDir, ".templateengine", "packages.json");
        try
        {
            if (!File.Exists(packagesJson) || JsonNode.Parse(File.ReadAllText(packagesJson))?["Packages"] is not JsonArray packages)
                return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var removed = 0;
            for (var i = packages.Count - 1; i >= 0; i--)
            {
                var uri = packages[i]?["MountPointUri"]?.GetValue<string>();
                if (uri is null || seen.Add(uri))
                    continue;
                packages.RemoveAt(i);
                removed++;
            }
            if (removed > 0)
                File.WriteAllText(packagesJson, packages.Root.ToJsonString());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            // Best effort: the template engine reads the file as it is.
        }
    }

    /// <summary>
    /// Installs (or reinstalls) the template package at <paramref name="source"/>. Accepts either
    /// a path to a <c>.nupkg</c> file (handled by the NuGet installer) or a path to an extracted
    /// package directory containing <c>content/.template.config/</c> (handled by the Folder
    /// installer). Prefer the directory form for local installs — the NuGet installer's local
    /// path support has edge cases around download-from-self when pointed at the global cache.
    /// </summary>
    public async Task<(bool Success, IReadOnlyList<string> Diagnostics)> InstallPackageAsync(
        string source, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(source) && !Directory.Exists(source))
            return (false, new[] { $"Package source not found: {source}" });

        // Force=true so dev rebuilds of a Stride templates package (same version, fresh content)
        // overwrite the bootstrapper's persisted state instead of being silently skipped as
        // AlreadyInstalled. ~hundreds of ms on startup; cheap insurance against stale templates.
        var request = new InstallRequest(identifier: source, force: true);
        var results = await bootstrapper.InstallTemplatePackagesAsync(
            new[] { request },
            InstallationScope.Global,
            cancellationToken).ConfigureAwait(false);

        var diagnostics = new List<string>();
        var success = true;
        foreach (var r in results.Where(r => !r.Success))
        {
            success = false;
            diagnostics.Add($"{r.InstallRequest.PackageIdentifier}: {r.ErrorMessage ?? r.Error.ToString()}");
        }
        return (success, diagnostics);
    }

    /// <summary>
    /// Uninstalls the installed packages that none of <paramref name="sources"/> (as given to
    /// <see cref="InstallPackageAsync"/>) installed, so templates of a package version no longer resolved do not
    /// linger next to the current ones. A <c>.nupkg</c> source is matched by file name, since the registry mounts
    /// its own copy.
    /// </summary>
    public async Task RemovePackagesExceptAsync(IReadOnlyCollection<string> sources, CancellationToken cancellationToken = default)
    {
        var directories = new HashSet<string>(sources.Where(Directory.Exists).Select(s => Path.GetFullPath(s).TrimEnd('\\', '/')), StringComparer.OrdinalIgnoreCase);
        var nupkgNames = new HashSet<string>(sources.Where(File.Exists).Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase);
        var stale = (await bootstrapper.GetManagedTemplatePackagesAsync(cancellationToken).ConfigureAwait(false))
            .Where(package => !directories.Contains(package.MountPointUri.TrimEnd('\\', '/'))
                && !nupkgNames.Contains(Path.GetFileName(package.MountPointUri)))
            .ToList();
        if (stale.Count > 0)
            await bootstrapper.UninstallTemplatePackagesAsync(stale, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Installs the third-party template package at <paramref name="source"/> (kept as it is when
    /// <see cref="IsUnchangedAsync"/>), unless one of its templates has an identity of <paramref name="taken"/>: the
    /// package is then uninstalled (an earlier run may have installed it) and the result names the clash. The registry
    /// cannot hold two templates of one identity safely (instantiation finds a template by identity), and a package
    /// cannot be installed with some of its templates left out, so whoever took the identity first wins: the caller
    /// fills <paramref name="taken"/> with Stride's packages, then passes the third-party ones in id order. The
    /// identities are read from the package's files (see <see cref="ReadTemplateIdentities"/>), since the template cache
    /// keeps one template per identity.
    /// </summary>
    /// <param name="source">The package folder, or a .nupkg file.</param>
    /// <param name="taken">The identities served already, with the source that serves each; the package's own are added when it is installed.</param>
    /// <param name="cancellationToken">Stops the install.</param>
    public async Task<ThirdPartyInstallResult> InstallThirdPartyPackageAsync(string source, IDictionary<string, string> taken, CancellationToken cancellationToken = default)
    {
        var own = ReadTemplateIdentities(source);
        foreach (var identity in own)
        {
            if (!taken.TryGetValue(identity, out var otherSource))
                continue;
            await UninstallAsync(source, cancellationToken).ConfigureAwait(false);
            return new ThirdPartyInstallResult(false, identity, otherSource, []);
        }

        if (!await IsUnchangedAsync(source, cancellationToken).ConfigureAwait(false))
        {
            var (success, diagnostics) = await InstallPackageAsync(source, cancellationToken).ConfigureAwait(false);
            if (!success)
                return new ThirdPartyInstallResult(false, null, null, diagnostics);
        }
        foreach (var identity in own)
            taken.TryAdd(identity, source);
        return new ThirdPartyInstallResult(true, null, null, []);
    }

    /// <summary>
    /// The template identities <paramref name="sources"/> serve, with the source of each, for
    /// <see cref="InstallThirdPartyPackageAsync"/>.
    /// </summary>
    public static Dictionary<string, string> TakenBy(IEnumerable<string> sources)
    {
        var taken = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            foreach (var identity in ReadTemplateIdentities(source))
                taken.TryAdd(identity, source);
        }
        return taken;
    }

    /// <summary>
    /// The identities of the templates of the package at <paramref name="source"/> (a folder, or a .nupkg file): the
    /// <c>identity</c> of each <c>content/.template.config/template.json</c> and <c>content/&lt;template&gt;/.template.config/template.json</c>.
    /// A file that cannot be read is skipped: the install reports it.
    /// </summary>
    public static IReadOnlyList<string> ReadTemplateIdentities(string source)
    {
        var identities = new List<string>();
        try
        {
            if (Directory.Exists(source))
            {
                var content = Path.Combine(source, "content");
                if (!Directory.Exists(content))
                    return identities;
                foreach (var folder in Directory.EnumerateDirectories(content).Prepend(content))
                {
                    var file = Path.Combine(folder, ".template.config", "template.json");
                    if (File.Exists(file) && IdentityOf(File.ReadAllText(file)) is { } identity)
                        identities.Add(identity);
                }
            }
            else if (File.Exists(source))
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(source);
                foreach (var entry in zip.Entries)
                {
                    var parts = entry.FullName.Split('/', '\\');
                    if (parts.Length is < 3 or > 4 || !parts[0].Equals("content", StringComparison.OrdinalIgnoreCase)
                        || parts[^2] != ".template.config" || !parts[^1].Equals("template.json", StringComparison.OrdinalIgnoreCase))
                        continue;
                    using var reader = new StreamReader(entry.Open());
                    if (IdentityOf(reader.ReadToEnd()) is { } identity)
                        identities.Add(identity);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Unreadable: the install reports it
        }
        return identities;

        static string? IdentityOf(string json)
        {
            try
            {
                return JsonNode.Parse(json, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })?["identity"]?.GetValue<string>();
            }
            catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or FormatException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Whether the package at <paramref name="source"/> is installed and its .nupkg (the file itself, or the one at the
    /// top of the extracted folder) has not changed since. A folder without a .nupkg counts as unchanged once installed.
    /// </summary>
    public async Task<bool> IsUnchangedAsync(string source, CancellationToken cancellationToken = default)
    {
        var installed = (await bootstrapper.GetTemplatePackagesAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(package => MountPointMatches(package.MountPointUri, source));
        if (installed is null)
            return false;

        var nupkgFile = File.Exists(source)
            ? source
            : Directory.Exists(source) ? Directory.EnumerateFiles(source, "*.nupkg", SearchOption.TopDirectoryOnly).FirstOrDefault() : null;
        var nupkgTime = nupkgFile != null ? File.GetLastWriteTimeUtc(nupkgFile) : DateTime.MinValue;
        return installed.LastChangeTime >= nupkgTime;
    }

    private async Task UninstallAsync(string source, CancellationToken cancellationToken)
    {
        var packages = (await bootstrapper.GetManagedTemplatePackagesAsync(cancellationToken).ConfigureAwait(false))
            .Where(package => MountPointMatches(package.MountPointUri, source))
            .ToList();
        if (packages.Count > 0)
            await bootstrapper.UninstallTemplatePackagesAsync(packages, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the registry's <paramref name="mountPointUri"/> is the package installed from <paramref name="source"/>: the
    /// same folder, or, for a .nupkg (the registry mounts its own copy), the same file name.
    /// </summary>
    public static bool MountPointMatches(string mountPointUri, string source)
    {
        if (File.Exists(source))
            return string.Equals(Path.GetFileName(mountPointUri), Path.GetFileName(source), StringComparison.OrdinalIgnoreCase);
        return string.Equals(NormalizeFolder(mountPointUri), NormalizeFolder(source), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFolder(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd('\\', '/');
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.TrimEnd('\\', '/');
        }
    }

    /// <summary>Enumerates all available templates (across all installed packages).</summary>
    public async Task<IReadOnlyList<ITemplateInfo>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await bootstrapper.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
        return templates.ToList();
    }

    /// <summary>The templates of the package installed from <paramref name="source"/> (see <see cref="MountPointMatches"/>).</summary>
    public async Task<IReadOnlyList<ITemplateInfo>> GetTemplatesOfAsync(string source, CancellationToken cancellationToken = default)
        => (await GetTemplatesAsync(cancellationToken).ConfigureAwait(false))
            .Where(template => MountPointMatches(template.MountPointUri, source))
            .ToList();

    /// <summary>
    /// Returns the bootstrapper's last-deployment timestamp for the package whose identifier
    /// matches <paramref name="identifier"/>, or <c>null</c> if no such package is installed.
    /// Used to skip a redundant <see cref="InstallPackageAsync"/> when the on-disk .nupkg
    /// hasn't been touched since the last install.
    /// </summary>
    public async Task<DateTime?> GetLastChangeTimeAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var packages = await bootstrapper.GetTemplatePackagesAsync(cancellationToken).ConfigureAwait(false);
        return packages
            .FirstOrDefault(p => string.Equals(p.MountPointUri, identifier, StringComparison.OrdinalIgnoreCase))
            ?.LastChangeTime;
    }

    /// <summary>
    /// Instantiates <paramref name="template"/> into <paramref name="outputPath"/> with the user-
    /// provided <paramref name="parameters"/> (keyed by template.json symbol name). <paramref
    /// name="name"/> is the dotnet new <c>-n</c> value — substituted everywhere <c>sourceName</c>
    /// appears (i.e. anywhere the staged content has the literal "MyTemplate" placeholder).
    /// </summary>
    public Task<Microsoft.TemplateEngine.Edge.Template.ITemplateCreationResult> InstantiateAsync(
        ITemplateInfo template,
        string name,
        string outputPath,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        return bootstrapper.CreateAsync(
            template,
            name,
            outputPath,
            parameters,
            baselineName: null,
            cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        bootstrapper.Dispose();
    }
}

#nullable enable
/// <summary>
/// The outcome of <see cref="DotNetNewTemplateRegistry.InstallThirdPartyPackageAsync"/>.
/// </summary>
/// <param name="Installed">Whether the package is installed in the registry.</param>
/// <param name="ConflictIdentity">The identity of the template that kept it out, or null.</param>
/// <param name="ConflictSource">The mount point of the installed package that serves that identity, or null.</param>
/// <param name="Diagnostics">The errors of a failed install.</param>
public sealed record ThirdPartyInstallResult(bool Installed, string? ConflictIdentity, string? ConflictSource, IReadOnlyList<string> Diagnostics);
#nullable restore
