using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorRise.Tessera.Editor.Pictures;
using ColorRise.Tessera.Editor.TileMapEditor;
using Rectangle = Stride.Core.Mathematics.Rectangle;

namespace ColorRise.Tessera.Editor.Avalonia.Controls;

/// <summary>
/// Draws a part of a picture, fitted in the control with hard pixels and its proportions kept, and drawn again when the
/// picture is touched. Where there is no picture to show, a box of the colour of missing tiles with a cross.
/// </summary>
public sealed class PictureView : Control
{
    /// <summary>
    /// The picture.
    /// </summary>
    public static readonly StyledProperty<TilePicture?> PictureProperty = AvaloniaProperty.Register<PictureView, TilePicture?>(nameof(Picture));

    /// <summary>
    /// The pixels of the picture that are drawn; empty for the whole picture.
    /// </summary>
    public static readonly StyledProperty<Rectangle> RegionProperty = AvaloniaProperty.Register<PictureView, Rectangle>(nameof(Region));

    /// <summary>
    /// Whether the control shows a missing tile: a box of <see cref="TileComposer.MissingColor"/> with a cross, of the
    /// proportions of the region.
    /// </summary>
    public static readonly StyledProperty<bool> IsMissingProperty = AvaloniaProperty.Register<PictureView, bool>(nameof(IsMissing));

    private static readonly IImmutableSolidColorBrush MissingBrush = new ImmutableSolidColorBrush(Color.FromArgb(TileComposer.MissingColor.A, TileComposer.MissingColor.R, TileComposer.MissingColor.G, TileComposer.MissingColor.B));
    private static readonly ImmutablePen CrossPen = new(Brushes.Black, 1);

    static PictureView()
    {
        AffectsRender<PictureView>(PictureProperty, RegionProperty, IsMissingProperty);
    }

    /// <inheritdoc cref="PictureProperty"/>
    public TilePicture? Picture
    {
        get => GetValue(PictureProperty);
        set => SetValue(PictureProperty, value);
    }

    /// <inheritdoc cref="RegionProperty"/>
    public Rectangle Region
    {
        get => GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
    }

    /// <inheritdoc cref="IsMissingProperty"/>
    public bool IsMissing
    {
        get => GetValue(IsMissingProperty);
        set => SetValue(IsMissingProperty, value);
    }

    /// <summary>
    /// Draws the region, or the box of a missing tile.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        var picture = Picture;
        var region = Region is { Width: > 0, Height: > 0 } part ? part : picture is null ? new Rectangle(0, 0, 1, 1) : new Rectangle(0, 0, picture.Width, picture.Height);
        if (region.Width <= 0 || region.Height <= 0)
        {
            return;
        }
        var target = Fit(region.Width, region.Height);
        if (target.Width <= 0 || target.Height <= 0)
        {
            return;
        }
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None, EdgeMode = EdgeMode.Aliased }))
        {
            if (IsMissing)
            {
                context.FillRectangle(MissingBrush, target);
                using (context.PushClip(target))
                {
                    context.DrawLine(CrossPen, new Point(target.Left + 0.5, target.Top + 0.5), new Point(target.Right - 0.5, target.Bottom - 0.5));
                    context.DrawLine(CrossPen, new Point(target.Right - 0.5, target.Top + 0.5), new Point(target.Left + 0.5, target.Bottom - 0.5));
                }
            }
            else if (picture is not null && PictureBitmaps.Get(picture) is { } bitmap)
            {
                context.DrawImage(bitmap, new Rect(region.X, region.Y, region.Width, region.Height), target);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PictureProperty && this.IsAttachedToVisualTree())
        {
            if (change.OldValue is TilePicture old)
            {
                old.Touched -= PictureTouched;
            }
            if (change.NewValue is TilePicture picture)
            {
                picture.Touched += PictureTouched;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Picture?.Touched += PictureTouched;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Picture?.Touched -= PictureTouched;
        base.OnDetachedFromVisualTree(e);
    }

    // The largest rectangle of these proportions in the control, in its middle, on whole pixels
    private Rect Fit(int width, int height)
    {
        var scale = Math.Min(Bounds.Width / width, Bounds.Height / height);
        var (fittedWidth, fittedHeight) = (Math.Round(width * scale), Math.Round(height * scale));
        return new Rect(Math.Round((Bounds.Width - fittedWidth) / 2), Math.Round((Bounds.Height - fittedHeight) / 2), fittedWidth, fittedHeight);
    }

    private void PictureTouched(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            InvalidateVisual();
        }
        else
        {
            Dispatcher.UIThread.Post(InvalidateVisual);
        }
    }
}
