// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#nullable enable
using Stride.Assets.Templates;
using Xunit;
using static Stride.Assets.Tests.TemplatePackageFolder;

namespace Stride.Assets.Tests;

public sealed class TestDotNetNewTemplateRegistry : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("stride-registry-tests").FullName;
    private readonly DotNetNewTemplateRegistry registry;

    public TestDotNetNewTemplateRegistry()
    {
        registry = new DotNetNewTemplateRegistry("4.5.6", Path.Combine(root, "profile"));
    }

    public void Dispose()
    {
        registry.Dispose();
        Directory.Delete(root, recursive: true);
    }

    private string Packages => Path.Combine(root, "packages");

    [Fact]
    public async Task APackageWithNewIdentitiesIsInstalledAndListed()
    {
        var folder = Write(Packages, "Foo.Templates", "1.0.0", new Template("foo-game", "Foo.Game"), new Template("foo-pack", "Foo.Pack", Item: true));

        var result = await registry.InstallThirdPartyPackageAsync(folder, new Dictionary<string, string>());

        Assert.True(result.Installed);
        Assert.Null(result.ConflictIdentity);
        Assert.Equal(["Foo.Game", "Foo.Pack"], (await registry.GetTemplatesAsync()).Select(template => template.Identity).Order());
    }

    [Fact]
    public async Task APackageWithAnIdentityAlreadyInstalledIsNotKept()
    {
        var first = Write(Packages, "Stride.Templates.Games", "4.5.6", new Template("game", "Shared.Identity"));
        var second = Write(Packages, "Bar.Templates", "1.0.0", new Template("bar-game", "Bar.Game"), new Template("bar-copy", "Shared.Identity"));
        Assert.True((await registry.InstallPackageAsync(first)).Success);

        var result = await registry.InstallThirdPartyPackageAsync(second, DotNetNewTemplateRegistry.TakenBy([first]));

        Assert.False(result.Installed);
        Assert.Equal("Shared.Identity", result.ConflictIdentity);
        Assert.Equal(Path.GetFullPath(first), Path.GetFullPath(result.ConflictSource!).TrimEnd('/', '\\'));
        var templates = await registry.GetTemplatesAsync();
        var shared = Assert.Single(templates);
        Assert.Equal("Shared.Identity", shared.Identity);
        Assert.Equal(Path.GetFullPath(first), Path.GetFullPath(shared.MountPointUri).TrimEnd('/', '\\'));
    }

    [Fact]
    public async Task InstallingTheSameFolderAgainUnchangedDoesNotReinstall()
    {
        var folder = Write(Packages, "Foo.Templates", "1.0.0", new Template("foo-game", "Foo.Game"));
        File.WriteAllText(Path.Combine(folder, "foo.templates.1.0.0.nupkg"), "not read");
        File.SetLastWriteTimeUtc(Path.Combine(folder, "foo.templates.1.0.0.nupkg"), DateTime.UtcNow.AddMinutes(-5));
        Assert.True((await registry.InstallThirdPartyPackageAsync(folder, new Dictionary<string, string>())).Installed);
        var installedAt = await registry.GetLastChangeTimeAsync(folder);

        Assert.True(await registry.IsUnchangedAsync(folder));
        var again = await registry.InstallThirdPartyPackageAsync(folder, new Dictionary<string, string>());

        Assert.True(again.Installed);
        Assert.Equal(installedAt, await registry.GetLastChangeTimeAsync(folder));
    }

    [Fact]
    public async Task ANewerNupkgInTheFolderIsAChange()
    {
        var folder = Write(Packages, "Foo.Templates", "1.0.0", new Template("foo-game", "Foo.Game"));
        var nupkg = Path.Combine(folder, "foo.templates.1.0.0.nupkg");
        File.WriteAllText(nupkg, "not read");
        File.SetLastWriteTimeUtc(nupkg, DateTime.UtcNow.AddMinutes(-5));
        Assert.True((await registry.InstallThirdPartyPackageAsync(folder, new Dictionary<string, string>())).Installed);

        File.SetLastWriteTimeUtc(nupkg, DateTime.UtcNow.AddMinutes(5));

        Assert.False(await registry.IsUnchangedAsync(folder));
    }

    [Fact]
    public void TheIdentitiesAreReadFromAFolderAndFromANupkg()
    {
        var folder = Write(Packages, "Foo.Templates", "1.0.0", new Template("foo-game", "Foo.Game"), new Template("foo-pack", "Foo.Pack", Item: true));
        var nupkg = Path.Combine(root, "foo.templates.1.0.0.nupkg");
        System.IO.Compression.ZipFile.CreateFromDirectory(folder, nupkg);

        Assert.Equal(["Foo.Game", "Foo.Pack"], DotNetNewTemplateRegistry.ReadTemplateIdentities(folder).Order());
        Assert.Equal(["Foo.Game", "Foo.Pack"], DotNetNewTemplateRegistry.ReadTemplateIdentities(nupkg).Order());
    }

    [Fact]
    public async Task ANotInstalledSourceIsAChange()
        => Assert.False(await registry.IsUnchangedAsync(Write(Packages, "Foo.Templates", "1.0.0", new Template("foo-game", "Foo.Game"))));
}
