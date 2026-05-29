// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Services;

namespace Stride.Editor.Avalonia.Thumbnails;

/// <summary>
/// An <see cref="IThumbnailService"/> that immediately returns type-based static PNG icons.
/// Intended as a lightweight placeholder until the full GPU-based thumbnail builder is ported.
/// </summary>
public sealed class StaticThumbnailService : IThumbnailService
{
    private const string BaseUri = "avares://Stride.Editor.Avalonia/Resources/Thumbnails/";

    private static readonly Dictionary<string, Uri> TypeNameToUri = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AnimationAsset"] = new(BaseUri + "AnimationThumbnail.png"),
        ["GameSettingsAsset"] = new(BaseUri + "GameSettingsThumbnail.png"),
        ["GraphicsCompositorAsset"] = new(BaseUri + "GraphicsCompositorThumbnail.png"),
        ["SceneAsset"] = new(BaseUri + "SceneThumbnail.png"),
        ["ScriptSourceFileAsset"] = new(BaseUri + "ScriptSourceFileThumbnail.png"),
        ["SkeletonAsset"] = new(BaseUri + "SkeletonThumbnail.png"),
        ["SkyboxAsset"] = new(BaseUri + "SkyboxThumbnail.png"),
        ["SoundAsset"] = new(BaseUri + "SoundThumbnail.png"),
        ["UILibraryAsset"] = new(BaseUri + "UILibraryThumbnail.png"),
        ["VideoAsset"] = new(BaseUri + "VideoThumbnail.png"),
    };

    private static readonly Uri FallbackUri = new(BaseUri + "DefaultThumbnail.png");

    // One thumbnail data instance per icon, shared across every asset that uses it: all assets of a given
    // type point at the same (already-decoded) instance, so there is nothing to recompute or allocate per asset.
    private readonly ConcurrentDictionary<Uri, StaticIconThumbnailData> thumbnailData = new();

    public event EventHandler<ThumbnailCompletedArgs>? ThumbnailCompleted;

    public void AddThumbnailAssetItems(IEnumerable<AssetItem> assetItems, QueuePosition position)
    {
        foreach (var item in assetItems)
        {
            var uri = TypeNameToUri.TryGetValue(item.Asset.GetType().Name, out var u) ? u : FallbackUri;
            var data = thumbnailData.GetOrAdd(uri, static u => new StaticIconThumbnailData(u));
            ThumbnailCompleted?.Invoke(this, new ThumbnailCompletedArgs(item.Id, data));
        }
    }

    public void IncreaseThumbnailPriority(IEnumerable<AssetItem> assetItems)
    {
        // No queue — all thumbnails are static; nothing to prioritise.
    }

    public bool HasStaticThumbnail(Type assetType) => true;

    public void Dispose()
    {
        // Nothing to dispose.
    }
}
