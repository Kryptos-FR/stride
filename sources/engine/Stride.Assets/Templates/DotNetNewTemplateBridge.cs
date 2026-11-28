// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Stride.Core;
using Stride.Core.Assets;
using Stride.Core.Assets.Templates;
using Stride.Core.Diagnostics;
using Stride.Core.IO;
using Stride.Core.Packages;

namespace Stride.Assets.Templates;

/// <summary>
/// Boots the <see cref="DotNetNewTemplateRegistry"/>, installs each known Stride template
/// nupkg into it, and surfaces each contained dotnet new template as a
/// <see cref="TemplateDotNetNewDescription"/> registered with <see cref="TemplateManager"/>.
/// </summary>
/// <remarks>
/// Singleton: the registry is process-scoped so the bootstrapper's loaded components survive
/// across multiple New-Project invocations. The host settings dir lives under
/// <c>%LocalAppData%\stride\template-engine\&lt;engineVersion&gt;</c> so we don't share state
/// with the user's global <c>dotnet new</c> installation, and so side-by-side Stride versions
/// keep their own template caches.
/// </remarks>
public static class DotNetNewTemplateBridge
{
    /// <summary>
    /// Package IDs the bridge resolves on startup. <c>Stride.Templates.Games</c> (NewGame) is engine-versioned and
    /// installed with Game Studio. The others carry the content version this engine names
    /// (<see cref="ContentVersion"/>, resolved by <see cref="ContentTemplateResolver"/>): Starters and
    /// Samples are Game Studio dependencies too, AssetPacks is fetched on demand (also by
    /// <see cref="GetAssetPackTemplatesAsync"/>). A package that is neither installed nor obtainable is tolerated
    /// (per-package warning, no error).
    /// </summary>
    private static readonly string[] BundledTemplatePackageIds =
    {
        "Stride.Templates.Games",
        ContentTemplateResolver.StartersPackageId,
        ContentTemplateResolver.SamplesPackageId,
        AssetPacksPackageId,
    };

    /// <summary>
    /// Package holding the asset-pack item templates offered by the New Game flow. Probed at
    /// startup like the others; additionally fetched on demand by
    /// <see cref="GetAssetPackTemplatesAsync"/> when a game template offering packs is
    /// instantiated on a machine that doesn't have it yet.
    /// </summary>
    public const string AssetPacksPackageId = ContentTemplateResolver.AssetPacksPackageId;

    private static DotNetNewTemplateRegistry? registry;
    private static readonly object InitLock = new();

    /// <summary>Serializes content downloads (<see cref="InstallContentPackageAsync"/>).</summary>
    private static readonly SemaphoreSlim DownloadGate = new(1, 1);

    /// <summary>The download of the missing content that <see cref="RegisterProjectTemplates"/> started (guarded by <see cref="InitLock"/>).</summary>
    private static Task contentDownload = Task.CompletedTask;

    /// <summary>Cancels <see cref="contentDownload"/> while it runs, null otherwise (guarded by <see cref="InitLock"/>).</summary>
    private static CancellationTokenSource? contentDownloadCancellation;

    /// <summary>
    /// In-process singleton; null until <see cref="RegisterProjectTemplates"/> has run.
    /// Consumed by <c>DotNetNewTemplateGenerator</c> at instantiation time.
    /// </summary>
    public static DotNetNewTemplateRegistry? Registry => registry;

    /// <summary>
    /// The download of the content packages missing at <see cref="RegisterProjectTemplates"/>, which registers their templates
    /// when it is done; a completed task when none runs. It never faults: a failed or cancelled download is logged.
    /// </summary>
    public static Task ContentDownload
    {
        get
        {
            lock (InitLock)
            {
                return contentDownload;
            }
        }
    }

    /// <summary>The install sources of the built-in packages in the registry (guarded by <see cref="InitLock"/>).</summary>
    private static readonly HashSet<string> BuiltInSources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Keeps the registry's third-party packages equal to what the rule finds; null until <see cref="RegisterProjectTemplates"/>.</summary>
    private static ThirdPartyTemplatePackages? thirdParty;

    /// <summary>The scan of the third-party packages last started (guarded by <see cref="InitLock"/>).</summary>
    private static Task<IReadOnlyList<ThirdPartyTemplatePackage>> thirdPartyScan = Task.FromResult<IReadOnlyList<ThirdPartyTemplatePackage>>([]);

    /// <summary>Cancels the third-party scans (guarded by <see cref="InitLock"/>).</summary>
    private static readonly CancellationTokenSource ThirdPartyCancellation = new();

    /// <summary>
    /// The descriptions of the registered third-party templates by identity, with the install source of their package
    /// (guarded by <see cref="InitLock"/>).
    /// </summary>
    private static readonly Dictionary<string, (TemplateDotNetNewDescription Description, string Source)> ThirdPartyDescriptions = new(StringComparer.Ordinal);

    /// <summary>
    /// The scan of the third-party template packages last started: after <see cref="RegisterProjectTemplates"/>, and
    /// again by <see cref="RescanThirdPartyAsync"/>. When it ends, the templates of the packages it installed are
    /// registered with <see cref="TemplateManager"/> and the ones of packages that left are unregistered. A completed
    /// task when none ran. It never faults: a failure is logged.
    /// </summary>
    public static Task ThirdPartyScan
    {
        get
        {
            lock (InitLock)
            {
                return thirdPartyScan;
            }
        }
    }

    /// <summary>The third-party template packages the last scan found (see <see cref="ThirdPartyTemplatePackages.Packages"/>).</summary>
    public static IReadOnlyList<ThirdPartyTemplatePackage> ThirdPartyPackages => thirdParty?.Packages ?? [];

    /// <summary>Identities of the templates already registered with <see cref="TemplateManager"/> (guarded by <see cref="InitLock"/>).</summary>
    private static readonly HashSet<string> RegisteredTemplateIdentities = new(StringComparer.Ordinal);

    /// <summary>
    /// The package holding the descriptions handed to <see cref="TemplateManager"/>. Never saved to disk and never
    /// backed by a real .sdpkg: its <see cref="Package.Templates"/> is the only thing
    /// <see cref="TemplateManager.FindTemplates"/> reads. A later pass (the background content download) adds to this
    /// same package, so the descriptions of one host live in one place. FullPath must be non-null for the
    /// DistinctPackagePathComparer used in FindTemplates; the sentinel path collides with no real package.
    /// </summary>
    private static readonly Package SyntheticPackage = new() { FullPath = new UFile("Stride.DotNetNewTemplates.synthetic") };

    /// <summary>
    /// Resolves each <see cref="BundledTemplatePackageIds"/> entry installed on this machine, installs them into the
    /// shared registry, and wraps every loaded dotnet new template as a <see cref="TemplateDotNetNewDescription"/>
    /// registered with <see cref="TemplateManager"/>. Never downloads: this runs during editor startup, often on the
    /// UI thread. Starters and Samples missing locally are downloaded in the background (<see cref="ContentDownload"/>, which
    /// <see cref="CancelContentDownloadAsync"/> stops) and their templates are registered when ready (AssetPacks are fetched
    /// on demand by <see cref="GetAssetPackTemplatesAsync"/>).
    /// Tolerates missing packages — dev workflows that haven't built every template project yet still load the editor.
    /// </summary>
    public static void RegisterProjectTemplates()
    {
        var logger = GlobalLogger.GetLogger("DotNetNewTemplateBridge");
        logger.ActivateLog(LogMessageType.Info);
        var missingContent = new List<string>();
        lock (InitLock)
        {
            // Stride-owned settings tree ({profileDir}/.templateengine/...), kept out of the user's
            // global ~/.templateengine. Versioned subfolder so side-by-side Stride installs don't
            // share template state. NuGet's ~/.nuget/packages/ cache stays shared.
            var profileDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
                "stride", "template-engine", StrideVersion.NuGetVersion);
            if (registry == null)
            {
                registry = new DotNetNewTemplateRegistry(StrideVersion.NuGetVersion, profileDir);
                logger.Info($"Bootstrapper created (profileDir={profileDir}, hostVersion={StrideVersion.NuGetVersion})");
            }

            // Drop missing/stale-version entries before any registry call scans them (the scanner
            // hard-throws otherwise — e.g. a ...-dev1 mount point left over after moving to ...-dev2).
            ReconcileInstalledPackages(profileDir, logger);

            // Aggregated metadata across all installed Stride template packages. Each package
            // ships its own templates.sdtpls; we merge them so a single template-identity lookup
            // can dispatch to the right per-package metadata block at registration time. The
            // entry also carries the package's installSource so Icon/Screenshot relative paths
            // can be resolved back to absolute on-disk locations at registration time.
            var sdtplsByIdentity = new Dictionary<string, TemplateMetadataSource>();
            foreach (var packageId in BundledTemplatePackageIds)
            {
                if (!InstallBundledPackage(packageId, sdtplsByIdentity, logger)
                    && ContentTemplateResolver.IsContentPackage(packageId) && packageId != AssetPacksPackageId)
                {
                    missingContent.Add(packageId);
                }
            }

            RegisterNewTemplates(sdtplsByIdentity, logger);

            // A download already running gets the same packages
            if (missingContent.Count > 0 && contentDownload.IsCompleted)
            {
                var cancellation = new CancellationTokenSource();
                contentDownloadCancellation = cancellation;
                contentDownload = Task.Run(() => DownloadMissingContentAsync(missingContent, logger, cancellation));
            }

            // Third-party packages: the store listing reads one nuspec per installed package, so it runs on the thread
            // pool after the built-in templates are shown; their templates arrive late, as downloaded content does.
            thirdParty ??= new ThirdPartyTemplatePackages(
                registry, HostVersion, new HashSet<string>(BundledTemplatePackageIds, StringComparer.OrdinalIgnoreCase),
                () => BuiltInSources.ToList());
            StartThirdPartyScan(logger);
        }
    }

    /// <summary>
    /// Scans the third-party template packages again (after one was added or removed) and returns what it found, once
    /// their templates are registered. Empty before <see cref="RegisterProjectTemplates"/>.
    /// </summary>
    public static Task<IReadOnlyList<ThirdPartyTemplatePackage>> RescanThirdPartyAsync()
    {
        var logger = GlobalLogger.GetLogger("DotNetNewTemplateBridge");
        lock (InitLock)
        {
            return thirdParty is null ? Task.FromResult<IReadOnlyList<ThirdPartyTemplatePackage>>([]) : StartThirdPartyScan(logger);
        }
    }

    /// <summary>Starts a scan after the one running, if any. Caller holds <see cref="InitLock"/>.</summary>
    private static Task<IReadOnlyList<ThirdPartyTemplatePackage>> StartThirdPartyScan(Logger logger)
    {
        var previous = thirdPartyScan;
        var cancellationToken = ThirdPartyCancellation.Token;
        thirdPartyScan = Task.Run(() => ScanThirdPartyAsync(previous, logger, cancellationToken));
        return thirdPartyScan;
    }

    /// <summary>
    /// Lists the installed packages, brings the registry's third-party packages in line with the rule, and registers or
    /// unregisters their templates. Never faults.
    /// </summary>
    private static async Task<IReadOnlyList<ThirdPartyTemplatePackage>> ScanThirdPartyAsync(Task previous, Logger logger, CancellationToken cancellationToken)
    {
        await previous.ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var localFolderSources = PackageStore.Instance.LocalFolderSources;
            var installed = PackageStore.Instance.GetAllPackagesInstalled()
                .Select(package => TemplatePackageRule.FromLocalPackage(package, localFolderSources))
                .ToList();
            cancellationToken.ThrowIfCancellationRequested();

            lock (InitLock)
            {
                var scanTask = thirdParty!.ScanAsync(installed, cancellationToken);
                scanTask.Wait(cancellationToken);
                var packages = scanTask.Result;
                foreach (var package in packages)
                {
                    if (package.ConflictIdentity is not null)
                        logger.Warning($"{package.Package.Id} {package.Package.Version} is not shown: its template {package.ConflictIdentity} has the identity of one of {package.ConflictPackage}.");
                    foreach (var diagnostic in package.Diagnostics)
                        logger.Error($"Template install ({package.Package.Id}): {diagnostic}");
                }
                SyncThirdPartyTemplates(packages, logger);
                return packages;
            }
        }
        catch (Exception e) when (cancellationToken.IsCancellationRequested && e is OperationCanceledException or AggregateException)
        {
            logger.Info("The scan of the third-party template packages was cancelled.");
        }
        catch (Exception e)
        {
            logger.Warning($"Could not scan the third-party template packages: {e.Message}");
        }
        return ThirdPartyPackages;
    }

    /// <summary>
    /// Registers the templates of the installed third-party packages and unregisters the ones whose package left or
    /// changed. Caller holds <see cref="InitLock"/>.
    /// </summary>
    private static void SyncThirdPartyTemplates(IReadOnlyList<ThirdPartyTemplatePackage> packages, Logger logger)
    {
        var wanted = new Dictionary<string, (ITemplateInfo Template, ThirdPartyTemplatePackage Package, Dictionary<string, SdtplMetadata> Sdtpls)>(StringComparer.Ordinal);
        foreach (var package in packages.Where(package => package.Installed))
        {
            var source = package.Package.InstallSource;
            // A loose .nupkg has no folder to read metadata, icons or screenshots from: the template.json is used.
            var sdtpls = Directory.Exists(source) ? LoadAggregatedSdtpls(source, logger) : new Dictionary<string, SdtplMetadata>();
            var templatesTask = registry!.GetTemplatesOfAsync(source);
            templatesTask.Wait();
            foreach (var template in templatesTask.Result.Where(template => !IsItemTemplate(template)))
                wanted.TryAdd(template.Identity, (template, package, sdtpls));
        }

        var changed = false;
        foreach (var (identity, registered) in ThirdPartyDescriptions.ToList())
        {
            if (wanted.TryGetValue(identity, out var entry) && string.Equals(entry.Package.Package.InstallSource, registered.Source, StringComparison.OrdinalIgnoreCase))
                continue;
            SyntheticPackage.Templates.Remove(registered.Description);
            RegisteredTemplateIdentities.Remove(identity);
            ThirdPartyDescriptions.Remove(identity);
            changed = true;
        }

        foreach (var (identity, (template, package, sdtpls)) in wanted)
        {
            if (ThirdPartyDescriptions.ContainsKey(identity) || !RegisteredTemplateIdentities.Add(identity))
                continue;
            var source = package.Package.InstallSource;
            sdtpls.TryGetValue(identity, out var sdtpl);
            var description = CreateDescription(template, sdtpl is null ? null : new TemplateMetadataSource(sdtpl, source), DefaultThirdPartyGroup);
            description.SourcePackageId = package.Package.Id;
            description.FullDescription = AppendOrigin(description.FullDescription ?? description.Description, package.Package);
            SyntheticPackage.Templates.Add(description);
            ThirdPartyDescriptions[identity] = (description, source);
            changed = true;
        }

        if (changed)
        {
            logger.Info($"Third-party templates: {ThirdPartyDescriptions.Count} registered.");
            TemplateManager.RegisterPackage(SyntheticPackage);
        }
    }

    /// <summary>The group of a third-party template that names none.</summary>
    public const string DefaultThirdPartyGroup = "Other";

    /// <summary>Appends the line that says which package a third-party template comes from.</summary>
    public static string AppendOrigin(string? description, TemplatePackageCandidate package)
    {
        var origin = string.IsNullOrWhiteSpace(package.Authors)
            ? $"From {package.Id} {package.Version}."
            : $"From {package.Id} {package.Version} by {package.Authors}.";
        return string.IsNullOrWhiteSpace(description) ? origin : description.TrimEnd() + Environment.NewLine + Environment.NewLine + origin;
    }

    /// <summary>
    /// Cancels <see cref="ContentDownload"/>, the package being downloaded included, and <see cref="ThirdPartyScan"/>, and
    /// waits for them to end. Later scans are cancelled too.
    /// </summary>
    /// <returns>The download, completed once it has stopped.</returns>
    public static Task CancelContentDownloadAsync()
    {
        lock (InitLock)
        {
            contentDownloadCancellation?.Cancel();
            // Shutdown does not wait on a slow store listing either
            ThirdPartyCancellation.Cancel();
            return Task.WhenAll(contentDownload, thirdPartyScan);
        }
    }

    /// <summary>
    /// Downloads content packages (<paramref name="packageIds"/>) this machine doesn't have, then installs them into the
    /// registry and registers their templates, so they show in the New-Project dialog once ready. Best effort.
    /// </summary>
    /// <param name="cancellation">Stops the download; disposed when it ends.</param>
    private static async Task DownloadMissingContentAsync(IReadOnlyList<string> packageIds, Logger logger, CancellationTokenSource cancellation)
    {
        var cancellationToken = cancellation.Token;
        try
        {
            var fetched = new List<string>();
            for (var i = 0; i < packageIds.Count; i++)
            {
                if (await InstallContentPackageAsync(packageIds[i], logger, i + 1, packageIds.Count, cancellationToken).ConfigureAwait(false) is not null)
                    fetched.Add(packageIds[i]);
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (fetched.Count == 0)
                return;

            lock (InitLock)
            {
                var sdtplsByIdentity = new Dictionary<string, TemplateMetadataSource>();
                foreach (var packageId in fetched)
                    InstallBundledPackage(packageId, sdtplsByIdentity, logger);
                RegisterNewTemplates(sdtplsByIdentity, logger);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.Info($"The download of the template content ({string.Join(", ", packageIds)}) was cancelled.");
        }
        catch (Exception e)
        {
            logger.Warning($"Could not download the template content ({string.Join(", ", packageIds)}): {e.Message}");
        }
        finally
        {
            lock (InitLock)
            {
                if (contentDownloadCancellation == cancellation)
                    contentDownloadCancellation = null;
            }
            cancellation.Dispose();
        }
    }

    /// <summary>
    /// Wraps every registry template not registered yet as a <see cref="TemplateDotNetNewDescription"/>, in one
    /// synthetic package registered with <see cref="TemplateManager"/>. Caller holds <see cref="InitLock"/>.
    /// </summary>
    private static void RegisterNewTemplates(Dictionary<string, TemplateMetadataSource> sdtplsByIdentity, Logger logger)
    {
        var templatesTask = registry!.GetTemplatesAsync();
        templatesTask.Wait();
        var templates = templatesTask.Result;
        logger.Info($"Loaded {templates.Count} dotnet new template(s) total");

        var added = 0;
        foreach (var template in templates)
        {
            // Item templates (asset packs) are not stand-alone projects — they surface as
            // checkboxes inside the parameter dialog of templates that offer them, not as
            // entries in the New-Project list.
            if (IsItemTemplate(template))
                continue;

            // Third-party templates are registered by the third-party scan, once the rule has run: an entry left in the
            // registry by an earlier session may be a package that is no longer installed or no longer compatible.
            if (!BuiltInSources.Any(source => DotNetNewTemplateRegistry.MountPointMatches(template.MountPointUri, source)))
                continue;

            // Registered by an earlier pass (startup, then the background download).
            if (!RegisteredTemplateIdentities.Add(template.Identity))
                continue;

            // Cross-ref by template.json identity → matches the sdtpl Id (we set
            // `identity = sdtpl.Id` in the preprocessor) so the dict lookup hits.
            sdtplsByIdentity.TryGetValue(template.Identity, out var source);
            SyntheticPackage.Templates.Add(CreateDescription(template, source, "Stride"));
            added++;
        }

        // Registering the same package again signals the new descriptions to an open template list.
        if (added > 0)
            TemplateManager.RegisterPackage(SyntheticPackage);
    }

    /// <summary>
    /// Wraps <paramref name="template"/> as a <see cref="TemplateDotNetNewDescription"/>, from its <c>.sdtpl</c> metadata
    /// when <paramref name="source"/> has it, else from the template.json.
    /// </summary>
    /// <param name="defaultGroup">The group when neither the <c>.sdtpl</c> nor the template.json name one.</param>
    private static TemplateDotNetNewDescription CreateDescription(ITemplateInfo template, TemplateMetadataSource? source, string defaultGroup)
    {
        var sdtpl = source?.Metadata;
        var shortName = template.ShortNameList.FirstOrDefault();
        // Per-template content dir inside the package, e.g. <packageDir>/content/stride-fps/.
        // Icon/Screenshot relative paths in .sdtpl resolve against this dir at runtime.
        var templateContentDir = source != null && shortName != null
            ? Path.Combine(source.InstallSource, "content", shortName)
            : null;
        var description = new TemplateDotNetNewDescription
        {
            Id = sdtpl?.Id ?? TryParseGuid(template.Identity),
            Name = sdtpl?.Name ?? template.Name ?? shortName ?? template.Identity,
            Description = sdtpl?.Description ?? template.Description,
            FullDescription = sdtpl?.FullDescription,
            DefaultOutputName = sdtpl?.DefaultOutputName ?? template.DefaultName ?? "MyGame",
            Group = sdtpl?.Group ?? template.GroupIdentity ?? defaultGroup,
            Scope = (sdtpl?.Scope != null && Enum.TryParse<TemplateScope>(sdtpl.Scope, ignoreCase: true, out var parsedScope))
                ? parsedScope
                : TemplateScope.Session,
            TemplateIdentity = template.Identity,
            TemplateShortName = shortName ?? string.Empty,
            OffersAssetPacks = sdtpl?.HasParameter("assetPacks") == true,
            // FullPath must be non-null: TemplateDescriptionViewModel calls
            // Template.FullPath.GetFullDirectory() unconditionally when constructing
            // image paths. We point it at a synthetic file inside the per-template
            // content dir so the directory part is the natural relative-resolution root
            // (matches what TemplateDescription.FullPath means for real .sdpkg-backed
            // templates). Falls back to a per-identity sentinel if we couldn't resolve
            // a content dir (e.g. template not in the aggregated metadata).
            FullPath = templateContentDir != null
                ? new UFile(Path.Combine(templateContentDir, ".synthetic.sdtpl"))
                : new UFile($"{template.Identity}.synthetic"),
            Icon = templateContentDir != null ? ResolveTemplateAsset(sdtpl?.Icon, templateContentDir) : null,
        };
        if (templateContentDir != null && sdtpl != null)
        {
            foreach (var s in sdtpl.Screenshots)
            {
                var resolved = ResolveTemplateAsset(s, templateContentDir);
                if (resolved != null)
                    description.Screenshots.Add(resolved);
            }
        }
        return description;
    }

    /// <summary>
    /// The asset-pack item templates available for the New Game flow, in registry order. When
    /// none of Stride's are installed (third-party packs do not count) and <paramref name="allowDownload"/> is true, resolves the
    /// <see cref="AssetPacksPackageId"/> package at this engine's content version — downloading
    /// it from the configured NuGet sources if this machine doesn't have it — and installs it into
    /// the registry. Returns the other packs only when the package can't be obtained (e.g. offline);
    /// callers degrade by not offering Stride's.
    /// </summary>
    public static async Task<IReadOnlyList<ITemplateInfo>> GetAssetPackTemplatesAsync(bool allowDownload = true)
    {
        var logger = GlobalLogger.GetLogger("DotNetNewTemplateBridge");
        if (registry == null)
            return [];

        var packs = FilterAssetPacks(await registry.GetTemplatesAsync().ConfigureAwait(false));
        string? builtInPacks;
        lock (InitLock)
        {
            builtInPacks = assetPacksSource;
        }
        // Third-party packs alone do not stop the download of Stride's
        if (HasBuiltInAssetPacks(packs, builtInPacks) || !allowDownload)
            return packs;

        // Download first, then the same path as startup probing: install the now-local package into the registry
        // (no TemplateManager registration — item templates stay out of the New-Project list). InstallBundledPackage
        // is synchronous and reindexes the package, so it goes to the thread pool rather than the UI thread.
        var installed = await InstallContentPackageAsync(AssetPacksPackageId, logger).ConfigureAwait(false) is not null
            && await Task.Run(() =>
            {
                lock (InitLock)
                {
                    return InstallBundledPackage(AssetPacksPackageId, new Dictionary<string, TemplateMetadataSource>(), logger);
                }
            }).ConfigureAwait(false);
        if (!installed)
        {
            logger.Warning($"{AssetPacksPackageId} is unavailable; its asset packs will not be offered.");
            return packs;
        }
        return FilterAssetPacks(await registry.GetTemplatesAsync().ConfigureAwait(false));
    }

    /// <summary>
    /// Whether <paramref name="packs"/> hold packs of Stride's own <see cref="AssetPacksPackageId"/> package, installed from
    /// <paramref name="assetPacksSource"/>: packs of a third-party package do not stand for them.
    /// </summary>
    public static bool HasBuiltInAssetPacks(IEnumerable<ITemplateInfo> packs, string? assetPacksSource)
        => assetPacksSource is not null && packs.Any(pack => DotNetNewTemplateRegistry.MountPointMatches(pack.MountPointUri, assetPacksSource));

    /// <summary>The install source of the <see cref="AssetPacksPackageId"/> package in the registry, or null (guarded by <see cref="InitLock"/>).</summary>
    private static string? assetPacksSource;

    /// <summary>Asset packs = item templates classified <c>AssetPacks</c> (see the preprocessor's item-template emission).</summary>
    private static IReadOnlyList<ITemplateInfo> FilterAssetPacks(IReadOnlyList<ITemplateInfo> templates)
        => templates.Where(t => IsItemTemplate(t) && t.Classifications.Contains("AssetPacks", StringComparer.Ordinal)).ToList();

    /// <summary>True for dotnet new item templates (<c>"tags": { "type": "item" }</c>).</summary>
    private static bool IsItemTemplate(ITemplateInfo template)
        => template.TagsCollection.TryGetValue("type", out var type) && string.Equals(type, "item", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The content version this engine names (Samples, Starters, AssetPacks): the committed value of
    /// sources/templates/StrideSamplesVersion.props, embedded as a resource.
    /// </summary>
    private static PackageVersion ContentVersion => LazyContentVersion.Value;

    private static readonly Lazy<PackageVersion> LazyContentVersion = new(() =>
    {
        using var stream = typeof(DotNetNewTemplateBridge).Assembly
            .GetManifestResourceStream("StrideSamplesVersion.props")
            ?? throw new InvalidOperationException("StrideSamplesVersion.props is not embedded in Stride.Assets.");
        using var reader = new StreamReader(stream);
        var match = Regex.Match(reader.ReadToEnd(), "<_StrideCommittedSamplesVersion>([^<]+)</");
        return match.Success
            ? new PackageVersion(match.Groups[1].Value)
            : throw new InvalidOperationException("No content version in the embedded StrideSamplesVersion.props.");
    });

    /// <summary>This build's own version, the engine the content is resolved for.</summary>
    private static PackageVersion HostVersion => new(StrideVersion.NuGetVersion);

    /// <summary>
    /// The installed directory of <paramref name="packageId"/> for this build, null when it is not installed. Never
    /// downloads (<see cref="InstallContentPackageAsync"/> does): this also runs on the UI thread at startup. Content
    /// packages resolve through <see cref="ContentTemplateResolver"/> (this checkout's own pack of the content
    /// version, or exactly the content version); everything else is exactly <see cref="StrideVersion.NuGetVersion"/>.
    /// </summary>
    private static UDirectory? ResolvePackageDirectory(string packageId, Logger logger)
    {
        if (ContentTemplateResolver.IsContentPackage(packageId))
        {
            var installed = PackageStore.Instance.GetLocalPackages(packageId, ContentTemplateResolver.LocalBuildRange(ContentVersion, HostVersion));
            var package = ContentTemplateResolver.Pick(installed, ContentVersion, HostVersion);
            logger.Info(package is null
                ? $"{packageId}: content version {ContentVersion} is not installed."
                : $"{packageId}: using {package.Version} (content version {ContentVersion}).");
            return package is not null ? PackageStore.Instance.GetPackageDirectory(package) : null;
        }

        // Exact range, so release/prerelease ordering is moot.
        var exact = new PackageVersionRange(HostVersion, true, HostVersion, true);
        return PackageStore.Instance.GetPackageDirectory(packageId, exact);
    }

    /// <summary>
    /// Resolves content package <paramref name="packageId"/>, downloading it when missing, and reports the download to
    /// <see cref="TemplateDownloads"/> so the template windows can show it.
    /// </summary>
    /// <param name="index">Position of <paramref name="packageId"/> in its batch, from 1 (shown with the progress).</param>
    /// <param name="count">Number of packages in the batch.</param>
    /// <param name="cancellationToken">Stops the download; the package is then not installed.</param>
    private static async Task<NugetLocalPackage?> InstallContentPackageAsync(string packageId, Logger logger, int index = 1, int count = 1, CancellationToken cancellationToken = default)
    {
        // One content download at a time: the store reports progress for all of them together.
        await DownloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var downloading = false;
            // A content install downloads the package and at most its small engine marker dependency, both started
            // together, so the size of the started downloads is the size to show.
            void OnProgress(long downloadedBytes)
                => TemplateDownloads.Report(new TemplateDownload(packageId, downloadedBytes, PackageStore.Instance.StartedDownloadBytes, index, count));
            Task<NugetLocalPackage?> Install(string id, PackageVersion version)
            {
                downloading = true;
                TemplateDownloads.Report(new TemplateDownload(packageId, 0, 0, index, count));
                return PackageStore.Instance.InstallPackage(id, version, cancellationToken: cancellationToken);
            }

            PackageStore.Instance.DownloadProgress += OnProgress;
            NugetLocalPackage? package = null;
            try
            {
                package = await ContentTemplateResolver.ResolveAsync(
                    packageId, ContentVersion, HostVersion,
                    PackageStore.Instance.GetLocalPackages,
                    Install,
                    message => logger.Info(message)).ConfigureAwait(false);
                return package;
            }
            finally
            {
                PackageStore.Instance.DownloadProgress -= OnProgress;
                // A cancelled download is no failure to show
                if (downloading)
                    TemplateDownloads.Report(package is null && !cancellationToken.IsCancellationRequested ? new TemplateDownload(packageId, 0, 0, index, count, Failed: true) : null);
            }
        }
        finally
        {
            DownloadGate.Release();
        }
    }

    /// <summary>
    /// Drops invalid entries from the persisted <c>packages.json</c> before the bootstrapper scans
    /// it — the scanner throws (failing New-Project) on a missing mount point, and a superseded
    /// version surfaces stale templates. Keeps only entries that pass <see cref="IsValidCurrentEntry"/>;
    /// the install loop repopulates the current set. Operates on Stride's own isolated profile only.
    /// </summary>
    private static void ReconcileInstalledPackages(string profileDir, Logger logger)
    {
        var packagesJson = Path.Combine(profileDir, ".templateengine", "packages.json");
        if (!File.Exists(packagesJson))
            return;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(packagesJson));
        }
        catch (Exception e)
        {
            // A corrupt settings file crashes the scanner just like a stale entry. Reset it and let
            // the install loop rebuild from scratch.
            logger.Warning($"Template package list at {packagesJson} is unreadable ({e.Message}); resetting it.");
            try { File.Delete(packagesJson); } catch { /* best effort */ }
            return;
        }

        var packages = root?["Packages"]?.AsArray();
        if (packages is null)
            return;

        var dropped = 0;
        for (var i = packages.Count - 1; i >= 0; i--)
        {
            var uri = packages[i]?["MountPointUri"]?.GetValue<string>();
            if (uri is not null && IsValidCurrentEntry(uri))
                continue;
            logger.Info($"Reconcile: dropping stale template package entry '{uri}'.");
            packages.RemoveAt(i);
            dropped++;
        }

        if (dropped == 0)
            return;

        File.WriteAllText(packagesJson, root!.ToJsonString());
        logger.Info($"Reconcile: removed {dropped} stale entr{(dropped == 1 ? "y" : "ies")} from {packagesJson}.");
    }

    /// <summary>
    /// True when <paramref name="mountPointUri"/> exists AND — for packages we manage — its version is one this
    /// build may use (see <see cref="ResolvePackageDirectory"/>). Mount points use NuGet's global-folder layout
    /// (<c>&lt;root&gt;\&lt;id&gt;\&lt;version&gt;</c>), so id/version are the trailing two segments;
    /// unrecognized entries are left untouched.
    /// </summary>
    private static bool IsValidCurrentEntry(string mountPointUri)
    {
        if (!Directory.Exists(mountPointUri))
            return false;

        var trimmed = mountPointUri.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var versionText = Path.GetFileName(trimmed);
        var id = Path.GetFileName(Path.GetDirectoryName(trimmed) ?? string.Empty);

        var managedId = BundledTemplatePackageIds.FirstOrDefault(p => string.Equals(p, id, StringComparison.OrdinalIgnoreCase));
        if (managedId is null)
            return true;

        if (!PackageVersion.TryParse(versionText, out var version))
            return false;
        return ContentTemplateResolver.IsContentPackage(managedId)
            ? ContentTemplateResolver.IsAcceptable(version, ContentVersion, HostVersion)
            : version.Equals(HostVersion);
    }

    /// <summary>
    /// Resolves <paramref name="packageId"/> for this build (<see cref="ResolvePackageDirectory"/>), installs it
    /// into the registry (skipping if unchanged since last session), and merges its <c>templates.sdtpls</c>
    /// entries into <paramref name="sdtplsByIdentity"/>. Tolerates a missing package by logging; returns whether
    /// the package is installed in the registry.
    /// </summary>
    private static bool InstallBundledPackage(string packageId, Dictionary<string, TemplateMetadataSource> sdtplsByIdentity, Logger logger)
    {
        var packageDir = ResolvePackageDirectory(packageId, logger);
        if (packageDir is null)
        {
            // Not installed and not obtainable (a fresh checkout before the first build of Stride.Templates.Games,
            // or content that is not published yet / no package source reachable). Not fatal — the editor still
            // opens; this package's templates just won't appear in the New-Project dialog.
            logger.Warning($"{packageId} is not available; its templates will be unavailable.");
            return false;
        }
        // Install from the extracted package directory rather than the .nupkg file inside it.
        // Reasoning: pointing the bootstrapper at the .nupkg path inside NuGet's global cache
        // triggers the NuGet installer's "download" code path which then tries to "fetch" the
        // package from the same local file — and fails with "Failed to download X from X".
        // The Folder installer (selected automatically when CanInstallAsync sees a directory)
        // reads the extracted content directly, no download dance.
        var installSource = packageDir.ToOSPath();
        logger.Info($"{packageId} resolved to {installSource}");

        // Skip the install when the bootstrapper's recorded LastChangeTime is at or after the
        // .nupkg's on-disk mtime — same content as last session, no need to spend a few hundred
        // ms re-indexing. Dev rebuilds bump the .nupkg mtime and re-trigger.
        var unchangedTask = registry!.IsUnchangedAsync(installSource);
        unchangedTask.Wait();
        if (unchangedTask.Result)
        {
            logger.Info($"{packageId} unchanged since its last install; skipping reinstall.");
        }
        else
        {
            logger.Info($"Installing {packageId} from {installSource}");
            var installTask = registry.InstallPackageAsync(installSource);
            installTask.Wait();
            var (success, diagnostics) = installTask.Result;
            if (!success)
            {
                foreach (var d in diagnostics)
                    logger.Error($"Template install ({packageId}): {d}");
                return false;
            }
        }

        BuiltInSources.Add(installSource);
        if (packageId == AssetPacksPackageId)
            assetPacksSource = installSource;

        // Merge this package's aggregated metadata into the shared dict. Each entry remembers
        // the installSource it came from so Icon/Screenshot relative paths can be resolved later.
        foreach (var (id, meta) in LoadAggregatedSdtpls(installSource, logger))
            sdtplsByIdentity[id] = new TemplateMetadataSource(meta, installSource);
        return true;
    }

    /// <summary>
    /// Loads the aggregated <c>templates.sdtpls</c> shipped at the package root. Returns an
    /// empty dict if the file isn't present (older packages, dev mode before aggregator runs);
    /// callers fall back to <see cref="ITemplateInfo"/> properties in that case.
    /// </summary>
    private static Dictionary<string, SdtplMetadata> LoadAggregatedSdtpls(string installSource, Logger logger)
    {
        var aggregatedPath = Path.Combine(installSource, "content", "templates.sdtpls");
        if (!File.Exists(aggregatedPath))
        {
            logger.Warning($"Aggregated metadata not found at {aggregatedPath}; falling back to ITemplateInfo properties for display.");
            return new Dictionary<string, SdtplMetadata>();
        }
        var entries = SdtplMetadata.ParseAll(aggregatedPath);
        // Key by Id-as-string, because that's what the preprocessor stamps into template.json's
        // identity field — and identity is what dotnet new's ITemplateInfo exposes back to us.
        return entries
            .Where(e => e.Id.HasValue)
            .ToDictionary(e => e.Id!.Value.ToString(), e => e);
    }

    /// <summary>
    /// Resolves an Icon/Screenshot path declared in a <c>.sdtpl</c> file (relative to the sample
    /// dir) to an absolute on-disk path inside the installed package content. Tries the
    /// as-declared relative path first; falls back to <c>.sdtpl/&lt;basename&gt;</c> inside the
    /// per-template dir, which is where the preprocessor stashes icons whose canonical location
    /// was outside the sample dir (e.g. the shared <c>samples/Templates/.sdtpl/Icon2*.png</c>
    /// used by the genre starters). Returns null if neither candidate exists.
    /// </summary>
    private static UFile? ResolveTemplateAsset(string? relPath, string templateContentDir)
    {
        if (string.IsNullOrEmpty(relPath))
            return null;
        var primary = Path.GetFullPath(Path.Combine(templateContentDir, relPath));
        if (File.Exists(primary))
            return new UFile(primary);
        var fallback = Path.Combine(templateContentDir, ".sdtpl", Path.GetFileName(relPath));
        if (File.Exists(fallback))
            return new UFile(fallback);
        return null;
    }

    /// <summary>
    /// Pairs a parsed <see cref="SdtplMetadata"/> with the <c>installSource</c> of the package
    /// it came from, so Icon/Screenshot paths inside the metadata can be resolved against the
    /// right per-template content dir at registration time.
    /// </summary>
    private sealed record TemplateMetadataSource(SdtplMetadata Metadata, string InstallSource);

    /// <summary>
    /// Parameter names the template's <c>dotnetcli.host.json</c> marks <c>isVisible:false</c>
    /// (e.g. <c>updateOnly</c>); empty when there's no host file or it can't be parsed.
    /// </summary>
    public static IReadOnlyCollection<string> GetHiddenParameterNames(ITemplateInfo template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template is not ITemplateInfoHostJsonCache { HostData: { Length: > 0 } hostData })
            return Array.Empty<string>();

        var hidden = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var symbolInfo = JsonNode.Parse(hostData)?["symbolInfo"]?.AsObject();
            if (symbolInfo != null)
            {
                foreach (var (name, info) in symbolInfo)
                {
                    var isVisible = info?["isVisible"];
                    if (isVisible != null && !ParseHostBool(isVisible))
                        hidden.Add(name);
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Malformed host file: hide nothing rather than block New-Project.
        }
        return hidden;
    }

    /// <summary>Reads a host-json bool that may be a JSON string ("false") or a raw bool.</summary>
    private static bool ParseHostBool(JsonNode node)
    {
        if (node.GetValueKind() == System.Text.Json.JsonValueKind.String)
            return bool.TryParse(node.GetValue<string>(), out var b) && b;
        try { return node.GetValue<bool>(); }
        catch { return true; }
    }

    private static Guid TryParseGuid(string s) => Guid.TryParse(s, out var g) ? g : new Guid(GetHash16(s));

    /// <summary>
    /// Derives a stable 16-byte seed from a template identity string so the same identity always
    /// produces the same <see cref="TemplateDescription.Id"/>. We don't need cryptographic
    /// uniqueness — collisions between distinct identities are astronomically unlikely under
    /// SHA1/MD5 truncation, and a deterministic Guid lets us key persistent UI state (e.g. last-
    /// selected template) by identity without bumping it across editor sessions.
    /// </summary>
    private static byte[] GetHash16(string s)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
    }
}
