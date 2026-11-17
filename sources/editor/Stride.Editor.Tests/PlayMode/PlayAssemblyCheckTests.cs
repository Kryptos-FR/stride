// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using Stride.Editor.PlayMode;
using Stride.Engine;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// Tests <see cref="PlayAssemblyCheck"/>: the game's assemblies compared with the editor's by their build.
/// </summary>
public sealed class PlayAssemblyCheckTests
{
    // Not a Stride assembly: compared by build
    private static readonly Assembly GameAssembly = typeof(FactAttribute).Assembly;
    private static readonly Assembly Engine = typeof(Entity).Assembly;

    [Fact]
    public void TheSameBuildIsSame()
    {
        var check = Create();

        check.Add([Info(GameAssembly, GameAssembly.ManifestModule.ModuleVersionId)]);

        Assert.Equal(PlayAssemblyStatus.Same, check.GetStatus(Name(GameAssembly)));
        Assert.Empty(check.StaleAssemblies);
    }

    [Fact]
    public void AnotherBuildIsStale()
    {
        var check = Create();

        check.Add([Info(GameAssembly, Guid.NewGuid())]);

        Assert.Equal(PlayAssemblyStatus.Stale, check.GetStatus(Name(GameAssembly)));
        Assert.Equal([Name(GameAssembly)], check.StaleAssemblies);
    }

    [Fact]
    public void AnotherBuildOfTheSameTypesIsSame()
    {
        // The editor loads the game's code built for itself, the game runs its platform's build: the members decide
        var check = Create();

        check.Add([Info(GameAssembly, Guid.NewGuid(), PlayAssemblyShape.Compute(GameAssembly))]);

        Assert.Equal(PlayAssemblyStatus.Same, check.GetStatus(Name(GameAssembly)));
        Assert.Empty(check.StaleAssemblies);
    }

    [Fact]
    public void AnotherBuildWithOtherMembersIsStale()
    {
        var check = Create();

        check.Add([Info(GameAssembly, Guid.NewGuid(), Guid.NewGuid())]);

        Assert.Equal(PlayAssemblyStatus.Stale, check.GetStatus(Name(GameAssembly)));
    }

    [Fact]
    public void AnAssemblyTheEditorDoesNotHaveIsMissing()
    {
        var check = Create();

        check.Add([new PlayAssemblyInfo("MyGame", "1.0.0.0", Guid.NewGuid())]);

        Assert.Equal(PlayAssemblyStatus.Missing, check.GetStatus("MyGame"));
        Assert.Equal(PlayAssemblyStatus.Missing, check.GetStatus("NotReported"));
    }

    [Fact]
    public void AnAssemblyTheGameDidNotReportIsSame()
    {
        Assert.Equal(PlayAssemblyStatus.Same, Create().GetStatus(Name(GameAssembly)));
    }

    [Fact]
    public void StrideAssembliesAreComparedByVersion()
    {
        // The editor and the game can load the same version of the engine from different builds
        var check = Create();

        check.Add([Info(Engine, Guid.NewGuid())]);
        Assert.Equal(PlayAssemblyStatus.Same, check.GetStatus(Name(Engine)));

        check.Add([new PlayAssemblyInfo(Name(Engine), "1.2.3.4", Guid.NewGuid())]);
        Assert.Equal(PlayAssemblyStatus.Stale, check.GetStatus(Name(Engine)));
    }

    [Fact]
    public void ANewReportReplacesTheStatus()
    {
        // What a reload of the game's code does: the game reports the new build
        var check = Create();
        check.Add([Info(GameAssembly, Guid.NewGuid())]);
        Assert.Equal(PlayAssemblyStatus.Stale, check.GetStatus(Name(GameAssembly)));

        check.Add([Info(GameAssembly, GameAssembly.ManifestModule.ModuleVersionId)]);

        Assert.Equal(PlayAssemblyStatus.Same, check.GetStatus(Name(GameAssembly)));
    }

    private static PlayAssemblyCheck Create()
        => new(name => name == Name(GameAssembly) ? GameAssembly : name == Name(Engine) ? Engine : null);

    private static PlayAssemblyInfo Info(Assembly assembly, Guid mvid, Guid shape = default)
        => new(Name(assembly), assembly.GetName().Version?.ToString(), mvid, shape);

    private static string Name(Assembly assembly) => assembly.GetName().Name!;
}
