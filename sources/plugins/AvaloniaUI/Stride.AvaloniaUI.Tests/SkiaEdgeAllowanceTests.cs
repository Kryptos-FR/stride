// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Graphics.Regression;
using Stride.Rendering.Compositing;
using Xunit;
using Image = Stride.Graphics.Image;

namespace Stride.AvaloniaUI.Tests;

/// <summary>
/// Measures how many pixels of each shape case and reference view Skia draws on the GPU more than 12 away from what it draws
/// on the CPU: the allowance the native renderer's edges get against the fallback.
/// </summary>
/// <remarks>
/// The counts are written into <see cref="ShapeCases.EdgeAllowance"/> and <see cref="ReferenceViews.EdgeAllowance"/>; a
/// failure gives the count measured.
/// </remarks>
public sealed class SkiaEdgeAllowanceTests : GameTestBase
{
    // A difference of up to 12 is how far apart the anti-aliasing of two good renderers is on one edge pixel
    private const int Tolerance = 12;

    // A game registers the plugins it references before it creates its systems; the test runner loads the plugin lazily
    static SkiaEdgeAllowanceTests() => RuntimeHelpers.RunModuleConstructor(typeof(AvaloniaUISystem).Module.ModuleHandle);

    /// <summary>
    /// Gets the names of the shape cases and of the pages, one test case each.
    /// </summary>
    public static IEnumerable<object[]> CasesAndPages => ShapeCases.Names.Concat(ReferenceViews.Pages).Select(name => new object[] { name });

    [SkippableTheory]
    [MemberData(nameof(CasesAndPages))]
    public void SkiaOnTheGpuStaysWithinTheAllowance(string name)
    {
        Skip.IfNot(GraphicsDevice.Platform == GraphicsPlatform.Vulkan, $"Skia renders on the GPU with Vulkan only, and this game runs on {GraphicsDevice.Platform}.");
        TestAvalonia.Run(() =>
        {
            var isPage = ReferenceViews.Pages.Contains(name);
            var (cpu, gpu) = isPage
                ? RenderBothWays(ReferenceViews.Create(name), ReferenceViews.Width, ReferenceViews.Height, 1)
                : RenderBothWays(ShapeCases.Create(name), ShapeCases.Width, ShapeCases.Height, ShapeCases.GetScaling(name));
            var allowance = isPage ? ReferenceViews.EdgeAllowance[name] : ShapeCases.EdgeAllowance[name];

            using (cpu)
            using (gpu)
            {
                var count = FrameComparison.CountBeyond(cpu, gpu, Tolerance);
                if (count > allowance)
                {
                    var directory = Path.Combine(Path.GetTempPath(), "Stride.AvaloniaUI.Tests");
                    Directory.CreateDirectory(directory);
                    foreach (var (frame, suffix) in new[] { (cpu, "cpu"), (gpu, "gpu") })
                    {
                        using var file = File.Create(Path.Combine(directory, $"{name}.{suffix}.png"));
                        frame.Save(file, ImageFileType.Png);
                    }
                }
                Assert.True(count <= allowance, $"Skia on the GPU draws {count} pixels of {name} more than {Tolerance} away from Skia on the CPU; the allowance is {allowance}.");
            }
        });
    }

    // Skia on the CPU, then on the GPU, in one game
    private static (Image Cpu, Image Gpu) RenderBothWays(Control content, int width, int height, double scaling)
    {
        var game = new SkiaEdgeAllowanceTests();
        game.GraphicsDeviceManager.PreferredBackBufferWidth = width;
        game.GraphicsDeviceManager.PreferredBackBufferHeight = height;
        var component = new AvaloniaUIComponent { Content = content, Resolution = new Vector2((float)(width / scaling), (float)(height / scaling)) };
        var scene = new Scene();
        scene.Entities.Add(new Entity("UI") { component });
        game.SceneSystem.SceneInstance = new SceneInstance(game.Services, scene);
        game.SceneSystem.GraphicsCompositor = new GraphicsCompositor { Game = new ClearRenderer { Color = Color.Black } };
        var system = game.Services.GetSafeServiceAs<AvaloniaUISystem>();
        system.RenderOnGpu = false;
        Image? cpu = null;
        Image? gpu = null;
        bool? onGpu = null;

        game.FrameGameSystem.IsUnitTestFeeding = true;
        // Read the back buffer once everything else is drawn
        game.FrameGameSystem.DrawOrder = int.MaxValue;
        game.FrameGameSystem
            // Away from the view, so that no control shows its pointer-over look
            .Update(1, () => game.MouseSimulated.SetPosition(new Vector2(2, 2)))
            .Draw(30, () => cpu = ReadBackBuffer(game))
            .Update(31, () => system.RenderOnGpu = true)
            .Draw(45, () =>
            {
                onGpu = system.IsRenderingOnGpu;
                gpu = ReadBackBuffer(game);
            })
            // Skia on the GPU keeps the layer of a bitmap cache while the view shows it, and that memory would outlive the device
            .Update(46, () => component.Content = null);
        RunGameTest(game);

        Assert.True(onGpu, "Skia doesn't draw the view on the GPU.");
        return (cpu!, gpu!);
    }

    private static Image ReadBackBuffer(Game game) => game.GraphicsDevice.Presenter.BackBuffer.GetDataAsImage(game.GraphicsContext.CommandList);
}
