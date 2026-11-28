// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Editor.Templates;
using Stride.Assets.Templates;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Presentation.Avalonia.Services;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.ViewModels;

namespace Stride.Assets.Editor.Avalonia.Templates;

/// <summary>
/// The <see cref="ITemplatePackagesService"/> of the cross-platform Game Studio: shows <see cref="TemplatePackagesWindow"/>
/// modally, over the packages <see cref="DotNetNewTemplateBridge"/> found.
/// </summary>
/// <param name="serviceProvider">The services of the editor; its <see cref="IDialogService"/> asks and picks the files.</param>
public sealed class AvaloniaTemplatePackagesService(IViewModelServiceProvider serviceProvider) : ITemplatePackagesService
{
    /// <inheritdoc/>
    public async Task ShowAsync()
    {
        // The packages found at start, once their scan is done (it runs in the background from the start of the editor)
        await DotNetNewTemplateBridge.ThirdPartyScan;
        var services = new ViewModelServiceProvider(serviceProvider, [DispatcherService.Create()]);
        var viewModel = new TemplatePackagesViewModel(
            services, DotNetNewTemplateBridge.CreateTemplatePackageInstaller(),
            () => DotNetNewTemplateBridge.ThirdPartyPackages, DotNetNewTemplateBridge.RescanThirdPartyAsync);
        try
        {
            await TemplateDialogs.ShowModalAsync(new TemplatePackagesWindow { DataContext = viewModel });
        }
        finally
        {
            viewModel.Destroy();
        }
    }
}
