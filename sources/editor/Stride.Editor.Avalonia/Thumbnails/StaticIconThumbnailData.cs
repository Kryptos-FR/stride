// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Text;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Storage;

namespace Stride.Editor.Avalonia.Thumbnails;

/// <summary>
/// A <see cref="ThumbnailData"/> implementation that wraps a static icon loaded from an embedded PNG resource.
/// </summary>
/// <remarks>
/// The set of static icons is small and fixed, and each icon is immutable. The decoded <see cref="Bitmap"/>
/// is therefore decoded once per resource and shared across every asset that uses it. This avoids both
/// re-decoding the same PNG and — more importantly — allocating a new GPU-backed <see cref="Bitmap"/> per
/// asset, which would leak one texture per asset and could exhaust GPU memory on large sessions.
/// </remarks>
internal sealed class StaticIconThumbnailData : ThumbnailData
{
    /// <summary>
    /// Shared cache of decoded icons, keyed by resource URI. A <see cref="Lazy{T}"/> guarantees the PNG is
    /// decoded exactly once even under concurrent access, and the resulting <see cref="Bitmap"/> is reused.
    /// </summary>
    private static readonly ConcurrentDictionary<Uri, Lazy<Bitmap?>> Cache = new();

    private readonly Uri resourceUri;

    /// <param name="resourceUri">An <c>avares://</c> URI pointing to the embedded PNG.</param>
    public StaticIconThumbnailData(Uri resourceUri)
        : base(ObjectId.FromBytes(Encoding.UTF8.GetBytes(resourceUri.ToString())))
    {
        this.resourceUri = resourceUri;
    }

    /// <inheritdoc />
    protected override object? BuildImageSource()
    {
        return Cache.GetOrAdd(resourceUri, static uri => new Lazy<Bitmap?>(() =>
        {
            try
            {
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        })).Value;
    }

    /// <inheritdoc />
    protected override void FreeBuildingResources()
    {
        // The decoded Bitmap is owned by the shared cache and reused across assets; nothing to release here.
    }
}
