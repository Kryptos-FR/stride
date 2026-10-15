// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace Stride.Core.Assets
{
    static class RestoreHelper
    {
        public static List<string> ListAssemblies(LockFile lockFile, string? graphicsApi = null)
        {
            graphicsApi ??= GraphicsApiSelector.Default;

            var assemblies = new List<string>();

            var libPaths = GetLibPaths(lockFile);

            bool TryCollectGraphicsApiDependentAssemblies(string assemblyFile)
            {
                var graphicsApiFolder = Path.Combine(Path.GetDirectoryName(assemblyFile), graphicsApi);
                if (Directory.Exists(graphicsApiFolder))
                {
                    // We escape the loop as we already enumerated all files and we don't want to be called multiple times
                    foreach (var graphicsApiDependentAssemblyFile in Directory.EnumerateFiles(graphicsApiFolder, "*.*"))
                    {
                        if (Path.GetExtension(graphicsApiDependentAssemblyFile).ToLowerInvariant() == ".exe" ||
                            Path.GetExtension(graphicsApiDependentAssemblyFile).ToLowerInvariant() == ".dll")
                            assemblies.Add(graphicsApiDependentAssemblyFile);
                    }
                    return true;
                }

                return false;
            }

            var target = lockFile.Targets.Last();
            foreach (var lib in target.Libraries)
            {
                if (libPaths.TryGetValue(ValueTuple.Create(lib.Name, lib.Version), out var libPath))
                {
                    // Check if this package has a dev redirect
                    if (TryResolveDevRedirect(lib.Name, libPath, graphicsApi, assemblies))
                        continue;

                    foreach (var a in lib.RuntimeAssemblies)
                    {
                        var assemblyFile = Path.Combine(libPath, a.Path.Replace('/', Path.DirectorySeparatorChar));

                        // Check if StrideGraphicsApi specific files exist
                        if (ReferenceEquals(a, lib.RuntimeAssemblies[0]) // first iteration?
                            && TryCollectGraphicsApiDependentAssemblies(assemblyFile))
                            break;

                        assemblies.Add(assemblyFile);

                    }
                    foreach (var a in lib.RuntimeTargets)
                    {
                        var assemblyFile = Path.Combine(libPath, a.Path.Replace('/', Path.DirectorySeparatorChar));

                        // Check if StrideGraphicsApi specific files exist
                        if (ReferenceEquals(a, lib.RuntimeTargets[0]) // first iteration?
                            && TryCollectGraphicsApiDependentAssemblies(assemblyFile))
                            break;

                        assemblies.Add(assemblyFile);
                    }
                }
            }

            return assemblies;
        }

        /// <summary>
        /// Checks if a package has dev-redirect stub props and resolves assembly paths from HintPath.
        /// </summary>
        private static bool TryResolveDevRedirect(string packageName, string libPath, string graphicsApi, List<string> assemblies)
        {
            // Look for build/<PackageId>.props in the package folder
            var propsPath = Path.Combine(libPath, "build", $"{packageName}.props");
            if (!File.Exists(propsPath))
                return false;

            try
            {
                var doc = XDocument.Load(propsPath);
                XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

                // Check if this is a dev-redirect stub
                var devRedirect = doc.Descendants(ns + "StrideDevRedirect").FirstOrDefault();
                if (devRedirect?.Value != "true")
                    return false;

                // Read StrideDevRoot and StrideDevConfiguration
                var devRoot = doc.Descendants(ns + "StrideDevRoot").FirstOrDefault()?.Value ?? "";
                var devConfig = doc.Descendants(ns + "StrideDevConfiguration").FirstOrDefault()?.Value ?? "Debug";

                // Find all Reference HintPaths and resolve MSBuild properties
                foreach (var reference in doc.Descendants(ns + "Reference"))
                {
                    var hintPath = reference.Element(ns + "HintPath")?.Value;
                    if (hintPath == null)
                        continue;

                    // Resolve MSBuild-style properties. The graphicsApi arg drives the first probe
                    // (Direct3D11 by default on the editor path); fall back to other APIs whose
                    // dev DLL exists, so a Linux/macOS build using Vulkan/OpenGL still resolves.
                    hintPath = hintPath
                        .Replace("$(StrideDevRoot)", devRoot)
                        .Replace("$(StrideDevConfiguration)", devConfig);

                    if (hintPath.Contains("$(StrideGraphicsApi)"))
                    {
                        var probeOrder = new[] { graphicsApi, "Direct3D11", "Direct3D12", "Vulkan", "OpenGL", "OpenGLES" };
                        foreach (var api in probeOrder)
                        {
                            var candidate = hintPath.Replace("$(StrideGraphicsApi)", api);
                            if (File.Exists(candidate))
                            {
                                assemblies.Add(candidate);
                                break;
                            }
                        }
                    }
                    else if (File.Exists(hintPath))
                    {
                        assemblies.Add(hintPath);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static List<string> ListNativeLibs(LockFile lockFile)
        {
            var libs = new List<string>();

            var libPaths = GetLibPaths(lockFile);

            var target = lockFile.Targets.Last();
            foreach (var lib in target.Libraries)
            {
                if (libPaths.TryGetValue(ValueTuple.Create(lib.Name, lib.Version), out var libPath))
                {
                    foreach (var n in lib.NativeLibraries)
                    {
                        if (!IsValidNativeLibraryFile(n.Path))
                        {
                            continue;
                        }
                        var assemblyFile = Path.Combine(libPath, n.Path.Replace('/', Path.DirectorySeparatorChar));
                        libs.Add(assemblyFile);
                    }
                }
            }

            return libs;

            static bool IsValidNativeLibraryFile(string path)
            {
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
                    || path.IndexOf(".so.", StringComparison.OrdinalIgnoreCase) >= 0    // Linux allows for files like 'libnativedep.so.6'
                    || path.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                // Also handle executables shipped as native deps
                if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            }
        }

        private static Dictionary<(string, NuGetVersion), string> GetLibPaths(LockFile lockFile)
        {
            var libPaths = new Dictionary<ValueTuple<string, NuGet.Versioning.NuGetVersion>, string>();
            foreach (var lib in lockFile.Libraries)
            {
                foreach (var packageFolder in lockFile.PackageFolders)
                {
                    var libraryPath = Path.Combine(packageFolder.Path, lib.Path.Replace('/', Path.DirectorySeparatorChar));
                    if (Directory.Exists(libraryPath))
                    {
                        libPaths.Add(ValueTuple.Create(lib.Name, lib.Version), libraryPath);
                        break;
                    }
                }
            }

            return libPaths;
        }

        public static (RestoreRequest, RestoreResult) Restore(ILogger logger, NuGetFramework nugetFramework, string runtimeIdentifier, string packageName, VersionRange versionRange, string settingsRoot = null)
        {
            var settings = NuGet.Configuration.Settings.LoadDefaultSettings(settingsRoot);
            var packagesPath = SettingsUtility.GetGlobalPackagesFolder(settings);
            var fallbackFolders = SettingsUtility.GetFallbackPackageFolders(settings).ToList();

            // A build-only package's own runtime dependencies are only known from its nuspec. When the package is
            // already in one of our package folders, read it now and restore once. Otherwise (first use, or the game
            // restored it to a folder of its own) restore the package first, then read the nuspec where that restore
            // put it and restore again with those dependencies.
            var nuspecPath = versionRange.HasLowerAndUpperBounds && versionRange.MinVersion == versionRange.MaxVersion
                ? FindManifest([packagesPath, .. fallbackFolders], packageName, versionRange.MinVersion)
                : null;
            var extraDependencies = nuspecPath != null ? GetRuntimeExcludedDependencies(nuspecPath, nugetFramework) : [];
            var (request, result) = Restore(logger, settings, nugetFramework, runtimeIdentifier, packageName, versionRange, packagesPath, fallbackFolders, extraDependencies);

            if (nuspecPath == null && result.Success
                && result.LockFile.Libraries.FirstOrDefault(library => library.Type == "package" && string.Equals(library.Name, packageName, StringComparison.OrdinalIgnoreCase)) is { } restoredPackage
                && FindManifest(result.LockFile.PackageFolders.Select(folder => folder.Path), restoredPackage.Name, restoredPackage.Version) is { } restoredNuspecPath)
            {
                extraDependencies = GetRuntimeExcludedDependencies(restoredNuspecPath, nugetFramework);
                if (extraDependencies.Count > 0)
                    (request, result) = Restore(logger, settings, nugetFramework, runtimeIdentifier, packageName, versionRange, packagesPath, fallbackFolders, extraDependencies);
            }

            return (request, result);
        }

        private static (RestoreRequest, RestoreResult) Restore(ILogger logger, ISettings settings, NuGetFramework nugetFramework, string runtimeIdentifier, string packageName, VersionRange versionRange, string packagesPath, List<string> fallbackFolders, List<LibraryDependency> extraDependencies)
        {
            var projectPath = Path.Combine("StrideNugetResolver.json");
            var spec = new PackageSpec()
            {
                Name = Path.GetFileNameWithoutExtension(projectPath), // make sure this package never collides with a dependency
                FilePath = projectPath,
                TargetFrameworks =
                {
                    new TargetFrameworkInformation
                    {
                        FrameworkName = nugetFramework,
                        Dependencies = [
                            new LibraryDependency
                            {
                                LibraryRange = new LibraryRange(packageName, versionRange, LibraryDependencyTarget.Package),
                            },
                            .. extraDependencies,
                        ],
                    }
                },
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectPath = projectPath,
                    ProjectName = Path.GetFileNameWithoutExtension(projectPath),
                    ProjectStyle = ProjectStyle.PackageReference,
                    ProjectUniqueName = projectPath,
                    // Per-user dir, not the shared temp dir: the output drives assembly loading and its name is predictable (pre-seed risk)
                    OutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "stride", "nugetresolver", $"{packageName}-{versionRange.MinVersion.ToString()}-{nugetFramework.GetShortFolderName()}-{runtimeIdentifier}"),
                    OriginalTargetFrameworks = new[] { nugetFramework.GetShortFolderName() },
                    ConfigFilePaths = settings.GetConfigFilePaths(),
                    PackagesPath = packagesPath,
                    Sources = SettingsUtility.GetEnabledSources(settings).ToList(),
                    FallbackFolders = fallbackFolders
                },
                RuntimeGraph = new RuntimeGraph(new[] { new RuntimeDescription(runtimeIdentifier) }),
            };

            using (var context = new SourceCacheContext())
            {
                context.IgnoreFailedSources = true;

                var dependencyGraphSpec = new DependencyGraphSpec();

                dependencyGraphSpec.AddProject(spec);

                dependencyGraphSpec.AddRestore(spec.RestoreMetadata.ProjectUniqueName);

                IPreLoadedRestoreRequestProvider requestProvider = new DependencyGraphSpecRequestProvider(new RestoreCommandProvidersCache(), dependencyGraphSpec);

                var restoreArgs = new RestoreArgs
                {
                    AllowNoOp = true,
                    CacheContext = context,
                    CachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(settings)),
                    Log = logger,
                };

                // Create requests from the arguments
                var requests = requestProvider.CreateRequests(restoreArgs).Result;

                // Restore the packages
                for (int tryCount = 0; tryCount < 2; ++tryCount)
                {
                    try
                    {
                        var results = RestoreRunner.RunWithoutCommit(requests, restoreArgs).Result;

                        // Commit results so that noop cache works next time
                        foreach (var result in results)
                        {
                            result.Result.CommitAsync(logger, CancellationToken.None).Wait();
                        }
                        var mainResult = results.First();
                        return (mainResult.SummaryRequest.Request, mainResult.Result);
                    }
                    catch (Exception e) when (e is UnauthorizedAccessException || e is IOException || ((e is AggregateException ae) && ae.InnerExceptions.Any(e2 => e2 is UnauthorizedAccessException || e2 is IOException)))
                    {
                        // If we have an unauthorized access exception, it means assemblies are locked by running Stride process
                        // During first try, kill some known harmless processes, and try again
                        if (tryCount == 1)
                            throw;

                        foreach (var process in new[] { "Stride.ConnectionRouter", "Stride.VisualStudio.Commands" }.SelectMany(Process.GetProcessesByName))
                        {
                            try
                            {
                                if (process.Id != Process.GetCurrentProcess().Id)
                                {
                                    logger.LogWarning($"Failed to restore NuGet, killing '{process.ProcessName}' to hopefully release locks held by it - VS extension will break");
                                    process.Kill();
                                    process.WaitForExit();
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }

                throw new InvalidOperationException("Unreachable code");
            }
        }

        /// <summary>
        /// The nuspec of the given package version in the first of <paramref name="packageFolders"/> that has it, or null.
        /// </summary>
        private static string? FindManifest(IEnumerable<string> packageFolders, string packageName, NuGetVersion version)
        {
            return packageFolders
                .Select(folder => new VersionFolderPathResolver(folder).GetManifestFilePath(packageName, version))
                .FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// The dependencies a build-only package (no lib/, e.g. Stride.AssetCompiler, a tool that runs from its own
        /// package) keeps out of its consumers' runtime, read from its nuspec. They are the package's own runtime
        /// dependencies, so they are restored as direct dependencies to get their runtime assets. None for a package
        /// with a lib/ folder.
        /// </summary>
        private static List<LibraryDependency> GetRuntimeExcludedDependencies(string nuspecPath, NuGetFramework nugetFramework)
        {
            if (Directory.Exists(Path.Combine(Path.GetDirectoryName(nuspecPath)!, "lib")))
                return [];

            var dependencyGroup = NuGetFrameworkUtility.GetNearest(new NuspecReader(nuspecPath).GetDependencyGroups(), nugetFramework);
            if (dependencyGroup == null)
                return [];

            return dependencyGroup.Packages
                .Where(dependency =>
                {
                    var includeFlags = dependency.Include.Count > 0 ? LibraryIncludeFlagUtils.GetFlags(dependency.Include) : LibraryIncludeFlags.All;
                    includeFlags &= ~LibraryIncludeFlagUtils.GetFlags(dependency.Exclude);
                    return (includeFlags & LibraryIncludeFlags.Runtime) == 0;
                })
                .Select(dependency => new LibraryDependency
                {
                    LibraryRange = new LibraryRange(dependency.Id, dependency.VersionRange, LibraryDependencyTarget.Package),
                })
                .ToList();
        }
    }
}
