// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Headless.XUnit;
using Stride.Assets.Materials;
using Stride.GameStudio.Avalonia.Tests.Helpers;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// File > New waits for the session being opened: its Ctrl+N is a key binding of the main window, and those run even while the
/// window is disabled.
/// </summary>
public sealed class FileNewTests
{
    [AvaloniaFact]
    public async Task FileNewWaitsForTheSessionToOpen()
    {
        using var fixture = ShellSessionFixture.Create(("Material", new MaterialAsset()));

        var opening = fixture.Main.OpenSession(fixture.PackagePath);
        Assert.False(fixture.Main.NewSessionCommand.IsEnabled);

        Assert.True(await opening);
        Assert.True(fixture.Main.NewSessionCommand.IsEnabled);
    }
}
