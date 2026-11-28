// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#nullable enable
using Stride.Assets.Templates;
using Stride.Core;
using Xunit;
using static Stride.Assets.Tests.TemplatePackageFolder;

namespace Stride.Assets.Tests;

public sealed class TestThirdPartyTemplatePackages : IDisposable
{
    private static readonly PackageVersion Engine = new("4.5.6");

    private readonly string root = Directory.CreateTempSubdirectory("stride-third-party-tests").FullName;
    private readonly DotNetNewTemplateRegistry registry;
    private readonly List<string> builtIn = [];
    private readonly ThirdPartyTemplatePackages packages;

    public TestThirdPartyTemplatePackages()
    {
        registry = new DotNetNewTemplateRegistry("4.5.6", Path.Combine(root, "profile"));
        packages = new ThirdPartyTemplatePackages(registry, Engine, new HashSet<string>(["Stride.Templates.Games"], StringComparer.OrdinalIgnoreCase), () => builtIn);
    }

    public void Dispose()
    {
        registry.Dispose();
        Directory.Delete(root, recursive: true);
    }

    private string Store => Path.Combine(root, "packages");

    private TemplatePackageCandidate Candidate(string id, string version, params Template[] templates)
        => new(id, new PackageVersion(version), IsTemplatePackage: true, HasTemplateTag: true, MarkerFloor: new PackageVersion("4.5.0"),
            InstallSource: Write(Store, id, version, templates), LocalFolderSource: null, Authors: "Someone", License: "MIT", Description: "Templates");

    private async Task<string> InstallBuiltIn(string id, params Template[] templates)
    {
        var folder = Write(Store, id, "4.5.6", templates);
        Assert.True((await registry.InstallPackageAsync(folder)).Success);
        builtIn.Add(folder);
        return folder;
    }

    private async Task<IReadOnlyList<string>> Identities() => (await registry.GetTemplatesAsync()).Select(template => template.Identity).Order().ToList();

    [Fact]
    public async Task AResolvedPackageIsInstalledAndReportedWithItsIdentities()
    {
        var foo = Candidate("Foo.Templates", "1.0.0", new Template("foo-game", "Foo.Game"), new Template("foo-pack", "Foo.Pack", Item: true));

        var result = await packages.ScanAsync([foo]);

        var package = Assert.Single(result);
        Assert.True(package.Installed);
        Assert.Equal(foo, package.Package);
        Assert.Equal(["Foo.Game", "Foo.Pack"], package.TemplateIdentities.Order());
        Assert.Equal(["Foo.Game", "Foo.Pack"], await Identities());
        Assert.Same(result, packages.Packages);
    }

    [Fact]
    public async Task APackageTheListingNoLongerReturnsIsRemovedButABuiltInOneIsKept()
    {
        await InstallBuiltIn("Stride.Templates.Games", new Template("game", "Stride.Game"));
        var foo = Candidate("Foo.Templates", "1.0.0", new Template("foo-game", "Foo.Game"));
        await packages.ScanAsync([foo]);

        var result = await packages.ScanAsync([]);

        Assert.Empty(result);
        Assert.Equal(["Stride.Game"], await Identities());
    }

    [Fact]
    public async Task ANewerCompatibleVersionReplacesTheOlderOne()
    {
        var old = Candidate("Foo.Templates", "1.0.0", new Template("foo-game", "Foo.Game"));
        await packages.ScanAsync([old]);
        var newer = Candidate("Foo.Templates", "1.1.0", new Template("foo-game", "Foo.Game"));

        var result = await packages.ScanAsync([old, newer]);

        Assert.Equal(new PackageVersion("1.1.0"), Assert.Single(result).Package.Version);
        var template = Assert.Single(await registry.GetTemplatesAsync());
        Assert.Equal(Path.GetFullPath(newer.InstallSource), Path.GetFullPath(template.MountPointUri).TrimEnd('/', '\\'));
    }

    [Fact]
    public async Task APackageThatClashesWithABuiltInIdentityIsReportedAndNotInstalled()
    {
        await InstallBuiltIn("Stride.Templates.Games", new Template("game", "Stride.Game"));
        var bar = Candidate("Bar.Templates", "1.0.0", new Template("bar-game", "Bar.Game"), new Template("copy", "Stride.Game"));

        var result = await packages.ScanAsync([bar]);

        var package = Assert.Single(result);
        Assert.False(package.Installed);
        Assert.Equal("Stride.Game", package.ConflictIdentity);
        Assert.Equal("stride.templates.games", package.ConflictPackage);
        Assert.Empty(package.TemplateIdentities);
        Assert.Equal(["Stride.Game"], await Identities());
    }

    [Fact]
    public async Task OfTwoThirdPartyPackagesWithOneIdentityTheFirstByIdStays()
    {
        var zeta = Candidate("Zeta.Templates", "1.0.0", new Template("zeta-game", "Shared.Game"));
        var alpha = Candidate("Alpha.Templates", "1.0.0", new Template("alpha-game", "Shared.Game"));

        var result = await packages.ScanAsync([zeta, alpha]);

        Assert.Equal(["Alpha.Templates", "Zeta.Templates"], result.Select(package => package.Package.Id));
        Assert.True(result[0].Installed);
        Assert.False(result[1].Installed);
        Assert.Equal("Alpha.Templates", result[1].ConflictPackage);
        var template = Assert.Single(await registry.GetTemplatesAsync());
        Assert.Equal(Path.GetFullPath(alpha.InstallSource), Path.GetFullPath(template.MountPointUri).TrimEnd('/', '\\'));
    }

    [Fact]
    public async Task APinnedIdIsNeverScanned()
    {
        var games = Candidate("Stride.Templates.Games", "9.0.0", new Template("game", "Other.Game"));

        Assert.Empty(await packages.ScanAsync([games]));
        Assert.Empty(await Identities());
    }

    [Fact]
    public async Task OnlyAThirdPartyPackIsNotStridesAssetPacks()
    {
        var bar = Candidate("Bar.Templates", "1.0.0", new Template("bar-pack", "Bar.Pack", Item: true));
        await packages.ScanAsync([bar]);
        var assetPacks = Path.Combine(Store, "stride.templates.assetpacks", "4.5.6");

        Assert.False(DotNetNewTemplateBridge.HasBuiltInAssetPacks(await registry.GetTemplatesAsync(), assetPacks));
        Assert.False(DotNetNewTemplateBridge.HasBuiltInAssetPacks(await registry.GetTemplatesAsync(), null));
    }

    [Fact]
    public async Task APackFromTheAssetPacksFolderIsStridesAssetPacks()
    {
        var assetPacks = await InstallBuiltIn("Stride.Templates.AssetPacks", new Template("stride-pack", "Stride.Pack", Item: true));
        await packages.ScanAsync([Candidate("Bar.Templates", "1.0.0", new Template("bar-pack", "Bar.Pack", Item: true))]);

        Assert.True(DotNetNewTemplateBridge.HasBuiltInAssetPacks(await registry.GetTemplatesAsync(), assetPacks));
    }
}
