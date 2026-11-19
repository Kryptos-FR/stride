// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Stride.Core.Assets.Editor.Avalonia.Tests.Helpers;
using Stride.Core.Assets.Editor.Avalonia.Views;
using Stride.Core.Assets.Editor.Quantum.NodePresenters.Keys;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Avalonia.Controls;
using Stride.Core.Presentation.Avalonia.Views;
using Stride.Core.Presentation.Quantum.Presenters;
using Stride.Core.Presentation.Quantum.ViewModels;

namespace Stride.Core.Assets.Editor.Avalonia.Tests;

/// <summary>
/// The property grid follows a node whose object value is replaced, as a paste or a "Reset to base value" does.
/// </summary>
public sealed class PropertyGridRefreshTests
{
    [AvaloniaFact]
    public void TheInlinedValueFollowsAReplacedObject()
    {
        DefaultProviders.Register();
        var context = new NodeViewModelContext(new InlineScalarUpdater());
        var holder = new ScalarHolder();
        var node = context.MemberOf(holder, nameof(ScalarHolder.Scalar));
        var window = new Window { Content = new ContentControl { Content = node, ContentTemplate = PropertyViewHelper.EditorProviders } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("1.5", FindValueText(window));

        // The paste replaces the whole ComputeFloat-like object: the node of its inlined value is a new one
        node.NodeValue = new InlinedScalar { InlinedProperty = 4f };
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("4", FindValueText(window));
        window.Close();
    }

    /// <summary>
    /// Writing an inlined value refreshes the node that inlines it, which builds a new view model for the value: the editor
    /// shown for it is kept, with its popup open, and follows the new view model.
    /// </summary>
    [AvaloniaFact]
    public void AnInlinedColorKeepsItsEditorWhenWritten()
    {
        DefaultProviders.Register();
        var context = new NodeViewModelContext(new InlineColorUpdater());
        var holder = new ColorHolder();
        var node = context.MemberOf(holder, nameof(ColorHolder.Color));
        var window = new Window { Content = new ContentControl { Content = node, ContentTemplate = PropertyViewHelper.EditorProviders } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var editor = Assert.Single(window.GetVisualDescendants().OfType<Color3Editor>());
        var toggle = editor.GetVisualDescendants().OfType<ToggleButton>().Single(x => x.Name == "PickerToggleButton");
        toggle.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        var oldValueNode = editor.DataContext;

        // Several picks: each one builds a new view model for the value, the editor stays
        for (var i = 1; i <= 3; i++)
        {
            var picked = new Color3(0.2f * i, 0.4f, 0.6f);
            editor.Value = picked;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(picked, holder.Color.InlinedProperty);
            Assert.Same(editor, Assert.Single(window.GetVisualDescendants().OfType<Color3Editor>()));
            Assert.True(toggle.IsChecked);
            // The editor shows the value of the new view model
            Assert.NotSame(oldValueNode, editor.DataContext);
            oldValueNode = editor.DataContext;
        }
        ((NodeViewModel)editor.DataContext!).NodeValue = new Color3(1f, 0f, 0f);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new Color3(1f, 0f, 0f), holder.Color.InlinedProperty);
        Assert.Equal(new Color3(1f, 0f, 0f), editor.Value);
        window.Close();
    }

    // The text the user sees in the numeric field of the inlined value
    private static string? FindValueText(Window window)
    {
        var valueBox = Assert.Single(window.GetVisualDescendants().OfType<NumericTextBox>());
        return valueBox.GetVisualDescendants().OfType<TextBox>().First().Text;
    }

    // What InlineMemberNodeUpdater does for a ColorRgbProvider, without an asset: the node depends on its inlined value
    private sealed class InlineColorUpdater : INodePresenterUpdater
    {
        public void UpdateNode(INodePresenter node)
        {
            if (node.Type == typeof(InlinedColor))
            {
                node.AttachedProperties.Set(InlineData.InlineMemberKey, true);
                node.AddDependency(node[nameof(InlinedColor.InlinedProperty)], false);
            }
        }

        public void FinalizeTree(INodePresenter root)
        {
        }
    }

    // What InlineMemberNodeUpdater and NumericValueNodeUpdater do for a ComputeFloat, without an asset
    private sealed class InlineScalarUpdater : INodePresenterUpdater
    {
        public void UpdateNode(INodePresenter node)
        {
            if (node.Type == typeof(InlinedScalar))
                node.AttachedProperties.Set(InlineData.InlineMemberKey, true);
            if (node.Type == typeof(float))
            {
                node.AttachedProperties.Set(NumericData.MinimumKey, float.MinValue);
                node.AttachedProperties.Set(NumericData.MaximumKey, float.MaxValue);
            }
        }

        public void FinalizeTree(INodePresenter root)
        {
        }
    }
}

public sealed class ScalarHolder
{
    public InlinedScalar Scalar { get; set; } = new() { InlinedProperty = 1.5f };
}

public sealed class InlinedScalar
{
    public float InlinedProperty { get; set; }
}

public sealed class ColorHolder
{
    public InlinedColor Color { get; set; } = new() { InlinedProperty = new Color3(1f, 1f, 1f) };
}

public sealed class InlinedColor
{
    public Color3 InlinedProperty { get; set; }
}
