// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Diagnostics;
using Stride.Core.Serialization;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Editor.PlayMode;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// What the pickers of the live references need: the play session (its built content, its end), the Live tab's tree, the
/// editor that sends the edits.
/// </summary>
internal sealed class LiveReferenceContext(ISessionViewModel session, Func<PlaySession?> playSession, Func<LiveTree?> tree, Func<LiveEditor?> editor, Logger log)
{
    public const string NotInBuildReason = "Not in the game's build: no root asset or scene of the game references it.";
    public const string NoBuildListReason = "The game has not said yet which content it built: try again in a moment.";
    public const string ChangedNote = "Changed since the game was built: the game loads it as it was built.";

    public ISessionViewModel Session => session;

    public PlaySession? PlaySession => playSession() is { State: PlaySessionState.Connected } connected ? connected : null;

    public LiveTree? Tree => tree();

    public LiveEditor? Editor => editor();

    public Logger Log => log;

    /// <summary>
    /// Why an asset cannot be set to a live reference: the game loads content from its build only.
    /// </summary>
    public string? ReasonFor(AssetViewModel asset)
    {
        if (PlaySession?.BuildContent is null)
            return NoBuildListReason;
        return BuildUrlOf(asset) is null ? NotInBuildReason : null;
    }

    /// <summary>
    /// The URL of an asset's content in the game's build: its location, or its location without the package's namespace (the
    /// game names the content of its own package so); <c>null</c> when the build does not have it.
    /// </summary>
    public string? BuildUrlOf(AssetViewModel asset)
    {
        if (PlaySession?.BuildContent is not { } build)
            return null;
        if (build.Contains(asset.Url))
            return asset.Url;
        var unqualified = asset.AssetItem.UnqualifiedUrl.FullPath;
        return build.Contains(unqualified) ? unqualified : null;
    }

    /// <summary>
    /// A reference of the given type to an asset's content, by its URL in the game's build.
    /// </summary>
    public object? CreateReference(AssetViewModel asset, Type referenceType)
    {
        var url = BuildUrlOf(asset) ?? asset.Url;
        if (UrlReferenceBase.IsUrlReferenceType(referenceType))
            return UrlReferenceBase.New(referenceType, asset.AssetItem.Id, url);
        var contentType = AssetRegistry.GetContentType(asset.AssetItem.Asset.GetType());
        return contentType is not null && contentType.IsAssignableTo(referenceType) ? AttachedReferenceManager.CreateProxyObject(contentType, asset.AssetItem.Id, url) : null;
    }

    /// <summary>
    /// The note on an asset that can be set: it changed since the game was built.
    /// </summary>
    public string? NoteFor(AssetViewModel asset)
        => PlaySession is { } play && asset.AssetItem.ModifiedTime > play.StartTime ? ChangedNote : null;

    /// <summary>
    /// A token cancelled when the play session ends (the game stopped, its connection was lost): a picker open for it closes.
    /// </summary>
    public (CancellationToken Token, IDisposable Registration) WatchEnd()
    {
        var source = new CancellationTokenSource();
        if (PlaySession is not { } play)
        {
            source.Cancel();
            return (source.Token, source);
        }
        void OnStateChanged(object? sender, PlaySessionStateChange change)
        {
            if (change.NewState != PlaySessionState.Connected)
                source.Cancel();
        }
        play.StateChanged += OnStateChanged;
        if (play.State != PlaySessionState.Connected)
            source.Cancel();
        return (source.Token, new Registration(() =>
        {
            play.StateChanged -= OnStateChanged;
            source.Dispose();
        }));
    }

    private sealed class Registration(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}

/// <summary>
/// The Pick button of a live reference to content: the asset picker lists the assets of the reference's type, those of the
/// game's build pickable and the others disabled with the reason, and the asset picked goes to the game by URL.
/// </summary>
internal sealed class LivePickupAssetCommand(LiveReferenceContext context) : PickupAssetCommand(context.Session)
{
    /// <summary>
    /// The pickers opened.
    /// </summary>
    public int Opened { get; private set; }

    protected override async Task<PickerResult> ShowPicker(IReadOnlyCollection<INodePresenter> nodePresenters, object? currentValue, object? parameter)
    {
        var targetType = parameter as Type ?? nodePresenters.First().Type;
        if (context.PlaySession is null || Session.ServiceProvider.TryGet<IAssetPickerService>() is not { } pickers)
            return new PickerResult { ProcessChange = false };
        var current = GetCurrentTarget(currentValue!);
        var (token, registration) = context.WatchEnd();
        using (registration)
        {
            ++Opened;
            var options = new AssetPickerOptions(GetAssetTypes(targetType))
            {
                Filter = x => FilterAsset(x, targetType),
                DisabledReason = context.ReasonFor,
                Note = context.NoteFor,
                InitialAsset = current,
                InitialLocation = current?.Directory,
                Message = "Assets of the game's build can be set while it runs; the others are listed disabled.",
                Cancellation = token,
            };
            var picked = await pickers.OpenAssetPickerAsync(Session, options);
            // The game stopped meanwhile: nothing is written
            if (picked is null || token.IsCancellationRequested || context.ReasonFor(picked) is not null)
                return new PickerResult { ProcessChange = false };
            if (context.NoteFor(picked) is { } note)
                context.Log.Info($"{picked.Url}: {note}");
            return new PickerResult { ProcessChange = true, NewValue = picked };
        }
    }

    protected override object CreateReference(AssetViewModel asset, Type referenceType) => context.CreateReference(asset, referenceType)!;

    protected override void ExecuteSync(INodePresenter nodePresenter, object? parameter, object? preExecuteResult)
    {
        // A picker closed without a pick writes nothing: in the game, writing the current value again would be an edit
        if (preExecuteResult is PickerResult { ProcessChange: true })
            base.ExecuteSync(nodePresenter, parameter, preExecuteResult);
    }
}

/// <summary>
/// The Pick button of a live reference to an entity or a component: the picker lists the entities of the running game (the
/// Live tab's tree) and, for a component, the components of the picked entity of the reference's type. The pick goes to the
/// game by the entity's handle and the component's id.
/// </summary>
internal sealed class LivePickupEntityCommand(LiveReferenceContext context, bool pickComponent) : ChangeValueWithPickerCommandBase
{
    /// <summary>
    /// The pickers opened.
    /// </summary>
    public int Opened { get; private set; }

    public override string Name => pickComponent ? PickupEntityComponentCommand.CommandName : PickupEntityCommand.CommandName;

    public override CombineMode CombineMode => CombineMode.CombineOnlyForAll;

    public override bool CanAttach(INodePresenter nodePresenter)
        => pickComponent ? new PickupEntityComponentCommand().CanAttach(nodePresenter) : new PickupEntityCommand().CanAttach(nodePresenter);

    protected override async Task<PickerResult> ShowPicker(IReadOnlyCollection<INodePresenter> nodePresenters, object? currentValue, object? parameter)
    {
        // The template gives the owner asset (none here) and the type of the property
        var targetType = parameter is Tuple<object?, object?> { Item2: Type type } ? type : nodePresenters.First().Type;
        if (context.PlaySession is null || context.Tree is not { } tree || context.Session.ServiceProvider.TryGet<ILiveEntityPickerService>() is not { } service)
            return new PickerResult { ProcessChange = false };
        var initial = context.Editor?.FindReference(nodePresenters.First())?.EntityHandle ?? 0;
        var picker = new LiveEntityPickerViewModel(context.Session.ServiceProvider, tree, pickComponent ? targetType : null, initial);
        var (token, registration) = context.WatchEnd();
        try
        {
            ++Opened;
            var picked = await service.PickAsync(picker, token);
            return picked is null || token.IsCancellationRequested
                ? new PickerResult { ProcessChange = false }
                : new PickerResult { ProcessChange = true, NewValue = picked };
        }
        finally
        {
            registration.Dispose();
            picker.Destroy();
        }
    }

    protected override void ExecuteSync(INodePresenter nodePresenter, object? parameter, object? preExecuteResult)
    {
        // One edit per entity of the selection, each with its own stand-in
        var result = (PickerResult)preExecuteResult!;
        if (result.ProcessChange && result.NewValue is LiveEntityPick pick && nodePresenter is ILiveNodePresenter presenter)
            context.Editor?.SetReference(presenter, pick.ToReference());
    }
}

/// <summary>
/// An asset dropped on a live reference to content: accepted when it has the reference's type and is in the game's build.
/// </summary>
internal sealed class LiveContentReferenceViewModel(LiveReferenceContext context) : AddReferenceViewModel
{
    public override bool CanAddChildren(IReadOnlyCollection<object> children, AddChildModifiers modifiers, out string message)
    {
        if (children.Count != 1 || children.First() is not AssetViewModel asset)
        {
            message = children.Count > 1 ? "Multiple assets selected" : "The selection is not an asset";
            return false;
        }
        if (!AssetRegistry.CanPropertyHandleAssets(TargetNode.Type, out var types) || !types.Any(x => x.IsAssignableFrom(asset.AssetType)))
        {
            message = "Incompatible asset";
            return false;
        }
        if (context.ReasonFor(asset) is { } reason)
        {
            message = reason;
            return false;
        }
        message = $"Reference {asset.Url} in the running game";
        return true;
    }

    public override void AddChildren(IReadOnlyCollection<object> children, AddChildModifiers modifiers)
    {
        var asset = (AssetViewModel)children.First();
        // Each entity of the selection gets its edit
        TargetNode.NodeValue = context.CreateReference(asset, TargetNode.Type);
    }
}
