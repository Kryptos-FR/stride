// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Templates;
using Stride.Core;
using Xunit;

namespace Stride.Launcher.Tests;

public sealed class TemplatePackageRuleTests
{
    private static readonly PackageVersion Engine = new("4.5.6");
    private static readonly IReadOnlySet<string> NoPins = new HashSet<string>();

    private static TemplatePackageCandidate Candidate(string id, string version, string? marker = "4.5.0", bool isTemplate = true, bool tagged = true)
        => new(id, new PackageVersion(version), isTemplate, tagged, marker is null ? null : new PackageVersion(marker),
            InstallSource: $"/store/{id}/{version}", LocalFolderSource: null, Authors: "Someone", License: "MIT", Description: "Templates");

    private static IReadOnlyList<TemplatePackageCandidate> Select(PackageVersion engine, params TemplatePackageCandidate[] candidates)
        => TemplatePackageRule.Select(candidates, engine, NoPins);

    [Fact]
    public void APackageWithoutTheTemplateTypeIsNotSelected()
        => Assert.Empty(Select(Engine, Candidate("Foo.Templates", "1.0.0", isTemplate: false)));

    [Fact]
    public void APackageWithoutTheTagIsNotSelected()
        => Assert.Empty(Select(Engine, Candidate("Foo.Templates", "1.0.0", tagged: false)));

    [Fact]
    public void APinnedIdIsNotSelectedWhateverItsCase()
    {
        var pinned = new HashSet<string>(["Stride.Templates.Games"], StringComparer.OrdinalIgnoreCase);
        var selected = TemplatePackageRule.Select([Candidate("stride.templates.games", "4.5.6")], Engine, pinned);
        Assert.Empty(selected);
    }

    [Fact]
    public void TheNewestVersionWhoseMarkerIsNotAboveTheEngineIsSelected()
    {
        var selected = Select(Engine,
            Candidate("Foo.Templates", "1.0.0", "4.4.0"),
            Candidate("Foo.Templates", "1.2.0", "4.5.0"),
            Candidate("Foo.Templates", "1.1.0", "4.5.0"),
            Candidate("Foo.Templates", "2.0.0", "4.6.0"));
        Assert.Equal(new PackageVersion("1.2.0"), Assert.Single(selected).Version);
    }

    [Fact]
    public void AMarkerAboveTheEngineIsSkipped()
        => Assert.Empty(Select(Engine, Candidate("Foo.Templates", "1.0.0", "4.5.7")));

    [Theory]
    [InlineData("4.5.6", "4.5.6")]
    [InlineData("4.5.6-dev1", "4.5.6-dev1")]
    public void AMarkerEqualToTheEngineIsTaken(string marker, string engine)
        => Assert.Single(Select(new PackageVersion(engine), Candidate("Foo.Templates", "1.0.0", marker)));

    [Fact]
    public void AnUnmarkedVersionIsTakenWhenNoMarkedVersionFits()
    {
        var selected = Select(Engine,
            Candidate("Foo.Templates", "1.0.0", marker: null),
            Candidate("Foo.Templates", "2.0.0", "4.6.0"));
        Assert.Equal(new PackageVersion("1.0.0"), Assert.Single(selected).Version);
    }

    [Fact]
    public void AMarkedFittingVersionWinsOverANewerUnmarkedOne()
    {
        var selected = Select(Engine,
            Candidate("Foo.Templates", "3.0.0", marker: null),
            Candidate("Foo.Templates", "1.0.0", "4.5.0"));
        Assert.Equal(new PackageVersion("1.0.0"), Assert.Single(selected).Version);
    }

    [Fact]
    public void IdsGroupWithoutCase()
    {
        var selected = Select(Engine,
            Candidate("Foo.Templates", "1.0.0"),
            Candidate("foo.templates", "1.1.0"));
        Assert.Equal(new PackageVersion("1.1.0"), Assert.Single(selected).Version);
    }

    [Fact]
    public void TheResultIsOrderedById()
    {
        var selected = Select(Engine,
            Candidate("Zeta.Templates", "1.0.0"),
            Candidate("alpha.Templates", "1.0.0"),
            Candidate("Beta.Templates", "1.0.0"));
        Assert.Equal(["alpha.Templates", "Beta.Templates", "Zeta.Templates"], selected.Select(candidate => candidate.Id));
    }

    [Fact]
    public void MarkerFloorReadsTheStrideCoreDependencyWhateverItsCase()
    {
        Tuple<string, PackageVersionRange>[] dependencies =
        [
            Tuple.Create("Other.Package", new PackageVersionRange(new PackageVersion("9.0.0"))),
            Tuple.Create("stride.core", new PackageVersionRange(new PackageVersion("4.5.1"))),
        ];
        Assert.Equal(new PackageVersion("4.5.1"), TemplatePackageRule.MarkerFloor(dependencies));
    }

    [Fact]
    public void MarkerFloorIsNullWithoutTheStrideCoreDependency()
        => Assert.Null(TemplatePackageRule.MarkerFloor([Tuple.Create("Other.Package", new PackageVersionRange(new PackageVersion("9.0.0")))]));

    [Theory]
    [InlineData(null, "4.5.6", false)]
    [InlineData("4.5.6", "4.5.6", true)]
    [InlineData("4.5.5", "4.5.6", true)]
    [InlineData("4.5.7", "4.5.6", false)]
    public void IsCompatibleNeedsAMarkerNoNewerThanTheEngine(string? marker, string engine, bool expected)
        => Assert.Equal(expected, TemplatePackageRule.IsCompatible(marker is null ? null : new PackageVersion(marker), new PackageVersion(engine)));
}
