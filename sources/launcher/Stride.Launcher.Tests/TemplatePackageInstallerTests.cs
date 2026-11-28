// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Templates;
using Stride.Core;
using Stride.Launcher.Tests.Helpers;
using Xunit;
using static Stride.Launcher.Tests.Helpers.TemplatePackageFixtures;

namespace Stride.Launcher.Tests;

public sealed class TemplatePackageInstallerTests : IDisposable
{
    private static readonly PackageVersion Engine = new("4.5.6");
    private static readonly IReadOnlySet<string> BuiltIn = new HashSet<string>(["Stride.Templates.Games", "Stride.Templates.AssetPacks"], StringComparer.OrdinalIgnoreCase);

    private readonly string folder = Directory.CreateTempSubdirectory("stride-installer-tests").FullName;
    private readonly FakeTemplatePackageStore store = new();
    private readonly TemplatePackageInstaller installer;

    public TemplatePackageInstallerTests()
    {
        installer = new TemplatePackageInstaller(store, Engine, BuiltIn);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void InspectFileReadsTheNuspecAndCountsTheTemplates()
    {
        var path = WriteNupkg(folder, "Foo.Templates", "1.2.0-dev3", templates: [("foo-game", "project"), ("foo-pack-a", "item"), ("foo-pack-b", "item")]);

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
            installer.Refusal(installer.InspectFile(WriteNupkg(folder, "Foo.Templates", "1.0.0", packageType: "Dependency"))));

    [Fact]
    public void APackageWithoutTheTagIsRefused()
        => Assert.Equal("Foo.Templates 1.0.0 is not a Stride template package: it does not have the tag stride-template.",
            installer.Refusal(installer.InspectFile(WriteNupkg(folder, "Foo.Templates", "1.0.0", tags: "game templates"))));

    [Fact]
    public void ABuiltInPackageIsRefused()
        => Assert.Equal("Stride.Templates.Games is one of Stride's own template packages: it comes with Stride.",
            installer.Refusal(installer.InspectFile(WriteNupkg(folder, "Stride.Templates.Games", "4.5.6"))));

    [Fact]
    public void APackageForANewerEngineIsRefusedWithBothVersions()
        => Assert.Equal("Foo.Templates 0.2.0 is made for Stride 4.6.0; this is Stride 4.5.6.",
            installer.Refusal(installer.InspectFile(WriteNupkg(folder, "Foo.Templates", "0.2.0", marker: "4.6.0"))));

    [Fact]
    public void AnUnmarkedPackageIsAccepted()
        => Assert.Null(installer.Refusal(installer.InspectFile(WriteNupkg(folder, "Foo.Templates", "1.0.0", marker: null))));

    [Fact]
    public void TheConfirmationNamesThePackageAndWarnsAboutCode()
    {
        var path = WriteNupkg(folder, "Foo.Templates", "1.0.0");

        var text = installer.Confirmation(installer.InspectFile(path));

        Assert.Contains("Foo.Templates 1.0.0", text);
        Assert.Contains("Color-Rise", text);
        Assert.Contains("MIT", text);
        Assert.Contains(Path.GetFullPath(path), text);
        Assert.Contains("project files and code that run when", text);
    }

    [Fact]
    public async Task FindByIdWithoutAVersionTakesTheNewestCompatibleOnePrereleasesIncludedAndReadsItsDownload()
    {
        Offer("1.0.0", "4.5.0");
        Offer("1.1.0-dev2", "4.5.6", templates: [("foo-game", "project"), ("foo-pack", "item")]);
        Offer("2.0.0", "4.6.0");

        var info = await installer.FindByIdAsync("Foo.Templates", null);

        Assert.Equal(new PackageVersion("1.1.0-dev2"), info.Version);
        // Read from the download, not from the listing: the type and the contents are known before the question
        Assert.Equal(["Foo.Templates 1.1.0-dev2"], store.Downloads);
        Assert.True(info.IsTemplatePackage);
        Assert.Equal(1, info.GameTemplates);
        Assert.Equal(1, info.AssetPacks);
        Assert.Equal("https://feed.example/v3/index.json", info.Origin);
        Assert.True(File.Exists(info.FilePath));
        Assert.Empty(store.Installs);

        TemplatePackageInstaller.Discard(info);

        Assert.False(File.Exists(info.FilePath));
    }

    [Fact]
    public async Task APackageRefusedByWhatTheSourceListsIsNotDownloaded()
    {
        Offer("1.0.0", "4.5.0");
        Offer("2.0.0", "4.6.0");

        var info = await installer.FindByIdAsync("Foo.Templates", new PackageVersion("2.0.0"));

        Assert.Equal(new PackageVersion("2.0.0"), info.Version);
        Assert.Equal("Foo.Templates 2.0.0 is made for Stride 4.6.0; this is Stride 4.5.6.", installer.Refusal(info));
        Assert.Empty(store.Downloads);
        Assert.Null(info.FilePath);
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
    public async Task AddingAFileAddsThatFile()
    {
        var path = WriteNupkg(folder, "Foo.Templates", "1.0.0");

        await installer.AddAsync(installer.InspectFile(path));

        Assert.Equal([("Foo.Templates", new PackageVersion("1.0.0"), Path.GetFullPath(path))], store.Installs);
    }

    [Fact]
    public async Task AddingByIdAddsTheFileThatWasRead()
    {
        Offer("1.0.0", "4.5.0");
        var info = await installer.FindByIdAsync("Foo.Templates", null);

        await installer.AddAsync(info);

        Assert.Equal([("Foo.Templates", new PackageVersion("1.0.0"), info.FilePath!)], store.Installs);
        TemplatePackageInstaller.Discard(info);
    }

    [Fact]
    public async Task APackageOfTheSourcesThatIsNotOfTheTemplateTypeIsRefusedBeforeAnyInstall()
    {
        // The source lists the tag, but not the package type: the download says it
        Offer("1.0.0", "4.5.0", packageType: "Dependency");

        var info = await installer.FindByIdAsync("Foo.Templates", null);

        Assert.Equal("Foo.Templates 1.0.0 is not a template package: it does not declare the NuGet package type Template.", installer.Refusal(info));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => installer.AddAsync(info));
        Assert.Contains("package type Template", error.Message);
        Assert.Empty(store.Installs);
        TemplatePackageInstaller.Discard(info);
    }

    [Fact]
    public void DiscardLeavesAUsersFileAlone()
    {
        var path = WriteNupkg(folder, "Foo.Templates", "1.0.0");

        TemplatePackageInstaller.Discard(installer.InspectFile(path));

        Assert.True(File.Exists(path));
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

    /// <summary>Lists a version of Foo.Templates in the sources, with its file for the download.</summary>
    private void Offer(string version, string marker, string packageType = "Template", (string ShortName, string Type)[]? templates = null)
    {
        var feed = Directory.CreateDirectory(Path.Combine(folder, "feed", version)).FullName;
        store.SourceFiles[$"Foo.Templates {version}"] = WriteNupkg(feed, "Foo.Templates", version, packageType: packageType, marker: marker, templates: templates);
        store.Sources["Foo.Templates"] = [.. store.Sources.GetValueOrDefault("Foo.Templates") ?? [], Info("Foo.Templates", version, marker)];
    }

    private static TemplatePackageInfo Info(string id, string version, string? marker)
        => new(id, new PackageVersion(version), "Someone", "MIT", "Templates", IsTemplatePackage: null, HasTemplateTag: true,
            marker is null ? null : new PackageVersion(marker), GameTemplates: null, AssetPacks: null, Origin: "https://feed.example/v3/index.json", FilePath: null);
}
