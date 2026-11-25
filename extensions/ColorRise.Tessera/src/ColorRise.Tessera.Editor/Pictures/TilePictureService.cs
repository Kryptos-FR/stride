using System.Runtime.InteropServices;
using ColorRise.Tessera.Editor.ViewModels;
using Stride.Assets.Editor.ViewModels.SpriteEditor;
using Stride.Core.Assets;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization;
using Stride.Graphics;

namespace ColorRise.Tessera.Editor.Pictures;

/// <summary>
/// The pictures of the session's tile sets, over one image cache. Used on the session's dispatcher.
/// </summary>
public sealed class TilePictureService : IDisposable
{
    private readonly ISessionViewModel session;
    // The sprite editor's cache: it reads an image again when its file changed
    private readonly SpriteEditorImageCache images = new();
    private readonly Dictionary<AssetId, TileSetPictures> tileSets = [];
    // A picture per file and color key, made again when the cache gives another image for the file
    private readonly Dictionary<(string Path, Color? Key), (Image Image, TilePicture Picture)> pictures = [];

    /// <summary>
    /// Makes the service of a session; the editor plugin registers it.
    /// </summary>
    public TilePictureService(ISessionViewModel session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    /// <summary>
    /// The service registered for a session.
    /// </summary>
    public static TilePictureService Of(ISessionViewModel session) => session.ServiceProvider.Get<TilePictureService>();

    /// <summary>
    /// The pictures of a tile set: one object per asset.
    /// </summary>
    public TileSetPictures Get(TileSetViewModel tileSet)
    {
        ArgumentNullException.ThrowIfNull(tileSet);
        if (!tileSets.TryGetValue(tileSet.Id, out var found) || found.TileSet != tileSet)
        {
            found?.Detach();
            found = new TileSetPictures(this, tileSet);
            tileSets[tileSet.Id] = found;
        }
        return found;
    }

    /// <summary>
    /// The pictures of the tile set a reference names, as a slot of a map holds it; null when no asset of the session has its id.
    /// </summary>
    public TileSetPictures? Get(TileSet? reference)
    {
        if (reference is null || AttachedReferenceManager.GetAttachedReference(reference) is not { } attached)
        {
            return null;
        }
        return session.GetAssetById(attached.Id) is TileSetViewModel { IsDeleted: false } tileSet ? Get(tileSet) : null;
    }

    /// <summary>
    /// Stops following the session's assets, and lets go of the images.
    /// </summary>
    public void Dispose()
    {
        foreach (var tileSet in tileSets.Values)
        {
            tileSet.Detach();
        }
        tileSets.Clear();
        pictures.Clear();
        images.Dispose();
    }

    /// <summary>
    /// The picture of an image file with the color key applied, or null and why, with the file's name.
    /// </summary>
    internal TilePicture? Picture(UFile path, Color? key, out string? problem)
    {
        problem = null;
        var name = path.GetFileName();
        if (!File.Exists(path))
        {
            problem = $"The image '{name}' is not there ({path}).";
            return null;
        }
        Image? image;
        try
        {
            image = images.RetrieveImage(path);
        }
        catch (Exception exception)
        {
            problem = $"The image '{name}' cannot be read: {exception.Message}";
            return null;
        }
        if (image is null)
        {
            problem = $"The image '{name}' cannot be read.";
            return null;
        }

        var entry = (path.FullPath, key);
        if (pictures.TryGetValue(entry, out var made) && ReferenceEquals(made.Image, image))
        {
            return made.Picture;
        }
        var picture = ToPicture(image, key);
        pictures[entry] = (image, picture);
        return picture;
    }

    // The cache gives 8-bit RGBA or BGRA pixels
    private static TilePicture ToPicture(Image image, Color? key)
    {
        var buffer = image.PixelBuffer[0];
        var picture = new TilePicture(buffer.Width, buffer.Height);
        var row = new byte[buffer.Width * 4];
        for (var y = 0; y < buffer.Height; y++)
        {
            Marshal.Copy(buffer.DataPointer + y * buffer.RowStride, row, 0, row.Length);
            MemoryMarshal.Cast<byte, Color>(row).CopyTo(picture.Pixels.AsSpan(y * buffer.Width));
        }
        var bgra = buffer.Format is PixelFormat.B8G8R8A8_UNorm or PixelFormat.B8G8R8A8_UNorm_SRgb;
        var pixels = picture.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            if (bgra)
            {
                pixels[i] = new Color(pixels[i].B, pixels[i].G, pixels[i].R, pixels[i].A);
            }
            // As the texture build applies the key: all four channels alike
            if (pixels[i] == key)
            {
                pixels[i] = Color.Transparent;
            }
        }
        return picture;
    }
}
