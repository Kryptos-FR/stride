using Stride.Core.Assets;

namespace ColorRise.Tessera.Assets;

/// <summary>
/// Makes the flipbook a template creates: one animation, "idle", with one clip None and no frame.
/// </summary>
public sealed class FlipbookAssetFactory : AssetFactory<FlipbookAsset>
{
    /// <summary>
    /// The name of the animation of a new flipbook.
    /// </summary>
    public const string FirstAnimationName = "idle";

    /// <inheritdoc />
    public override FlipbookAsset New() => new()
    {
        Animations =
        {
            new FlipbookAnimation
            {
                Name = FirstAnimationName,
                Clips = { new FlipbookClip { Direction = FlipbookDirection.None } },
            },
        },
    };
}
