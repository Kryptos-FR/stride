// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Templates;
using Stride.Core.Presentation.Collections;
using Stride.Core.Presentation.Commands;
using Stride.Core.Presentation.Services;
using Stride.Core.Presentation.ViewModels;

namespace Stride.Assets.Editor.Templates;

/// <summary>
/// The <em>Template packages</em> window of the start screen: the third-party template packages Game Studio found, and adding
/// one (from a .nupkg file, or by package id) or removing one. The checks, the texts and the install are the ones of
/// <c>stride template</c> (<see cref="TemplatePackageInstaller"/>); after each change the packages are scanned again, so the
/// start screen shows the new templates without a restart.
/// </summary>
public sealed class TemplatePackagesViewModel : DispatcherViewModel
{
    private readonly TemplatePackageInstaller installer;
    private readonly Func<Task<IReadOnlyList<ThirdPartyTemplatePackage>>> rescan;

    /// <param name="serviceProvider">The services; <see cref="IDialogService"/> asks and picks the files.</param>
    /// <param name="installer">Adds and removes the packages.</param>
    /// <param name="current">The packages found by the last scan (see <see cref="DotNetNewTemplateBridge.ThirdPartyPackages"/>).</param>
    /// <param name="rescan">Scans again and gets what was found (see <see cref="DotNetNewTemplateBridge.RescanThirdPartyAsync"/>).</param>
    public TemplatePackagesViewModel(IViewModelServiceProvider serviceProvider, TemplatePackageInstaller installer,
        Func<IReadOnlyList<ThirdPartyTemplatePackage>> current, Func<Task<IReadOnlyList<ThirdPartyTemplatePackage>>> rescan)
        : base(serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(rescan);
        this.installer = installer;
        this.rescan = rescan;
        AddFromFileCommand = new AnonymousTaskCommand(serviceProvider, AddFromFileAsync);
        AddByIdCommand = new AnonymousTaskCommand(serviceProvider, AddByIdAsync);
        DependentProperties.Add(nameof(IsBusy), [nameof(IsIdle)]);
        ShowPackages(current());
    }

    /// <summary>
    /// One row per third-party template package found, by id.
    /// </summary>
    public ObservableList<TemplatePackageRowViewModel> Packages { get; } = [];

    /// <summary>
    /// Whether a package was found (else the window says there is none).
    /// </summary>
    public bool HasPackages
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The package id to add with <see cref="AddByIdCommand"/>.
    /// </summary>
    public string? PackageId
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// The version to add with <see cref="AddByIdCommand"/>; empty for the newest version made for this engine.
    /// </summary>
    public string? PackageVersion
    {
        get;
        set => SetValue(ref field, value);
    }

    /// <summary>
    /// Whether a package is being added or removed.
    /// </summary>
    public bool IsBusy
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// The opposite of <see cref="IsBusy"/>, for the views (the buttons are enabled while idle).
    /// </summary>
    public bool IsIdle => !IsBusy;

    /// <summary>
    /// The error of the last add or remove, or null.
    /// </summary>
    public string? Error
    {
        get;
        private set => SetValue(ref field, value);
    }

    /// <summary>
    /// Picks a .nupkg file and adds it.
    /// </summary>
    public ICommandBase AddFromFileCommand { get; }

    /// <summary>
    /// Adds <see cref="PackageId"/> (at <see cref="PackageVersion"/>, when given) from the configured package sources.
    /// </summary>
    public ICommandBase AddByIdCommand { get; }

    private IDialogService Dialogs => ServiceProvider.Get<IDialogService>();

    /// <summary>
    /// What <see cref="AddFromFileCommand"/> does.
    /// </summary>
    public async Task AddFromFileAsync()
    {
        var file = await Dialogs.OpenFilePickerAsync(null, [new FilePickerFilter("NuGet packages") { Patterns = ["*.nupkg"] }]);
        if (file is null)
            return;
        await AddAsync(() => Task.FromResult(installer.InspectFile(file.ToOSPath())));
    }

    /// <summary>
    /// What <see cref="AddByIdCommand"/> does.
    /// </summary>
    public async Task AddByIdAsync()
    {
        if (string.IsNullOrWhiteSpace(PackageId))
            return;
        var id = PackageId.Trim();
        Core.PackageVersion? version = null;
        if (!string.IsNullOrWhiteSpace(PackageVersion))
        {
            if (!Core.PackageVersion.TryParse(PackageVersion.Trim(), out var parsed))
            {
                Error = $"{PackageVersion.Trim()} is not a package version.";
                return;
            }
            version = parsed;
        }
        await AddAsync(() => installer.FindByIdAsync(id, version));
    }

    internal async Task RemoveAsync(TemplatePackageRowViewModel row)
    {
        var removal = installer.PlanRemoval(row.Id);
        if (removal.Kind != TemplatePackageRemovalKind.Uninstall)
        {
            await Dialogs.MessageBoxAsync(removal.Message, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (await Dialogs.MessageBoxAsync(removal.Message, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        await RunAsync(() => installer.RemoveAsync(removal));
    }

    private async Task AddAsync(Func<Task<TemplatePackageInfo>> find)
    {
        TemplatePackageInfo package;
        Error = null;
        IsBusy = true;
        try
        {
            package = await find();
        }
        catch (InvalidOperationException e)
        {
            Error = e.Message;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        if (installer.Refusal(package) is { } refusal)
        {
            await Dialogs.MessageBoxAsync(refusal, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (await Dialogs.MessageBoxAsync(installer.Confirmation(package), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        await RunAsync(() => installer.AddAsync(package));
    }

    /// <summary>
    /// Runs an add or a remove, then scans again; a failure goes in <see cref="Error"/>.
    /// </summary>
    private async Task RunAsync(Func<Task> change)
    {
        Error = null;
        IsBusy = true;
        try
        {
            await change();
            var found = await rescan();
            await Dispatcher.InvokeAsync(() => ShowPackages(found));
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            Error = e.Message;
        }
        catch (Exception e)
        {
            // NuGet reports a failed restore with its own exception types
            Error = e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowPackages(IReadOnlyList<ThirdPartyTemplatePackage> packages)
    {
        Packages.Clear();
        Packages.AddRange(packages.Select(package => new TemplatePackageRowViewModel(ServiceProvider, package, this)));
        HasPackages = Packages.Count > 0;
    }
}

/// <summary>
/// One package of the <em>Template packages</em> window.
/// </summary>
public sealed class TemplatePackageRowViewModel : DispatcherViewModel
{
    private readonly TemplatePackagesViewModel owner;

    internal TemplatePackageRowViewModel(IViewModelServiceProvider serviceProvider, ThirdPartyTemplatePackage package, TemplatePackagesViewModel owner)
        : base(serviceProvider)
    {
        this.owner = owner;
        var candidate = package.Package;
        Id = candidate.Id;
        Version = candidate.Version.ToString();
        Authors = candidate.Authors;
        License = candidate.License;
        Contents = package.Installed ? TemplatePackageInstaller.DescribeContents(package.GameTemplates, package.AssetPacks) : null;
        CanRemove = candidate.LocalFolderSource is null;
        Note = package.ConflictPackage is not null
            ? $"Not shown: a template has the same identity as one of {package.ConflictPackage}."
            : candidate.LocalFolderSource is { } folder ? TemplatePackageInstaller.LocalFolderMessage(candidate.Id, folder) : null;
        RemoveCommand = new AnonymousTaskCommand(serviceProvider, RemoveAsync);
    }

    public string Id { get; }

    public string Version { get; }

    public string Authors { get; }

    /// <summary>
    /// The licence expression or URL, null when the package gives none.
    /// </summary>
    public string? License { get; }

    /// <summary>
    /// "1 game template, 2 asset packs"; null when the package is not shown.
    /// </summary>
    public string? Contents { get; }

    /// <summary>
    /// Why the package is not shown, or the local-folder source it is found in; null for neither.
    /// </summary>
    public string? Note { get; }

    public bool HasNote => Note is not null;

    /// <summary>
    /// Whether the package can be removed: not for a package of a local-folder source, whose files Game Studio does not delete.
    /// </summary>
    public bool CanRemove { get; }

    /// <summary>
    /// Asks, then removes every version of the package from the global packages folder.
    /// </summary>
    public ICommandBase RemoveCommand { get; }

    /// <summary>
    /// What <see cref="RemoveCommand"/> does.
    /// </summary>
    public Task RemoveAsync() => owner.RemoveAsync(this);
}
