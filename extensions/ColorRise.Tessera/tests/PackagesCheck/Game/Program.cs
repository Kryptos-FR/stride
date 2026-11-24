using ColorRise.Tessera;
using ColorRise.Tessera.Rendering;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;

// Software Vulkan, as Stride's own graphics tests use, unless a driver is chosen
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VK_DRIVER_FILES")))
{
    Stride.Dependencies.Lavapipe.Lavapipe.TryConfigure();
}

using var game = new CheckGame();
game.Run(new GameContextHeadless(CheckGame.Side, CheckGame.Side));
Console.WriteLine(game.Result);
return game.Result == CheckGame.Passed ? 0 : 1;

/// <summary>
/// Loads the map its build compiled, draws one frame of its four cells, and reads four pixels: red, green, blue, yellow.
/// </summary>
internal sealed class CheckGame : Game
{
    public const int Side = 64;
    public const string Passed = "TESSERA-PACKAGES-CHECK OK";

    public CheckGame()
    {
        AutoLoadDefaultSettings = false;
        GraphicsDeviceManager.PreferredBackBufferWidth = Side;
        GraphicsDeviceManager.PreferredBackBufferHeight = Side;
        GraphicsDeviceManager.PreferredColorSpace = ColorSpace.Gamma;
        // The forward renderer reads the depth buffer, which a game without a window has none of by default
        GraphicsDeviceManager.PreferredDepthStencilFormat = PixelFormat.D24_UNorm_S8_UInt;
        // A game without a window counts as minimized, which caps it at 15 frames per second
        MinimizedMinimumUpdateRate.MinimumElapsedTime = TimeSpan.Zero;
    }

    public string Result { get; private set; } = "TESSERA-PACKAGES-CHECK FAILED: no frame was drawn";

    protected override async Task LoadContent()
    {
        await base.LoadContent();
        try
        {
            var slot = new SceneCameraSlot();
            var scene = new Scene();
            // Two units high, centred on the map's four cells: each cell is 32 pixels wide
            var camera = new Entity("camera")
            {
                new CameraComponent { Projection = CameraProjectionMode.Orthographic, OrthographicSize = 2, NearClipPlane = 0.1f, FarClipPlane = 100, Slot = slot.ToSlotId() },
            };
            camera.Transform.Position = new Vector3(1, 1, 10);
            scene.Entities.Add(camera);
            scene.Entities.Add(new Entity("map") { new TileMapComponent { Map = Content.Load<TileMap>("Maps/Town") } });

            SceneSystem.GraphicsCompositor = Compositor(slot);
            SceneSystem.SceneInstance = new SceneInstance(Services, scene);
        }
        catch (Exception error)
        {
            Result = $"TESSERA-PACKAGES-CHECK FAILED: {error}";
            Exit();
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        if (SceneSystem.SceneInstance is null)
        {
            return;
        }

        using var image = GraphicsDevice.Presenter.BackBuffer.GetDataAsImage(GraphicsContext.CommandList);
        var buffer = image.PixelBuffer[0];
        var bgra = buffer.Format is PixelFormat.B8G8R8A8_UNorm or PixelFormat.B8G8R8A8_UNorm_SRgb;
        Color Pixel(int x, int y)
        {
            var pixel = buffer.GetPixel<Color>(x, y);
            return bgra ? new Color(pixel.B, pixel.G, pixel.R, pixel.A) : pixel;
        }

        var expected = new[] { new Color(255, 0, 0, 255), new Color(0, 255, 0, 255), new Color(0, 0, 255, 255), new Color(255, 255, 0, 255) };
        var actual = new[] { Pixel(16, 16), Pixel(48, 16), Pixel(16, 48), Pixel(48, 48) };
        Result = expected.SequenceEqual(actual)
            ? Passed
            : $"TESSERA-PACKAGES-CHECK FAILED: the four cells are {string.Join(", ", actual)}, not red, green, blue, yellow";
        Exit();
    }

    // A forward renderer of an opaque and a transparent stage, as a game's compositor has; Tessera's render feature draws the
    // map in the transparent one
    private static GraphicsCompositor Compositor(SceneCameraSlot slot)
    {
        var opaque = new RenderStage("Opaque", "Main") { SortMode = new StateChangeSortMode() };
        var transparent = new RenderStage("Transparent", "Main") { SortMode = new BackToFrontSortMode() };
        return new GraphicsCompositor
        {
            Cameras = { slot },
            RenderStages = { opaque, transparent },
            RenderFeatures =
            {
                new TileRenderFeature { RenderStageSelectors = { new TileTransparentRenderStageSelector { TransparentRenderStage = transparent } } },
            },
            Game = new SceneCameraRenderer
            {
                Camera = slot,
                Child = new ForwardRenderer { Clear = { Color = new Color(255, 0, 255, 255) }, OpaqueRenderStage = opaque, TransparentRenderStage = transparent },
            },
        };
    }
}
