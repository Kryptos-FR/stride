using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using ColorRise.Tessera.Rendering;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;
using Stride.Rendering.Sprites;

namespace ColorRise.Tessera.Tests.Graphics;

/// <summary>
/// Runs a game without a window and keeps the pixels of the frames it draws.
/// </summary>
internal static class GraphicsHarness
{
    /// <summary>
    /// Builds the scene once, then for each frame action, runs it, draws a frame and keeps its pixels.
    /// </summary>
    /// <remarks>An exception in an action or in the drawing ends the run and comes out of this method.</remarks>
    public static Frame[] Run(HarnessOptions options, Action<HarnessScene> build, params Action<HarnessScene>[] frames)
    {
        ConfigureSoftwareVulkan();
        using var game = new HarnessGame(options, build, frames);
        game.Run(new GameContextHeadless(options.Width, options.Height));
        game.Error?.Throw();
        return [.. game.Frames];
    }

    // The software driver, unless the caller chose a driver: kept out of Run so that its assembly loads only here.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConfigureSoftwareVulkan()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VK_DRIVER_FILES")))
        {
            Stride.Dependencies.Lavapipe.Lavapipe.TryConfigure();
        }
    }
}

/// <summary>
/// What the actions of a test see of its game: the scene, and an orthographic camera looking down -Z.
/// </summary>
internal sealed class HarnessScene
{
    private const float CameraZ = 10;
    private const float Near = 0.1f;
    private const float Far = 100;

    private readonly int width;
    private readonly int height;
    private Matrix viewProjection;

    public HarnessScene(Game game, Scene scene, Entity camera, int width, int height)
    {
        Game = game;
        Scene = scene;
        Camera = camera;
        this.width = width;
        this.height = height;
        SetView(Vector2.Zero, unitsHigh: 4);
    }

    public Game Game { get; }

    public Scene Scene { get; }

    /// <summary>
    /// The orthographic camera, at z = 10, looking down -Z, with planes at 0.1 and 100.
    /// </summary>
    public Entity Camera { get; }

    /// <summary>
    /// Centres the view on a point of the plane z = 0, with this many units from the bottom of the frame to its top.
    /// </summary>
    public void SetView(Vector2 center, float unitsHigh, Quaternion? cameraRotation = null)
    {
        var rotation = cameraRotation ?? Quaternion.Identity;
        Camera.Transform.Position = new Vector3(center, CameraZ);
        Camera.Transform.Rotation = rotation;
        var camera = Camera.Get<CameraComponent>();
        camera.OrthographicSize = unitsHigh;

        var view = Matrix.Invert(Matrix.RotationQuaternion(rotation) * Matrix.Translation(center.X, center.Y, CameraZ));
        var projection = Matrix.OrthoRH(unitsHigh * width / height, unitsHigh, Near, Far);
        viewProjection = view * projection;
    }

    /// <summary>
    /// The pixel that shows a point of the plane z = 0, with y going down.
    /// </summary>
    public Int2 ToPixel(Vector2 world)
    {
        var clip = Vector3.TransformCoordinate(new Vector3(world, 0), viewProjection);
        return new Int2((int)MathF.Floor((clip.X + 1) / 2 * width), (int)MathF.Floor((1 - clip.Y) / 2 * height));
    }

    public Entity Add(string name, params EntityComponent[] components)
    {
        var entity = new Entity(name);
        foreach (var component in components)
        {
            entity.Add(component);
        }
        Scene.Entities.Add(entity);
        return entity;
    }
}

/// <summary>
/// The test game: builds in LoadContent, runs one frame action per Update, and reads the back buffer after each Draw.
/// </summary>
internal sealed class HarnessGame : Game
{
    private readonly HarnessOptions options;
    private readonly Action<HarnessScene> build;
    private readonly Action<HarnessScene>[] frameActions;
    private const int MaxDraws = 600;

    private HarnessScene? scene;
    private int actionsRun;
    private int draws;
    private PickingRenderer? picking;

    public HarnessGame(HarnessOptions options, Action<HarnessScene> build, Action<HarnessScene>[] frameActions)
    {
        this.options = options;
        this.build = build;
        this.frameActions = frameActions;

        AutoLoadDefaultSettings = false;
        GraphicsDeviceManager.PreferredBackBufferWidth = options.Width;
        GraphicsDeviceManager.PreferredBackBufferHeight = options.Height;
        GraphicsDeviceManager.PreferredColorSpace = options.ColorSpace;
        GraphicsDeviceManager.PreferredDepthStencilFormat = PixelFormat.D24_UNorm_S8_UInt;
        // No test shares its pipelines with the next one
        GraphicsDeviceManager.PersistPipelineCache = false;
        // A game without a window counts as minimized, which caps it at 15 frames per second
        MinimizedMinimumUpdateRate.MinimumElapsedTime = TimeSpan.Zero;
    }

    public List<Frame> Frames { get; } = [];

    public ExceptionDispatchInfo? Error { get; private set; }

    protected override async Task LoadContent()
    {
        await base.LoadContent();
        try
        {
            var slot = new SceneCameraSlot();
            var sceneRoot = new Scene();
            var camera = new Entity("camera")
            {
                new CameraComponent
                {
                    Projection = CameraProjectionMode.Orthographic,
                    NearClipPlane = 0.1f,
                    FarClipPlane = 100,
                    Slot = slot.ToSlotId(),
                },
            };
            sceneRoot.Entities.Add(camera);

            SceneSystem.GraphicsCompositor = CreateCompositor(slot);
            SceneSystem.SceneInstance = new SceneInstance(Services, sceneRoot);
            scene = new HarnessScene(this, sceneRoot, camera, options.Width, options.Height);
            build(scene);
        }
        catch (Exception error)
        {
            Fail(error);
            return;
        }

        if (frameActions.Length == 0)
        {
            Exit();
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (Error is not null)
        {
            return;
        }
        try
        {
            // Runs the scripts, LoadContent among them
            base.Update(gameTime);
            // One action per frame read, whatever the number of updates between two draws
            if (Error is null && scene is not null && actionsRun == Frames.Count && actionsRun < frameActions.Length)
            {
                frameActions[actionsRun++](scene);
            }
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        if (Error is not null)
        {
            return;
        }
        try
        {
            base.Draw(gameTime);
            // A test that never ends its run fails instead of hanging
            if (++draws > MaxDraws)
            {
                throw new TimeoutException($"The game drew {MaxDraws} frames and its actions are not done.");
            }
            if (actionsRun == Frames.Count + 1)
            {
                Frames.Add(ReadBackBuffer());
                if (Frames.Count == frameActions.Length)
                {
                    Exit();
                }
            }
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    private void Fail(Exception error)
    {
        Error = ExceptionDispatchInfo.Capture(error);
        Exit();
    }

    private Frame ReadBackBuffer()
    {
        var backBuffer = GraphicsDevice.Presenter.BackBuffer;
        using var image = backBuffer.GetDataAsImage(GraphicsContext.CommandList);
        var buffer = image.PixelBuffer[0];
        var bgra = buffer.Format is PixelFormat.B8G8R8A8_UNorm or PixelFormat.B8G8R8A8_UNorm_SRgb;
        var pixels = new Color[buffer.Width * buffer.Height];
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                var pixel = buffer.GetPixel<Color>(x, y);
                pixels[y * buffer.Width + x] = bgra ? new Color(pixel.B, pixel.G, pixel.R, pixel.A) : pixel;
            }
        }
        return new Frame(buffer.Width, buffer.Height, pixels, picking?.Ids);
    }

    // A forward renderer without post effects that draws sprites, tiles and flipbooks, a renderer of its own when multisampled, or the test's own
    // drawing in place of the scene; then the picking pass when the options ask for it.
    private GraphicsCompositor CreateCompositor(SceneCameraSlot slot)
    {
        var opaque = new RenderStage("Opaque", "Main") { SortMode = new StateChangeSortMode() };
        var transparent = new RenderStage("Transparent", "Main") { SortMode = options.TransparentSortMode ?? new BackToFrontSortMode() };

        ISceneRenderer view = options.CustomDraw is { } customDraw
            ? new SceneRendererCollection
            {
                new ClearRenderer { Color = options.Background },
                new DelegateSceneRenderer(context => customDraw(context, context.RenderContext.RenderView.ViewProjection)),
            }
            : options.Msaa > 1
            ? new MultisampleRenderer
            {
                Background = options.Background,
                OpaqueStage = opaque,
                TransparentStage = transparent,
                Samples = (MultisampleCount)options.Msaa,
            }
            : new ForwardRenderer
            {
                Clear = { Color = options.Background },
                OpaqueRenderStage = opaque,
                TransparentRenderStage = transparent,
            };

        var tiles = new TileRenderFeature
        {
            RenderStageSelectors = { new TileTransparentRenderStageSelector { TransparentRenderStage = transparent } },
        };
        // The flipbooks' feature, with the selector of the tiles' feature: it puts any render object in its stage
        var flipbooks = new FlipbookRenderFeature
        {
            RenderStageSelectors = { new TileTransparentRenderStageSelector { TransparentRenderStage = transparent } },
        };
        var compositor = new GraphicsCompositor
        {
            Cameras = { slot },
            RenderStages = { opaque, transparent },
            RenderFeatures =
            {
                new SpriteRenderFeature
                {
                    RenderStageSelectors =
                    {
                        new SpriteTransparentRenderStageSelector
                        {
                            EffectName = "Test",
                            OpaqueRenderStage = opaque,
                            TransparentRenderStage = transparent,
                        },
                    },
                },
                tiles,
                flipbooks,
            },
        };

        if (options.Picking)
        {
            // The stage the scene editor makes, with the selector the editor's picking service gives the tile feature for it
            var stage = new RenderStage("Picking", "Picking");
            compositor.RenderStages.Add(stage);
            tiles.RenderStageSelectors.Add(new TileTransparentRenderStageSelector { TransparentRenderStage = stage });
            picking = new PickingRenderer { Stage = stage };
            view = new SceneRendererCollection { view, picking };
        }
        compositor.Game = new SceneCameraRenderer { Camera = slot, Child = view };
        return compositor;
    }
}
