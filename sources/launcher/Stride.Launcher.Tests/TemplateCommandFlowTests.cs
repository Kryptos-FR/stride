// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Templates;
using Stride.Cli.Core;
using Stride.Core;
using Stride.Launcher.Tests.Helpers;
using Xunit;
using static Stride.Launcher.Tests.Helpers.TemplatePackageFixtures;

namespace Stride.Launcher.Tests;

/// <summary>
/// What <c>stride template add</c>, <c>list</c> and <c>remove</c> print and do, over a package store in memory.
/// </summary>
public sealed class TemplateCommandFlowTests : IDisposable
{
    private static readonly PackageVersion Engine = new("4.5.6");

    private readonly string folder = Directory.CreateTempSubdirectory("stride-template-command-tests").FullName;
    private readonly FakeTemplatePackageStore store = new();
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();
    private readonly Queue<string?> answers = new();
    private int questions;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private TemplateCommandFlow Create(bool inputRedirected = false)
    {
        var installer = new TemplatePackageInstaller(store, Engine, new HashSet<string>(["Stride.Templates.Games"], StringComparer.OrdinalIgnoreCase));
        return new TemplateCommandFlow(installer, output, error, () =>
        {
            questions++;
            return answers.Count > 0 ? answers.Dequeue() : null;
        }, inputRedirected);
    }

    [Theory]
    [InlineData(true, false, "Proceed")]
    [InlineData(true, true, "Proceed")]
    [InlineData(false, false, "Ask")]
    [InlineData(false, true, "Refuse")]
    public void TheQuestionIsAskedOnlyOnAConsoleWithoutYes(bool yes, bool inputRedirected, string expected)
        => Assert.Equal(Enum.Parse<TemplateCommandFlow.Gate>(expected), TemplateCommandFlow.Decide(yes, inputRedirected));

    [Theory]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData(" yes ", true)]
    [InlineData("YES", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("n", false)]
    [InlineData("yep", false)]
    public void OnlyYesAnswersYes(string? answer, bool expected)
        => Assert.Equal(expected, TemplateCommandFlow.IsYes(answer));

    [Fact]
    public async Task AddingAFilePrintsTheQuestionAndInstallsOnYes()
    {
        var path = WriteNupkg(folder, "Foo.Templates", "1.0.0");
        answers.Enqueue("y");

        var exit = await Create().AddAsync(path, packageVersion: null, yes: false, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(1, questions);
        var text = output.ToString();
        Assert.Contains("Install the template package Foo.Templates 1.0.0?", text);
        Assert.Contains("Install only packages you trust.", text);
        Assert.Contains("Install? [y/N] ", text);
        Assert.Contains("Installed Foo.Templates 1.0.0.", text);
        Assert.Equal([("Foo.Templates", new PackageVersion("1.0.0"), Path.GetFullPath(path))], store.Installs);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task AddingDoesNothingWithoutYes()
    {
        answers.Enqueue("");

        var exit = await Create().AddAsync(WriteNupkg(folder, "Foo.Templates", "1.0.0"), null, yes: false, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Empty(store.Installs);
        Assert.Contains("Nothing was installed.", output.ToString());
    }

    [Fact]
    public async Task AddingRefusesToAskWhenTheInputIsRedirected()
    {
        var exit = await Create(inputRedirected: true).AddAsync(WriteNupkg(folder, "Foo.Templates", "1.0.0"), null, yes: false, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal(0, questions);
        Assert.Empty(store.Installs);
        Assert.Contains("--yes", error.ToString());
    }

    [Fact]
    public async Task AddingWithYesInstallsWithoutAsking()
    {
        var exit = await Create(inputRedirected: true).AddAsync(WriteNupkg(folder, "Foo.Templates", "1.0.0"), null, yes: true, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(0, questions);
        Assert.Single(store.Installs);
        Assert.DoesNotContain("[y/N]", output.ToString());
    }

    [Fact]
    public async Task ARefusedPackageIsNotInstalled()
    {
        var exit = await Create().AddAsync(WriteNupkg(folder, "Foo.Templates", "0.2.0", marker: "4.6.0"), null, yes: true, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Empty(store.Installs);
        Assert.Equal("Foo.Templates 0.2.0 is made for Stride 4.6.0; this is Stride 4.5.6.", error.ToString().Trim());
    }

    [Fact]
    public async Task AMissingFileIsNamed()
    {
        var path = Path.Combine(folder, "Nope.1.0.0.nupkg");

        var exit = await Create().AddAsync(path, null, yes: true, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal($"{path} does not exist.", error.ToString().Trim());
    }

    [Fact]
    public async Task AddingByIdTakesTheGivenVersion()
    {
        store.Sources["Foo.Templates"] = [Source("1.0.0"), Source("1.1.0")];
        store.SourceFiles["Foo.Templates 1.0.0"] = WriteNupkg(folder, "Foo.Templates", "1.0.0");

        var exit = await Create().AddAsync("Foo.Templates", "1.0.0", yes: true, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(["Foo.Templates 1.0.0"], store.Downloads);
        var install = Assert.Single(store.Installs);
        Assert.Equal(("Foo.Templates", new PackageVersion("1.0.0")), (install.Id, install.Version));
        // The download is deleted once installed
        Assert.False(File.Exists(install.File));
    }

    [Fact]
    public async Task AWrongVersionIsRefused()
    {
        var exit = await Create().AddAsync("Foo.Templates", "one", yes: true, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal("one is not a package version.", error.ToString().Trim());
    }

    [Fact]
    public async Task AnUnknownIdIsSaid()
    {
        var exit = await Create().AddAsync("Nope.Templates", null, yes: true, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal("No package Nope.Templates was found in the package sources.", error.ToString().Trim());
    }

    [Fact]
    public async Task AFailedInstallIsSaid()
    {
        store.InstallFailure = new InvalidDataException("the feed is down");

        var exit = await Create().AddAsync(WriteNupkg(folder, "Foo.Templates", "1.0.0"), null, yes: true, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal("Could not install Foo.Templates 1.0.0: the feed is down", error.ToString().Trim());
    }

    [Fact]
    public void ListingWithoutPackagesSaysSo()
    {
        store.Installed.Add(Candidate("Stride.Templates.Games", "4.5.6"));

        Assert.Equal(0, Create().List());

        Assert.Equal("No template packages besides Stride's.", output.ToString().Trim());
    }

    [Fact]
    public void ListingPrintsALinePerPackageAndTheLocalFolder()
    {
        store.Installed.Add(Candidate("Foo.Templates", "1.0.0"));
        store.Installed.Add(Candidate("Foo.Templates", "1.1.0"));
        store.Installed.Add(Candidate("Mine.Templates", "0.1.0-dev2", localFolder: "/feeds/mine"));

        Assert.Equal(0, Create().List());

        Assert.Equal(
            [
                "Foo.Templates   1.1.0       Someone",
                "Mine.Templates  0.1.0-dev2  Someone",
                "  from /feeds/mine",
            ],
            output.ToString().TrimEnd().Split(Environment.NewLine));
    }

    [Fact]
    public async Task RemovingAsksAndUninstallsOnYes()
    {
        store.Installed.Add(Candidate("Foo.Templates", "1.0.0"));
        answers.Enqueue("yes");

        var exit = await Create().RemoveAsync("foo.templates", yes: false, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("Remove foo.templates (1.0.0) from the global packages folder? [y/N] ", output.ToString());
        Assert.Contains("Removed foo.templates (1.0.0).", output.ToString());
        Assert.Equal(["Foo.Templates 1.0.0"], store.Uninstalls);
    }

    [Fact]
    public async Task RemovingDoesNothingWithoutYes()
    {
        store.Installed.Add(Candidate("Foo.Templates", "1.0.0"));
        answers.Enqueue("n");

        var exit = await Create().RemoveAsync("Foo.Templates", yes: false, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Empty(store.Uninstalls);
        Assert.Contains("Nothing was removed.", output.ToString());
    }

    [Fact]
    public async Task RemovingRefusesToAskWhenTheInputIsRedirected()
    {
        store.Installed.Add(Candidate("Foo.Templates", "1.0.0"));

        var exit = await Create(inputRedirected: true).RemoveAsync("Foo.Templates", yes: false, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal(0, questions);
        Assert.Empty(store.Uninstalls);
    }

    [Theory]
    [InlineData("Stride.Templates.Games", "Stride.Templates.Games is one of Stride's own template packages and cannot be removed.")]
    [InlineData("Nope.Templates", "Nope.Templates is not installed.")]
    [InlineData("Mine.Templates", "Mine.Templates is found in the package source /feeds/mine, which Stride does not change. Take its file out of that folder to remove it.")]
    public async Task WhatCannotBeRemovedIsSaid(string id, string message)
    {
        store.Installed.Add(Candidate("Mine.Templates", "0.1.0", localFolder: "/feeds/mine"));

        var exit = await Create().RemoveAsync(id, yes: true, CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal(message, error.ToString().Trim());
        Assert.Empty(store.Uninstalls);
    }

    private static TemplatePackageInfo Source(string version)
        => new("Foo.Templates", new PackageVersion(version), "Color-Rise", "MIT", "", IsTemplatePackage: true, HasTemplateTag: true,
            new PackageVersion("4.5.0"), GameTemplates: null, AssetPacks: null, Origin: "https://feed.example", FilePath: null);
}
