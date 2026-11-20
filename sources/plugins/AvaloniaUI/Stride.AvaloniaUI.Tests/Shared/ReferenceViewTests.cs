// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics.Regression;
using Stride.Rendering.Compositing;
using Xunit;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// Draws each reference view in a game and compares the frame with its gold image.
/// </summary>
/// <remarks>
/// The golds are the frames of the whole-frame Skia renderer drawing on the CPU. The native renderer, which draws through
/// Skia on the CPU, is checked against the same images.
/// </remarks>
public sealed class ReferenceViewTests : GameTestBase
{
    // A game registers the plugins it references before it creates its systems; the test runner loads the plugin lazily
    static ReferenceViewTests() => RuntimeHelpers.RunModuleConstructor(typeof(AvaloniaUISystem).Module.ModuleHandle);

    public ReferenceViewTests()
    {
        GraphicsDeviceManager.PreferredBackBufferWidth = ReferenceViews.Width;
        GraphicsDeviceManager.PreferredBackBufferHeight = ReferenceViews.Height;
    }

    /// <summary>
    /// Gets the names of the pages, one test case each.
    /// </summary>
    public static IEnumerable<object[]> PageNames => ReferenceViews.Pages.Select(page => new object[] { page });

    [Theory]
    [MemberData(nameof(PageNames))]
    public void PageMatchesItsGold(string page) => TestAvalonia.Run(() =>
    {
        var game = new ReferenceViewTests();
        var component = new AvaloniaUIComponent
        {
            Content = ReferenceViews.Create(page),
            Resolution = new Vector2(ReferenceViews.Width, ReferenceViews.Height),
        };
        var scene = new Scene();
        scene.Entities.Add(new Entity("UI") { component });
        game.SceneSystem.SceneInstance = new SceneInstance(game.Services, scene);
        game.SceneSystem.GraphicsCompositor = new GraphicsCompositor { Game = new ClearRenderer { Color = Color.Black } };
        // The golds are Skia's frames on the CPU, where the native renderer draws too
        game.Services.GetSafeServiceAs<AvaloniaUISystem>().RenderOnGpu = false;

        game.FrameGameSystem.IsUnitTestFeeding = true;
        // Read the back buffer once everything else is drawn
        game.FrameGameSystem.DrawOrder = int.MaxValue;
        game.FrameGameSystem
            // Away from the page, so that no control shows its pointer-over look
            .Update(1, () => game.MouseSimulated.SetPosition(new Vector2(2, 2)))
            .Draw(60, () =>
            {
                using var frame = game.GraphicsDevice.Presenter.BackBuffer.GetDataAsImage(game.GraphicsContext.CommandList);
                game.SaveImage(frame, page);
            });

        RunGameTest(game);
    });
}
