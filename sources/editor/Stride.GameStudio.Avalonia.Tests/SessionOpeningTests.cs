// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Stride.Assets.Materials;
using Stride.GameStudio.Avalonia.Tests.Helpers;
using Stride.GameStudio.Avalonia.Views;

namespace Stride.GameStudio.Avalonia.Tests;

/// <summary>
/// While a session opens, the main window takes no input: the WPF editor shows its main window only once the session is open.
/// </summary>
public sealed class SessionOpeningTests
{
    [AvaloniaFact]
    public async Task TheMainWindowTakesNoInputWhileASessionOpens()
    {
        using var fixture = ShellSessionFixture.Create(("Material", new MaterialAsset()));
        var window = new MainWindow { DataContext = fixture.Main };
        window.Show();
        try
        {
            await WindowTestHelper.PumpAsync();
            var fileMenu = window.GetVisualDescendants().OfType<Menu>().First().Items.OfType<MenuItem>().First();

            // The session loads on a background thread: the window is disabled from the start of the opening
            var opening = fixture.Main.OpenSession(fixture.PackagePath);
            Assert.True(fixture.Main.IsOpeningSession);
            Assert.False(window.IsEffectivelyEnabled);
            Assert.False(fileMenu.IsEffectivelyEnabled);

            // A click on the File menu does nothing
            Click(window, fileMenu);
            await WindowTestHelper.PumpAsync();
            Assert.False(fileMenu.IsSubMenuOpen);

            Assert.True(await opening);
            await WindowTestHelper.PumpAsync();
            Assert.False(fixture.Main.IsOpeningSession);
            Assert.True(window.IsEffectivelyEnabled);

            // Once the session is open, the same click opens the menu
            Click(window, fileMenu);
            await WindowTestHelper.PumpAsync();
            Assert.True(fileMenu.IsSubMenuOpen);
            fileMenu.Close();
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheMainWindowTakesInputAgainWhenTheOpeningIsCancelled()
    {
        using var fixture = ShellSessionFixture.Create(("Material", new MaterialAsset()));
        var window = new MainWindow { DataContext = fixture.Main };
        window.Show();
        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var opening = fixture.Main.OpenSession(fixture.PackagePath, cancellation.Token);
            Assert.False(window.IsEffectivelyEnabled);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
            await WindowTestHelper.PumpAsync();
            Assert.Null(fixture.Main.Session);
            Assert.False(fixture.Main.IsOpeningSession);
            Assert.True(window.IsEffectivelyEnabled);
        }
        finally
        {
            await window.CloseAsync();
        }
    }

    private static void Click(Window window, Control control)
    {
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
    }
}
