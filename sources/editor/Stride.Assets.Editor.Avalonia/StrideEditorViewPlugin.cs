// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using Stride.Assets.Editor.Avalonia.Views;
using Stride.Core.Assets.Editor;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Diagnostics;
using Stride.Core.Presentation.Avalonia.Views;
using Stride.Core.Presentation.Views;
using Stride.Editor.Annotations;
using Stride.Editor.Avalonia.Preview;
using Stride.Editor.Build;
using Stride.Editor.Avalonia.Thumbnails;
using Stride.Editor.Preview;
using Stride.Editor.Preview.Views;

namespace Stride.Assets.Editor.Avalonia;

public sealed class StrideEditorViewPlugin : AssetsEditorPlugin
{
    public override void InitializePlugin(ILogger logger)
    {
        // nothing for now
    }

    public override void InitializeSession(ISessionViewModel session)
    {
        var pluginService = session.ServiceProvider.Get<IAssetsPluginService>();
        var previewFactories = new Dictionary<Type, AssetPreviewFactory>();
        foreach (var stridePlugin in pluginService.Plugins.OfType<AssetsEditorPlugin>())
        {
            var pluginAssembly = stridePlugin.GetType().Assembly;
            foreach (var type in pluginAssembly.GetTypes())
            {
                if (typeof(IAssetPreview).IsAssignableFrom(type) &&
                    type.GetCustomAttribute<AssetPreviewAttribute>() is { } attribute)
                {
                    previewFactories.Add(attribute.AssetType, (_, _, _) => (IAssetPreview)Activator.CreateInstance(type)!);
                }
            }
        }

        // GameStudioPreviewService depends on the builder and game-settings services that the agnostic
        // StrideEditorPlugin registers earlier during session initialization. Guard against a changed
        // plugin order (or that plugin failing) so we fail with a clear message instead of an opaque
        // exception deep inside the preview service constructor.
        if (session.ServiceProvider.TryGet<GameStudioBuilderService>() is null ||
            session.ServiceProvider.TryGet<GameSettingsProviderService>() is null)
        {
            GlobalLogger.GetLogger(nameof(StrideEditorViewPlugin)).Error(
                "Cannot create the asset preview service: the builder or game-settings service is not registered. Asset preview will be unavailable.");
        }
        else
        {
            var previewService = new GameStudioPreviewService(session);
            previewService.RegisterAssetPreviewFactories(previewFactories);
            session.ServiceProvider.RegisterService(previewService);
        }

        session.ServiceProvider.RegisterService(new StaticThumbnailService());
    }

    public override void RegisterAssetPreviewViewModelTypes(IDictionary<Type, Type> assetPreviewViewModelTypes)
    {
        // nothing for now
    }

    public override void RegisterAssetPreviewViewTypes(IDictionary<Type, Type> assetPreviewViewTypes)
    {
        var pluginAssembly = GetType().Assembly;
        foreach (var type in pluginAssembly.GetTypes())
        {
            if (typeof(IPreviewView).IsAssignableFrom(type))
            {
                foreach (var attribute in type.GetCustomAttributes<AssetPreviewViewAttribute>())
                {
                    assetPreviewViewTypes.Add(attribute.AssetPreviewType, type);
                }
            }
        }
    }

    public override void RegisterPrimitiveTypes(ICollection<Type> primitiveTypes)
    {
        // nothing for now
    }

    public override void RegisterTemplateProviders(ICollection<ITemplateProvider> templateProviders)
    {
        foreach (var (_, value) in new EntityPropertyTemplateProviders())
        {
            if (value is TemplateProviderBase provider)
            {
                templateProviders.Add(provider);
            }
        }
    }
}
