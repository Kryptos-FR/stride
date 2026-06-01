// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Avalonia;
using Avalonia.Controls;
using Stride.Assets.Editor.ViewModels;
using Stride.Core.Assets.Editor.Annotations;
using Stride.Core.Assets.Editor.Editors;

namespace Stride.Assets.Editor.Avalonia.Views;

/// <summary>
/// Avalonia view for the script source editor.
/// </summary>
[AssetEditorView<ScriptSourceFileEditorViewModel>]
public partial class ScriptSourceFileEditorView : UserControl, IAssetEditorView
{
    private ScriptSourceFileEditorViewModel? viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptSourceFileEditorView"/> class.
    /// </summary>
    public ScriptSourceFileEditorView()
    {
        InitializeComponent();
        Editor.TextEdited += OnEditorTextEdited;
        Editor.GotFocus += OnEditorGotFocus;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (viewModel is not null)
            viewModel.TextReloaded -= OnTextReloaded;

        viewModel = DataContext as ScriptSourceFileEditorViewModel;

        if (viewModel is not null)
        {
            Editor.LoadText(viewModel.Text);
            viewModel.TextReloaded += OnTextReloaded;
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        viewModel?.CheckExternalChanges();
    }

    private void OnEditorTextEdited(object? sender, string text) => viewModel?.OnTextEdited(text);

    private void OnEditorGotFocus(object? sender, global::Avalonia.Input.FocusChangedEventArgs e) => viewModel?.CheckExternalChanges();

    private void OnTextReloaded(object? sender, string text) => Editor.LoadText(text);
}
