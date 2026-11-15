using Stride.Core.Assets.Editor.Panels;

namespace ColorRise.Saga.Editor.Tests.Helpers;

/// <summary>
/// A shell without panels that records the panels shown, and whether they were brought forward.
/// </summary>
internal sealed class RecordingPanelService : IPanelService
{
    public List<(string PanelId, bool Activate)> Shown { get; } = [];

    public PanelRegistry Registry { get; } = new();

    public event EventHandler<PanelChangedEventArgs>? PanelChanged { add { } remove { } }

    public bool IsVisible(string panelId) => Shown.Any(x => x.PanelId == panelId);

    public void Show(string panelId, bool activate = true) => Shown.Add((panelId, activate));

    public void Hide(string panelId) { }

    public IPanelViewModel? GetPanel(string panelId) => null;

    public IReadOnlyList<IPanelViewModel> GetInstances(string panelId) => [];

    public Task<IPanelViewModel> OpenInstanceAsync(string panelId) => throw new NotSupportedException();

    public void Activate(IPanelViewModel instance) { }

    public Task<bool> CloseInstanceAsync(IPanelViewModel instance) => Task.FromResult(false);

    public Task ResetLayoutAsync() => Task.CompletedTask;
}
