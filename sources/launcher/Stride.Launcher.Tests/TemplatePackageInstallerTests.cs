// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.IO.Compression;
using System.Security;
using Stride.Assets.Templates;
using Stride.Core;
using Xunit;

namespace Stride.Launcher.Tests;

public sealed class TemplatePackageInstallerTests : IDisposable
{
    private static readonly PackageVersion Engine = new("4.5.6");
    private static readonly IReadOnlySet<string> BuiltIn = new HashSet<string>(["Stride.Templates.Games", "Stride.Templates.AssetPacks"], StringComparer.OrdinalIgnoreCase);

    private readonly string folder = Directory.CreateTempSubdirectory("stride-installer-tests").FullName;
    private readonly FakeStore store = new();
    private readonly TemplatePackageInstaller installer;

    public TemplatePackageInstallerTests()
    {
        installer = new TemplatePackageInstaller(store, Engine, BuiltIn);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void InspectFileReadsTheNuspecAndCountsTheTemplates()
    {
        var path = WriteNupkg("Foo.Templates", "1.2.0-dev3", templates: [("foo-game", "project"), ("foo-pack-a", "item"), ("foo-pack-b", "item")]);

        var info = installer.InspectFile(path);

        Assert.Equal("Foo.Templates", info.Id);
        Assert.Equal(new PackageVersion("1.2.0-dev3"), info.Version);
        Assert.Equal("Color-Rise", info.Authors);
        Assert.Equal("MIT", info.License);
        Assert.Equal("Templates for the tests", info.Description);
        Assert.True(info.IsTemplatePackage);
        Assert.True(info.HasTemplateTag);
        Assert.Equal(new PackageVersion("4.5.0"), info.MarkerFloor);
        Assert.Equal(1, info.GameTemplates);
        Assert.Equal(2, info.AssetPacks);
        Assert.Equal(Path.GetFullPath(path), info.Origin);
        Assert.Equal(Path.GetFullPath(path), info.FilePath);
    }

    [Fact]
    public void APackageOfAnotherTypeIsRefused()
        => Assert.Equal("Foo.Templates 1.0.0 is not a template package: it does not declare the NuGet package type Template.",
            installer.Refusal(installer.InspectFile(WriteNupkg("Foo.Templates", "1.0.0", packageType: "Dependency"))));

    [Fact]
    public void APackageWithoutTheTagIsRefused()
        => Assert.Equal("Foo.Templates 1.0.0 is not a Stride template package: it does not have the tag stride-template.",
            installer.Refusal(installer.InspectFile(WriteNupkg("Foo.Templates", "1.0.0", tags: "game templates"))));

    [Fact]
    public void ABuiltInPackageIsRefused()
        => Assert.Equal("Stride.Templates.Games is one of Stride's own template packages: it comes with Stride.",
            installer.Refusal(installer.InspectFile(WriteNupkg("Stride.Templates.Games", "4.5.6"))));

    [Fact]
    public void APackageForANewerEngineIsRefusedWithBothVersions()
        => Assert.Equal("Foo.Templates 0.2.0 is made for Stride 4.6.0; this is Stride 4.5.6.",
            installer.Refusal(installer.InspectFile(WriteNupkg("Foo.Templates", "0.2.0", marker: "4.6.0"))));

    [Fact]
    public void AnUnmarkedPackageIsAccepted()
        => Assert.Null(installer.Refusal(installer.InspectFile(WriteNupkg("Foo.Templates", "1.0.0", marker: null))));

    [Fact]
    public void TheConfirmationNamesThePackageAndWarnsAboutCode()
    {
        var path = WriteNupkg("Foo.Templates", "1.0.0");

        var text = installer.Confirmation(installer.InspectFile(path));

        Assert.Contains("Foo.Templates 1.0.0", text);
        Assert.Contains("Color-Rise", text);
        Assert.Contains("MIT", text);
        Assert.Contains(Path.GetFullPath(path), text);
        Assert.Contains("project files and code that run when", text);
    }

    [Fact]
    public async Task FindByIdWithoutAVersionTakesTheNewestCompatibleOnePrereleasesIncluded()
    {
        store.Sources["Foo.Templates"] =
        [
            Info("Foo.Templates", "1.0.0", "4.5.0"),
            Info("Foo.Templates", "1.1.0-dev2", "4.5.6"),
            Info("Foo.Templates", "2.0.0", "4.6.0"),
        ];

        var info = await installer.FindByIdAsync("Foo.Templates", null);

        Assert.Equal(new PackageVersion("1.1.0-dev2"), info.Version);
        Assert.Null(info.GameTemplates);
        Assert.Null(info.FilePath);
    }

    [Fact]
    public async Task FindByIdWithAVersionTakesThatOne()
    {
        store.Sources["Foo.Templates"] = [Info("Foo.Templates", "1.0.0", "4.5.0"), Info("Foo.Templates", "2.0.0", "4.6.0")];

        var info = await installer.FindByIdAsync("Foo.Templates", new PackageVersion("2.0.0"));

        Assert.Equal(new PackageVersion("2.0.0"), info.Version);
        // Checked like a file: made for a newer engine
        Assert.NotNull(installer.Refusal(info));
    }

    [Fact]
    public async Task AnUnknownIdOrVersionIsAnErrorNamingTheId()
    {
        store.Sources["Foo.Templates"] = [Info("Foo.Templates", "1.0.0", "4.5.0")];

        var unknownId = await Assert.ThrowsAsync<InvalidOperationException>(() => installer.FindByIdAsync("Bar.Templates", null));
        var unknownVersion = await Assert.ThrowsAsync<InvalidOperationException>(() => installer.FindByIdAsync("Foo.Templates", new PackageVersion("3.0.0")));

        Assert.Contains("Bar.Templates", unknownId.Message);
        Assert.Contains("Foo.Templates 3.0.0", unknownVersion.Message);
    }

    [Fact]
    public async Task AddingAFileInstallsFromItsFolder()
    {
        var path = WriteNupkg("Foo.Templates", "1.0.0");

        await installer.AddAsync(installer.InspectFile(path));

        Assert.Equal([("Foo.Templates", new PackageVersion("1.0.0"), (string?)Path.GetDirectoryName(Path.GetFullPath(path)))], store.Installs);
    }

    [Fact]
    public async Task AddingByIdInstallsFromTheSources()
    {
        store.Sources["Foo.Templates"] = [Info("Foo.Templates", "1.0.0", "4.5.0")];

        await installer.AddAsync(await installer.FindByIdAsync("Foo.Templates", null));

        Assert.Equal([("Foo.Templates", new PackageVersion("1.0.0"), (string?)null)], store.Installs);
    }

    [Fact]
    public async Task APackageFromTheSourcesThatIsNotOfTheTemplateTypeIsUninstalledAgain()
    {
        // A source does not say the package type: it is checked once the package is installed
        store.Sources["Foo.Templates"] = [Info("Foo.Templates", "1.0.0", "4.5.0")];
        store.InstalledAs = (id, version) => Candidate(id, version.ToString(), isTemplate: false);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await installer.AddAsync(await installer.FindByIdAsync("Foo.Templates", null)));

        Assert.Contains("package type Template", error.Message);
        Assert.Equal(["Foo.Templates 1.0.0"], store.Uninstalls);
    }

    [Fact]
    public void RemovingAPackageThatIsNotInstalledSaysSo()
    {
        var removal = installer.PlanRemoval("Foo.Templates");

        Assert.Equal(TemplatePackageRemovalKind.NotInstalled, removal.Kind);
        Assert.Equal("Foo.Templates is not installed.", removal.Message);
    }

    [Fact]
    public void ABuiltInPackageCannotBeRemoved()
    {
        store.Installed.Add(Candidate("Stride.Templates.Games", "4.5.6"));

        var removal = installer.PlanRemoval("Stride.Templates.Games");

        Assert.Equal(TemplatePackageRemovalKind.BuiltIn, removal.Kind);
        Assert.Equal("Stride.Templates.Games is one of Stride's own template packages and cannot be removed.", removal.Message);
    }

    [Fact]
    public async Task APackageOnlyInALocalFolderIsNotRemoved()
    {
        store.Installed.Add(Candidate("Foo.Templates", "1.0.0", localFolder: "/feeds/mine"));

        var removal = installer.PlanRemoval("Foo.Templates");

        Assert.Equal(TemplatePackageRemovalKind.OnlyInLocalFolder, removal.Kind);
        Assert.Equal("/feeds/mine", removal.LocalFolder);
        Assert.Contains("/feeds/mine", removal.Message);
        await installer.RemoveAsync(removal);
        Assert.Empty(store.Uninstalls);
    }

    [Fact]
    public async Task EveryInstalledVersionIsRemoved()
    {
        store.Installed.Add(Candidate("Foo.Templates", "1.0.0"));
        store.Installed.Add(Candidate("foo.templates", "1.1.0"));
        store.Installed.Add(Candidate("Bar.Templates", "1.0.0"));

        var removal = installer.PlanRemoval("Foo.Templates");
        await installer.RemoveAsync(removal);

        Assert.Equal(TemplatePackageRemovalKind.Uninstall, removal.Kind);
        Assert.Equal(["Foo.Templates 1.0.0", "foo.templates 1.1.0"], store.Uninstalls);
    }

    [Fact]
    public void ListedIsWhatTheRuleSelects()
    {
        store.Installed.Add(Candidate("Foo.Templates", "1.0.0"));
        store.Installed.Add(Candidate("Foo.Templates", "1.1.0"));
        store.Installed.Add(Candidate("Stride.Templates.Games", "4.5.6"));
        store.Installed.Add(Candidate("Other.Package", "1.0.0", isTemplate: false));

        Assert.Equal(["Foo.Templates 1.1.0"], installer.Listed().Select(candidate => $"{candidate.Id} {candidate.Version}"));
    }

    private static TemplatePackageCandidate Candidate(string id, string version, bool isTemplate = true, string? localFolder = null)
        => new(id, new PackageVersion(version), isTemplate, HasTemplateTag: true, new PackageVersion("4.5.0"), $"/store/{id}/{version}", localFolder, "Someone", "MIT", "Templates");

    private static TemplatePackageInfo Info(string id, string version, string? marker)
        => new(id, new PackageVersion(version), "Someone", "MIT", "Templates", IsTemplatePackage: null, HasTemplateTag: true,
            marker is null ? null : new PackageVersion(marker), GameTemplates: null, AssetPacks: null, Origin: "https://feed.example/v3/index.json", FilePath: null);

    private string WriteNupkg(string id, string version, string packageType = "Template", string tags = "stride-template games", string? marker = "4.5.0",
        (string ShortName, string Type)[]? templates = null)
    {
        var path = Path.Combine(folder, $"{id}.{version}.nupkg");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var dependencies = marker is null ? "" : $"""<dependencies><group targetFramework="net10.0"><dependency id="Stride.Core" version="{marker}" /></group></dependencies>""";
        var nuspec = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{SecurityElement.Escape(id)}</id>
                <version>{version}</version>
                <authors>Color-Rise</authors>
                <license type="expression">MIT</license>
                <description>Templates for the tests</description>
                <tags>{tags}</tags>
                <packageTypes><packageType name="{packageType}" /></packageTypes>
                {dependencies}
              </metadata>
            </package>
            """;
        using (var writer = new StreamWriter(zip.CreateEntry($"{id}.nuspec").Open()))
            writer.Write(nuspec);
        foreach (var (shortName, type) in templates ?? [("game", "project")])
        {
            using var writer = new StreamWriter(zip.CreateEntry($"content/{shortName}/.template.config/template.json").Open());
            writer.Write($$"""{ "identity": "{{id}}.{{shortName}}", "shortName": "{{shortName}}", "tags": { "language": "C#", "type": "{{type}}" } }""");
        }
        return path;
    }

    private sealed class FakeStore : ITemplatePackageStore
    {
        public List<TemplatePackageCandidate> Installed { get; } = [];

        public Dictionary<string, IReadOnlyList<TemplatePackageInfo>> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<(string Id, PackageVersion Version, string? ExtraSource)> Installs { get; } = [];

        public List<string> Uninstalls { get; } = [];

        public Func<string, PackageVersion, TemplatePackageCandidate> InstalledAs { get; set; } = (id, version) => Candidate(id, version.ToString());

        public IReadOnlyList<TemplatePackageCandidate> GetInstalled() => Installed;

        public Task<IReadOnlyList<TemplatePackageInfo>> FindInSourcesAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(Sources.TryGetValue(id, out var found) ? found : (IReadOnlyList<TemplatePackageInfo>)[]);

        public Task InstallAsync(string id, PackageVersion version, string? extraSourceFolder, CancellationToken cancellationToken)
        {
            Installs.Add((id, version, extraSourceFolder));
            Installed.Add(InstalledAs(id, version));
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
