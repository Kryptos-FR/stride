// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Compression;
using Stride.Assets.Editor.Templates;
using Stride.Assets.Templates;
using Stride.Core;
using Stride.Core.IO;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.ViewModels;

namespace Stride.Assets.Editor.Tests.Templates;

/// <summary>
/// The view model of the <em>Template packages</em> window: the third-party template packages found, adding one from a file or
/// by id, removing one.
/// </summary>
public sealed class TemplatePackagesViewModelTests : IDisposable
{
    private static readonly PackageVersion Engine = new("4.5.6");

    private readonly string folder = Directory.CreateTempSubdirectory("stride-template-packages-tests").FullName;
    private readonly TestEditorDialogService dialogs = new();
    private readonly FakeStore store = new();
    private readonly List<ThirdPartyTemplatePackage> scanned = [];
    private int rescans;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private TemplatePackagesViewModel Create()
    {
        var installer = new TemplatePackageInstaller(store, Engine, new HashSet<string>(["Stride.Templates.Games"], StringComparer.OrdinalIgnoreCase));
        return new TemplatePackagesViewModel(
            new ViewModelServiceProvider([new NullDispatcherService(), dialogs]),
            installer,
            () => scanned,
            () =>
            {
                rescans++;
                // The scan finds what the store holds now
                scanned.Clear();
                scanned.AddRange(store.Installed.Select(candidate => new ThirdPartyTemplatePackage(candidate, true, null, null, ["x"], [], 1, 2)));
                return Task.FromResult<IReadOnlyList<ThirdPartyTemplatePackage>>(scanned.ToList());
            });
    }

    [Fact]
    public void ARowPerPackageFound()
    {
        scanned.Add(new ThirdPartyTemplatePackage(Candidate("Foo.Templates", "0.1.0"), true, null, null, ["a", "b", "c"], [], 1, 2));
        scanned.Add(new ThirdPartyTemplatePackage(Candidate("Mine.Templates", "1.0.0", localFolder: "/feeds/mine"), true, null, null, ["d"], [], 1, 0));
        scanned.Add(new ThirdPartyTemplatePackage(Candidate("Bar.Templates", "1.0.0"), false, "Shared.Game", "Foo.Templates", [], []));

        var viewModel = Create();

        Assert.Equal(["Foo.Templates", "Mine.Templates", "Bar.Templates"], viewModel.Packages.Select(row => row.Id));
        var foo = viewModel.Packages[0];
        Assert.Equal("0.1.0", foo.Version);
        Assert.Equal("Color-Rise", foo.Authors);
        Assert.Equal("MIT", foo.License);
        Assert.Equal("1 game template, 2 asset packs", foo.Contents);
        Assert.True(foo.CanRemove);
        Assert.Null(foo.Note);
        // A package of a local-folder source: no remove, the line says what to do
        var mine = viewModel.Packages[1];
        Assert.False(mine.CanRemove);
        Assert.Contains("/feeds/mine", mine.Note);
        // A package left out by a clash says why
        Assert.Equal("Not shown: a template has the same identity as one of Foo.Templates.", viewModel.Packages[2].Note);
        Assert.True(viewModel.HasPackages);
    }

    [Fact]
    public async Task AddingAFileAsksAndInstallsOnYes()
    {
        var path = WriteNupkg("Foo.Templates", "1.0.0");
        dialogs.PickFile = _ => new UFile(path);
        dialogs.Answer = MessageBoxResult.Yes;
        var viewModel = Create();

        await viewModel.AddFromFileAsync();

        Assert.Contains("Install the template package Foo.Templates 1.0.0?", Assert.Single(dialogs.Messages));
        Assert.Equal(["Foo.Templates 1.0.0"], store.Installs);
        Assert.Equal(1, rescans);
        Assert.Equal(["Foo.Templates"], viewModel.Packages.Select(row => row.Id));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task AddingAFileDoesNothingOnNo()
    {
        dialogs.PickFile = _ => new UFile(WriteNupkg("Foo.Templates", "1.0.0"));
        dialogs.Answer = MessageBoxResult.No;
        var viewModel = Create();

        await viewModel.AddFromFileAsync();

        Assert.Single(dialogs.Messages);
        Assert.Empty(store.Installs);
        Assert.Equal(0, rescans);
    }

    [Fact]
    public async Task ARefusedPackageShowsTheReasonAndInstallsNothing()
    {
        dialogs.PickFile = _ => new UFile(WriteNupkg("Foo.Templates", "0.2.0", marker: "4.6.0"));
        var viewModel = Create();

        await viewModel.AddFromFileAsync();

        Assert.Equal(["Foo.Templates 0.2.0 is made for Stride 4.6.0; this is Stride 4.5.6."], dialogs.Messages);
        Assert.Empty(store.Installs);
    }

    [Fact]
    public async Task AddingByIdTakesTheIdAndTheVersion()
    {
        store.Sources["Foo.Templates"] =
        [
            new TemplatePackageInfo("Foo.Templates", new PackageVersion("1.0.0"), "Color-Rise", "MIT", "", null, true, new PackageVersion("4.5.0"), null, null, "https://feed.example", null),
            new TemplatePackageInfo("Foo.Templates", new PackageVersion("1.1.0"), "Color-Rise", "MIT", "", null, true, new PackageVersion("4.5.0"), null, null, "https://feed.example", null),
        ];
        dialogs.Answer = MessageBoxResult.Yes;
        var viewModel = Create();
        viewModel.PackageId = "Foo.Templates";
        viewModel.PackageVersion = "1.0.0";

        await viewModel.AddByIdAsync();

        Assert.Equal(["Foo.Templates 1.0.0"], store.Installs);
    }

    [Fact]
    public async Task AnUnknownIdShowsTheErrorLine()
    {
        var viewModel = Create();
        viewModel.PackageId = "Nope.Templates";

        await viewModel.AddByIdAsync();

        Assert.Equal("No package Nope.Templates was found in the package sources.", viewModel.Error);
        Assert.Empty(store.Installs);
    }

    [Fact]
    public async Task RemovingAsksAndUninstalls()
    {
        store.Installed.Add(Candidate("Foo.Templates", "1.0.0"));
        scanned.Add(new ThirdPartyTemplatePackage(store.Installed[0], true, null, null, ["a"], [], 1, 0));
        dialogs.Answer = MessageBoxResult.Yes;
        var viewModel = Create();

        await viewModel.Packages[0].RemoveAsync();

        Assert.Equal("Remove Foo.Templates (1.0.0) from the global packages folder?", Assert.Single(dialogs.Messages));
        Assert.Equal(["Foo.Templates 1.0.0"], store.Uninstalls);
        Assert.Empty(viewModel.Packages);
        Assert.False(viewModel.HasPackages);
    }

    [Fact]
    public async Task RemovingDoesNothingOnNo()
    {
        store.Installed.Add(Candidate("Foo.Templates", "1.0.0"));
        scanned.Add(new ThirdPartyTemplatePackage(store.Installed[0], true, null, null, ["a"], [], 1, 0));
        dialogs.Answer = MessageBoxResult.No;
        var viewModel = Create();

        await viewModel.Packages[0].RemoveAsync();

        Assert.Empty(store.Uninstalls);
        Assert.Single(viewModel.Packages);
    }

    private static TemplatePackageCandidate Candidate(string id, string version, string? localFolder = null)
        => new(id, new PackageVersion(version), true, true, new PackageVersion("4.5.0"), $"/store/{id}/{version}", localFolder, "Color-Rise", "MIT", "Templates");

    private string WriteNupkg(string id, string version, string marker = "4.5.0")
    {
        var path = Path.Combine(folder, $"{id}.{version}.nupkg");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(zip.CreateEntry($"{id}.nuspec").Open()))
        {
            writer.Write($"""
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                  <metadata>
                    <id>{id}</id>
                    <version>{version}</version>
                    <authors>Color-Rise</authors>
                    <license type="expression">MIT</license>
                    <description>Templates for the tests</description>
                    <tags>stride-template</tags>
                    <packageTypes><packageType name="Template" /></packageTypes>
                    <dependencies><group targetFramework="net10.0"><dependency id="Stride.Core" version="{marker}" /></group></dependencies>
                  </metadata>
                </package>
                """);
        }
        using (var writer = new StreamWriter(zip.CreateEntry("content/game/.template.config/template.json").Open()))
            writer.Write("""{ "identity": "Foo.Game", "tags": { "type": "project" } }""");
        return path;
    }

    private sealed class FakeStore : ITemplatePackageStore
    {
        public List<TemplatePackageCandidate> Installed { get; } = [];

        public Dictionary<string, IReadOnlyList<TemplatePackageInfo>> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Installs { get; } = [];

        public List<string> Uninstalls { get; } = [];

        public IReadOnlyList<TemplatePackageCandidate> GetInstalled() => Installed;

        public Task<IReadOnlyList<TemplatePackageInfo>> FindInSourcesAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(Sources.TryGetValue(id, out var found) ? found : (IReadOnlyList<TemplatePackageInfo>)[]);

        public Task InstallAsync(string id, PackageVersion version, string? extraSourceFolder, CancellationToken cancellationToken)
        {
            Installs.Add($"{id} {version}");
            Installed.Add(Candidate(id, version.ToString()));
            return Task.CompletedTask;
        }

        public Task UninstallAsync(IReadOnlyList<TemplatePackageCandidate> installed, CancellationToken cancellationToken)
        {
            Uninstalls.AddRange(installed.Select(candidate => $"{candidate.Id} {candidate.Version}"));
            Installed.RemoveAll(installed.Contains);
            return Task.CompletedTask;
        }
    }
}
