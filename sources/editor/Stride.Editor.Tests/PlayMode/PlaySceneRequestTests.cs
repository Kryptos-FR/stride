// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Editor.PlayMode;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// The scene that Play this scene asks for: what the Live tab, the status bar and the Output pane say, and how a scene the game
/// loaded is matched to it whether the game's URL has the package's namespace or not.
/// </summary>
public sealed class PlaySceneRequestTests
{
    [Fact]
    public void TheDescriptionNamesTheSceneAndTheOneItReplaces()
    {
        Assert.Equal("Playing Level2 instead of MainScene", new PlaySceneRequest("/MyGame/Scenes/Level2", "Level2", "/MyGame/MainScene", "MainScene").Description);
        Assert.Equal("Level2 is not in the game's built data: the game started on MainScene.", new PlaySceneRequest("/MyGame/Scenes/Level2", "Level2", "/MyGame/MainScene", "MainScene").NotInBuildDescription);
        // A game without a default scene
        Assert.Equal("Playing Level2", new PlaySceneRequest("Scenes/Level2", "Level2").Description);
        Assert.Equal("Level2 is not in the game's built data: the game started without it.", new PlaySceneRequest("Scenes/Level2", "Level2").NotInBuildDescription);
    }

    [Theory]
    [InlineData("/MyGame/Scenes/Level2", true)]
    [InlineData("MyGame/Scenes/Level2", true)]
    [InlineData("Scenes/Level2", true)]
    [InlineData("Level2", true)]
    [InlineData("Scenes/OtherLevel2", false)]
    [InlineData("/MyGame/Scenes/Level3", false)]
    [InlineData("/MyGame/MainScene", false)]
    [InlineData(null, false)]
    public void ASceneTheGameLoadedMatchesWithOrWithoutTheNamespace(string? loadedUrl, bool expected)
    {
        var request = new PlaySceneRequest("/MyGame/Scenes/Level2", "Level2", "/MyGame/MainScene", "MainScene");
        Assert.Equal(expected, request.IsScene(loadedUrl));
    }

    [Fact]
    public void TheDefaultSceneIsKnownByItsUrl()
    {
        var request = new PlaySceneRequest("/MyGame/Scenes/Level2", "Level2", "/MyGame/MainScene", "MainScene");
        Assert.True(request.IsDefaultScene("MainScene"));
        Assert.False(request.IsDefaultScene("/MyGame/Scenes/Level2"));
        Assert.False(new PlaySceneRequest("Scenes/Level2", "Level2").IsDefaultScene("MainScene"));
    }
}
