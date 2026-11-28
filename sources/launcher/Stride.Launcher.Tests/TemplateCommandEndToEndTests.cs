// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Versioning;
using Xunit;
using static Stride.Launcher.Tests.Helpers.TemplatePackageFixtures;

namespace Stride.Launcher.Tests;

/// <summary>
/// <c>stride template add</c>, <c>list</c> and <c>remove</c> run by the built CLI, in a child process whose NuGet folders,
/// configuration and profile are temporary: one local-folder source holds a fake Stride 4.5.6 (installed in the global
/// packages folder, so that a Stride version is installed) and a template package added by id; another template package
/// file, under a name NuGet would not find it by, is kept outside that source.
/// </summary>
public sealed class TemplateCommandEndToEndTests : IDisposable
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    private readonly string root = Directory.CreateTempSubdirectory("stride-template-e2e").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // A file the child process left open on Windows: the temporary folder is cleaned up later
        }
    }

    [Fact]
    public async Task TheTemplateCommandsAddListAndRemoveAPackage()
    {
        var home = Directory.CreateDirectory(Path.Combine(root, "home")).FullName;
        var appData = Directory.CreateDirectory(Path.Combine(root, "appdata")).FullName;
        var localAppData = Directory.CreateDirectory(Path.Combine(root, "localappdata")).FullName;
        var packages = Directory.CreateDirectory(Path.Combine(root, "packages")).FullName;
        var feed = Directory.CreateDirectory(Path.Combine(root, "feed")).FullName;
        var drop = Directory.CreateDirectory(Path.Combine(root, "drop")).FullName;
        var work = Directory.CreateDirectory(Path.Combine(root, "work")).FullName;

        // NuGet reads the user configuration from ~/.nuget/NuGet (Linux, macOS) or %APPDATA%\NuGet (Windows)
        var config = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="feed" value="{feed}" />
              </packageSources>
            </configuration>
            """;
        foreach (var configFolder in new[] { Path.Combine(home, ".nuget", "NuGet"), Path.Combine(appData, "NuGet") })
        {
            Directory.CreateDirectory(configFolder);
            File.WriteAllText(Path.Combine(configFolder, "NuGet.Config"), config);
        }

        var gameStudio = WriteNupkg(feed, "Stride.GameStudio", "4.5.6", packageType: "Dependency", tags: "stride", marker: null, templates: []);
        await ExtractAsync(gameStudio, "Stride.GameStudio", "4.5.6", packages);
        // A file named as a browser saves it: NuGet would not find it by id in its folder
        var package = Path.Combine(drop, "my downloaded templates (1).nupkg");
        File.Move(WriteNupkg(drop, "E2e.Templates", "1.0.0", templates: [("e2e-game", "project")]), package);
        // A package of the feed, added by id. Its marker (Stride.Core 4.5.0) is in no source: nothing restores it.
        WriteNupkg(feed, "E2e.More.Templates", "2.0.0", templates: [("e2e-more", "project")]);

        var environment = new Dictionary<string, string?>
        {
            ["HOME"] = home,
            ["USERPROFILE"] = home,
            ["APPDATA"] = appData,
            ["LOCALAPPDATA"] = localAppData,
            ["XDG_DATA_HOME"] = null,
            ["XDG_CONFIG_HOME"] = null,
            ["NUGET_PACKAGES"] = packages,
            ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(root, "http-cache"),
            ["DOTNET_CLI_HOME"] = home,
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["StrideDir"] = null,
        };

        var add = await RunAsync(work, environment, "template", "add", package, "--yes");
        Assert.True(add.ExitCode == 0, add.ToString());
        Assert.Contains("Installed E2e.Templates 1.0.0.", add.Output);

        var again = await RunAsync(work, environment, "template", "add", package, "--yes");
        Assert.True(again.ExitCode == 0, again.ToString());

        var byId = await RunAsync(work, environment, "template", "add", "E2e.More.Templates", "--yes");
        Assert.True(byId.ExitCode == 0, byId.ToString());
        Assert.Contains("Installed E2e.More.Templates 2.0.0.", byId.Output);
        Assert.False(Directory.Exists(Path.Combine(packages, "stride.core")), "The marker dependency was restored.");

        var list = await RunAsync(work, environment, "template", "list");
        Assert.True(list.ExitCode == 0, list.ToString());
        Assert.Equal(["E2e.More.Templates  2.0.0  Color-Rise", "E2e.Templates       1.0.0  Color-Rise"], list.Output.Trim().Split(Environment.NewLine));

        var templates = await RunAsync(work, environment, "new", "list");
        Assert.True(templates.ExitCode == 0, templates.ToString());
        Assert.Contains("e2e.templates:", templates.Output);
        Assert.Contains("e2e-game", templates.Output);
        Assert.Contains("e2e-more", templates.Output);

        foreach (var (id, version) in new[] { ("E2e.Templates", "1.0.0"), ("E2e.More.Templates", "2.0.0") })
        {
            var remove = await RunAsync(work, environment, "template", "remove", id, "--yes");
            Assert.True(remove.ExitCode == 0, remove.ToString());
            Assert.Contains($"Removed {id} ({version}).", remove.Output);
            Assert.False(Directory.Exists(Path.Combine(packages, id.ToLowerInvariant())), $"{id} is left in the global packages folder.");
        }

        var left = await RunAsync(work, environment, "template", "list");
        Assert.True(left.ExitCode == 0, left.ToString());
        // The feed is a local-folder source: its file is still found there, and the line says where
        Assert.Equal(["E2e.More.Templates  2.0.0  Color-Rise", $"  from {feed}"], left.Output.Trim().Split(Environment.NewLine));
    }

    /// <summary>Extracts a package into a global packages folder, the way a restore does.</summary>
    private static async Task ExtractAsync(string nupkg, string id, string version, string packagesFolder)
    {
        var context = new PackageExtractionContext(PackageSaveMode.Defaultv3, XmlDocFileSaveMode.None,
            ClientPolicyContext.GetClientPolicy(NullSettings.Instance, NullLogger.Instance), NullLogger.Instance);
        await PackageExtractor.InstallFromSourceAsync(
            Path.GetDirectoryName(nupkg)!, new PackageIdentity(id, NuGetVersion.Parse(version)),
            async destination =>
            {
                await using var source = File.OpenRead(nupkg);
                await source.CopyToAsync(destination);
            },
            new VersionFolderPathResolver(packagesFolder), context, CancellationToken.None);
    }

    /// <summary>Runs the built CLI (Stride.Cli's output next to this project's) with no console input.</summary>
    private static async Task<Result> RunAsync(string workingDirectory, IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var separator = Path.DirectorySeparatorChar;
        var cli = Path.Combine(AppContext.BaseDirectory.Replace($"{separator}Stride.Launcher.Tests{separator}", $"{separator}Stride.Cli{separator}"), "stride.dll");
        Assert.True(File.Exists(cli), $"The Stride CLI is not built: {cli} is missing.");

        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(cli);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        foreach (var (name, value) in environment)
        {
            if (value is null)
                startInfo.Environment.Remove(name);
            else
                startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(CommandTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"stride {string.Join(' ', arguments)} did not end within {CommandTimeout}.");
        }
        return new Result(string.Join(' ', arguments), process.ExitCode, await output, await error);
    }

    private sealed record Result(string Command, int ExitCode, string Output, string Error)
    {
        public override string ToString() => $"stride {Command} exited with {ExitCode}.{Environment.NewLine}{Output}{Environment.NewLine}{Error}";
    }
}
