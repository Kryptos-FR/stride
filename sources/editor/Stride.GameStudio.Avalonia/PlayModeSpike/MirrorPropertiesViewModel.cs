// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics;
using Stride.Core.Assets.Editor.Components.Properties;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Commands;
using Stride.Core.Assets.Editor.ViewModels;
using Stride.Core.Assets.Presentation.Quantum.NodePresenters;
using Stride.Core.Assets.Quantum;
using Stride.Core.Presentation.Quantum;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Presentation.Quantum.ViewModels;
using Stride.Core.Quantum;
using Stride.Engine;

namespace Stride.GameStudio.Avalonia.PlayModeSpike;

/// <summary>
/// Play mode spike: the session's property grid on a mirror entity (an entity rebuilt from a snapshot, in no scene and no asset),
/// read-only, updated in place by <see cref="ApplyAsync"/>.
/// </summary>
/// <remarks>
/// It is a <see cref="SessionObjectPropertiesViewModel"/> so that the property grid panel shows it as it shows the others; its
/// node presenter commands and updaters are those of the session's grid (the plugins'), with a presenter factory that stops at
/// the references to objects outside of the mirror and refuses writes from the view.
/// </remarks>
public sealed class MirrorPropertiesViewModel : SessionObjectPropertiesViewModel
{
    private readonly AssetNodeContainer mirrorNodes = new() { NodeBuilder = { NodeFactory = new AssetNodeFactory() } };
    private readonly MirrorApplier applier = new();
    private MirrorPropertyProvider? provider;

    public MirrorPropertiesViewModel(SessionViewModel session)
        : base(session)
    {
        // The same commands and updaters as the session's grid, in the same order (the plugins added theirs to that one)
        var source = (PropertiesViewModel)session.AssetViewProperties;
        foreach (var command in NodePresenterCommands.ToList())
            UnregisterNodePresenterCommand(command);
        foreach (var updater in NodePresenterUpdaters.ToList())
            UnregisterNodePresenterUpdater(updater);
        foreach (var command in source.NodePresenterCommands)
            RegisterNodePresenterCommand(command);
        // Each updater runs behind a guard that records its failures: an updater written for assets can fail on a mirror
        foreach (var updater in source.NodePresenterUpdaters)
            RegisterNodePresenterUpdater(new MirrorGuardedUpdater(updater));
        RegisterNodePresenterUpdater(new MirrorReadOnlyUpdater());

        ViewModelService.NodePresenterFactory = new MirrorNodePresenterFactory(mirrorNodes.NodeBuilder, ViewModelService.AvailableCommands, ViewModelService.AvailableUpdaters);
    }

    /// <summary>
    /// The entity the grid shows.
    /// </summary>
    public Entity? Mirror => provider?.Mirror;

    /// <summary>
    /// The value nodes not to write; see <see cref="MirrorApplier.IsProtected"/>.
    /// </summary>
    public Func<IGraphNode, NodeIndex, bool>? IsProtected { get => applier.IsProtected; set => applier.IsProtected = value; }

    /// <summary>
    /// Whether <see cref="ApplyAsync"/> is rebuilding the grid.
    /// </summary>
    public bool IsRebuilding { get; private set; }

    /// <summary>
    /// Whether the updaters finalize the tree once after the writes of a snapshot instead of after each write (an experiment of
    /// the spike, measured in its report).
    /// </summary>
    public bool BatchFinalizeTree { get; set; }

    /// <summary>
    /// The number of times the grid was built (the first time included).
    /// </summary>
    public int BuildCount { get; private set; }

    /// <summary>
    /// Shows <paramref name="mirror"/> in the grid (a build).
    /// </summary>
    public async Task<MirrorTiming> ShowAsync(Entity mirror)
    {
        ArgumentNullException.ThrowIfNull(mirror);
        var stopwatch = Stopwatch.StartNew();
        IsRebuilding = true;
        try
        {
            provider = new MirrorPropertyProvider(mirror, mirrorNodes);
            TypeDescription = "Live entity (mirror)";
            Name = mirror.Name ?? string.Empty;
            await GenerateSelectionPropertiesAsync([provider]);
            if (ViewModel is null)
            {
                // The grid swallows the error of a build: build again here to report it
                GraphViewModel.Create(ServiceProvider, [provider])?.Destroy();
                throw new InvalidOperationException($"The grid was not built on the mirror: {FallbackMessage}");
            }
            ++BuildCount;
        }
        finally
        {
            IsRebuilding = false;
        }
        return new MirrorTiming(stopwatch.Elapsed, null, Rebuilt: true);
    }

    /// <summary>
    /// Applies a snapshot: writes the changed values in place, or rebuilds the grid on the snapshot's objects when the structure
    /// changed.
    /// </summary>
    public async Task<MirrorTiming> ApplyAsync(MirrorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Dispatcher.EnsureAccess();
        var stopwatch = Stopwatch.StartNew();
        var incoming = MirrorSnapshotSerializer.Restore(snapshot);
        var restored = stopwatch.Elapsed;
        if (provider is null || IsRebuilding)
            return await ShowAsync(incoming) with { Restore = restored };

        MirrorApplyResult result;
        if (BatchFinalizeTree)
        {
            // Each write refreshes its node presenter, which finalizes the whole tree (every updater's FinalizeTree on the root):
            // once for the batch instead
            MirrorGuardedUpdater.DeferFinalizeTree = true;
            try
            {
                result = applier.Apply(provider.GetRootNode(), incoming);
            }
            finally
            {
                MirrorGuardedUpdater.DeferFinalizeTree = false;
            }
            if (result.WrittenValues > 0 && ViewModel is { } viewModel)
            {
                foreach (var root in viewModel.RootNode.NodePresenters)
                {
                    foreach (var updater in ViewModelService.AvailableUpdaters)
                        updater.FinalizeTree(root);
                }
            }
        }
        else
        {
            result = applier.Apply(provider.GetRootNode(), incoming);
        }
        if (!result.StructureChanged)
            return new MirrorTiming(stopwatch.Elapsed, restored, Rebuilt: false, result);

        var rebuild = await ShowAsync(incoming);
        return new MirrorTiming(stopwatch.Elapsed, restored, Rebuilt: true, result, rebuild.Total);
    }

    /// <summary>
    /// The mirror as a property provider: not an asset, every member shown.
    /// </summary>
    private sealed class MirrorPropertyProvider(Entity mirror, NodeContainer nodes) : IPropertyProviderViewModel
    {
        public Entity Mirror { get; } = mirror;

        public bool CanProvidePropertiesViewModel => true;

        public IObjectNode GetRootNode() => nodes.GetOrCreateNode(Mirror)!;

        public bool ShouldConstructMember(IMemberNode member) => true;

        public bool ShouldConstructItem(IObjectNode collection, NodeIndex index) => true;
    }
}

/// <summary>
/// The time an apply or a build took on the UI thread: <see cref="Restore"/> is the deserialization of the snapshot,
/// <see cref="RebuildTotal"/> the build of the grid when the structure changed.
/// </summary>
public sealed record MirrorTiming(TimeSpan Total, TimeSpan? Restore, bool Rebuilt, MirrorApplyResult? Result = null, TimeSpan? RebuildTotal = null);

/// <summary>
/// Play mode spike: the node presenters of a mirror. The presenters of an object outside of the mirror (another entity, its
/// components) have no children, as the asset grid does for an object reference; the presenters refuse writes from the view.
/// </summary>
internal sealed class MirrorNodePresenterFactory(INodeBuilder nodeBuilder, IReadOnlyCollection<INodePresenterCommand> availableCommands, IReadOnlyCollection<INodePresenterUpdater> availableUpdaters)
    : AssetNodePresenterFactory(nodeBuilder, availableCommands, availableUpdaters)
{
    protected override bool ShouldCreateMemberPresenter(INodePresenter parent, IMemberNode member, IPropertyProviderViewModel? propertyProvider)
    {
        if (IsOutsideOfMirror(parent))
            return false;
        return base.ShouldCreateMemberPresenter(parent, member, propertyProvider);
    }

    protected override bool ShouldCreateItemPresenter(INodePresenter parent, IObjectNode collectionNode, NodeIndex index, IPropertyProviderViewModel? propertyProvider)
    {
        if (IsOutsideOfMirror(parent))
            return false;
        return base.ShouldCreateItemPresenter(parent, collectionNode, index, propertyProvider);
    }

    protected override IInitializingNodePresenter CreateRootPresenter(IPropertyProviderViewModel? propertyProvider, IObjectNode rootNode)
    {
        return new MirrorRootNodePresenter(this, propertyProvider, (IAssetObjectNode)rootNode);
    }

    protected override IInitializingNodePresenter CreateMember(IPropertyProviderViewModel? propertyProvider, INodePresenter parentPresenter, IMemberNode member)
    {
        return new MirrorMemberNodePresenter(this, propertyProvider, parentPresenter, (IAssetMemberNode)member);
    }

    protected override IInitializingNodePresenter CreateItem(IPropertyProviderViewModel? propertyProvider, INodePresenter containerPresenter, IObjectNode containerNode, NodeIndex index)
    {
        return new MirrorItemNodePresenter(this, propertyProvider, containerPresenter, (IAssetObjectNode)containerNode, index);
    }

    /// <summary>
    /// Whether the value of <paramref name="presenter"/> is an object outside of the mirror: an entity other than the mirror, or
    /// a component of one.
    /// </summary>
    public static bool IsOutsideOfMirror(INodePresenter presenter)
    {
        var mirror = presenter.Root.Value as Entity;
        return presenter.Value switch
        {
            Entity entity => presenter.Parent is not null && entity != mirror,
            EntityComponent component => component.Entity != mirror,
            _ => false,
        };
    }

    private static void Refused(INodePresenter presenter)
    {
        Debug.WriteLine($"Play mode spike: the mirror is read-only, a write to {presenter.Name} was refused.");
    }

    private sealed class MirrorRootNodePresenter(INodePresenterFactoryInternal factory, IPropertyProviderViewModel? propertyProvider, IAssetObjectNode rootNode)
        : AssetRootNodePresenter(factory, propertyProvider, rootNode)
    {
        public override void UpdateValue(object? newValue) => Refused(this);

        public override void AddItem(object value) => Refused(this);

        public override void AddItem(object value, NodeIndex index) => Refused(this);

        public override void RemoveItem(object value, NodeIndex index) => Refused(this);
    }

    private sealed class MirrorMemberNodePresenter(INodePresenterFactoryInternal factory, IPropertyProviderViewModel? propertyProvider, INodePresenter parent, IAssetMemberNode member)
        : AssetMemberNodePresenter(factory, propertyProvider, parent, member)
    {
        public override void UpdateValue(object? newValue) => Refused(this);

        public override void AddItem(object value) => Refused(this);

        public override void AddItem(object value, NodeIndex index) => Refused(this);

        public override void RemoveItem(object value, NodeIndex index) => Refused(this);
    }

    private sealed class MirrorItemNodePresenter(INodePresenterFactoryInternal factory, IPropertyProviderViewModel? propertyProvider, INodePresenter parent, IAssetObjectNode container, NodeIndex index)
        : AssetItemNodePresenter(factory, propertyProvider, parent, container, index)
    {
        public override void UpdateValue(object? newValue) => Refused(this);

        public override void AddItem(object value) => Refused(this);

        public override void AddItem(object value, NodeIndex index) => Refused(this);

        public override void RemoveItem(object value, NodeIndex index) => Refused(this);
    }
}

/// <summary>
/// Play mode spike: runs an updater of the session's grid on a mirror, and records the exceptions it throws instead of
/// failing the build of the grid (the failures are listed in <see cref="Failures"/>).
/// </summary>
public sealed class MirrorGuardedUpdater(INodePresenterUpdater inner) : INodePresenterUpdater
{
    private static readonly Dictionary<string, string> failures = [];

    /// <summary>
    /// The updaters that failed on a mirror, with the first error of each.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Failures
    {
        get
        {
            lock (failures)
                return new Dictionary<string, string>(failures);
        }
    }

    public INodePresenterUpdater Inner => inner;

    /// <summary>
    /// While set (on the UI thread), <see cref="FinalizeTree"/> does nothing: the caller finalizes the tree once at the end.
    /// </summary>
    [field: ThreadStatic]
    public static bool DeferFinalizeTree { get; set; }

    public void UpdateNode(INodePresenter node)
    {
        try
        {
            inner.UpdateNode(node);
        }
        catch (Exception e)
        {
            Record(node, e);
        }
    }

    public void FinalizeTree(INodePresenter root)
    {
        if (DeferFinalizeTree)
            return;
        try
        {
            inner.FinalizeTree(root);
        }
        catch (Exception e)
        {
            Record(root, e);
        }
    }

    private void Record(INodePresenter node, Exception e)
    {
        lock (failures)
            failures.TryAdd(inner.GetType().Name, $"{e.GetType().Name} on {node.Type.Name} {node.Name}: {e.Message}");
    }
}

/// <summary>
/// Play mode spike: makes every node of a mirror read-only and takes off the commands that change it (add, remove, replace,
/// pick, clear...), so that their buttons are not shown. Copying a value stays.
/// </summary>
internal sealed class MirrorReadOnlyUpdater : NodePresenterUpdaterBase
{
    private static readonly HashSet<string> KeptCommands = [CopyPropertyCommand.CommandName];

    public override void UpdateNode(INodePresenter node)
    {
        node.IsReadOnly = true;
        node.Commands.RemoveAll(x => !KeptCommands.Contains(x.Name));
    }
}
