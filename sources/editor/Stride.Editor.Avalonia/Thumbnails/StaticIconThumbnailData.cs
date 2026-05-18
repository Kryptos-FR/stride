// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Text;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Storage;

namespace Stride.Editor.Avalonia.Thumbnails;

/// <summary>
/// A <see cref="ThumbnailData"/> implementation that wraps a static icon loaded from an embedded PNG resource.
/// </summary>
internal sealed class StaticIconThumbnailData : ThumbnailData
{
    private readonly Uri resourceUri;

    /// <param name="resourceUri">An <c>avares://</c> URI pointing to the embedded PNG.</param>
    public StaticIconThumbnailData(Uri resourceUri)
        : base(ObjectId.FromBytes(Encoding.UTF8.GetBytes(resourceUri.ToString())))
    {
        this.resourceUri = resourceUri;
    }

    protected override object? BuildImageSource()
    {
        try
        {
            using var stream = AssetLoader.Open(resourceUri);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    protected override void FreeBuildingResources()
    {
        // Nothing to release — the Bitmap is cached by the base class.
    }
}
