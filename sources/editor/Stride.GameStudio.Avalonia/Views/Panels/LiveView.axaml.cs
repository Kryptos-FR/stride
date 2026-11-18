// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Input;
using Stride.Editor.PlayMode;

namespace Stride.GameStudio.Avalonia.Views.Panels;

public sealed partial class LiveView : UserControl
{
    public LiveView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Coming back to the tree shows its selection in the property grid again, after another selection took it.
    /// </summary>
    private void OnTreeGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (DataContext is LivePanelViewModel { Selection: not null } viewModel)
            viewModel.ShowSelectionCommand.Execute();
    }
}
