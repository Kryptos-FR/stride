// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
using Stride.Core.Presentation.Avalonia.Views;
using Stride.Core.Presentation.Quantum.ViewModels;

namespace Stride.Core.Assets.Editor.Avalonia.Views;

/// <summary>
/// Selects the template of a property of the property grid.
/// </summary>
/// <remarks>
/// A refresh of a node builds new view models for its children. A control built for a property is kept for the new view
/// model when it would get the same template, so that what the user has open in it (the popup of a color) stays open.
/// </remarks>
public sealed class PropertyTemplateProviderSelector : TemplateProviderSelector, IRecyclingDataTemplate
{
    private readonly ConditionalWeakTable<NodeViewModel, List<string>> usedProviders = new();
    private readonly ConditionalWeakTable<NodeViewModel, WeakReference> lastControls = new();
    // The provider each control was built from, and the providers used before it for the same node
    private readonly ConditionalWeakTable<Control, BuiltTemplate> builtTemplates = new();

    public override Control? Build(object? item)
    {
        return Build(item, null);
    }

    public Control? Build(object? data, Control? existing)
    {
        if (data is NodeViewModel node)
        {
            var usedProvidersForItem = usedProviders.GetOrCreateValue(node);

            // The last control built for this node is still in the window: the request comes from a template of a provider that
            // falls back on the next one inside it, so the providers
            // already used are kept. In any other case, we clear the list of used providers.
            if (!lastControls.TryGetValue(node, out var lastControl) || !lastControl.IsAlive)
            {
                usedProvidersForItem.Clear();
            }
            lastControls.Remove(node);

            var availableSelectors = templateProviders.Where(x => x.Match(node)).ToList();
            var provider = availableSelectors.FirstOrDefault(x => !usedProvidersForItem.Contains(x.Name));
            if (provider != null)
            {
                var previousProviders = usedProvidersForItem.ToList();
                usedProvidersForItem.Add(provider.Name);
                BuiltTemplate? built = null;
                var isSameTemplate = existing is not null
                    && builtTemplates.TryGetValue(existing, out built)
                    && built.Provider == provider.Name
                    && built.PreviousProviders.SequenceEqual(previousProviders);
                var newControl = isSameTemplate ? existing : provider.Template.Build(node);
                if (newControl is not null)
                {
                    if (!isSameTemplate || built is null)
                    {
                        built = new BuiltTemplate(provider.Name, previousProviders);
                        builtTemplates.AddOrUpdate(newControl, built);
                    }
                    built.Node = node;
                    lastControls.Add(node, new WeakReference(newControl));
                    // One handler per control, for the node it shows now
                    newControl.DetachedFromLogicalTree -= OnDetached;
                    newControl.DetachedFromLogicalTree += OnDetached;
                }
                return newControl;
            }

            return null;
        }

        return base.Build(data);
    }

    // A control that leaves the window is no longer the last one: when the property moves (its panel is moved, or the layout
    // is reset), its presenters build their templates again, from the first provider
    private void OnDetached(object? sender, LogicalTreeAttachmentEventArgs e)
    {
        if (sender is not Control control)
            return;

        control.DetachedFromLogicalTree -= OnDetached;
        if (builtTemplates.TryGetValue(control, out var built) && built.Node is { } node
            && lastControls.TryGetValue(node, out var last) && last.Target == control)
        {
            lastControls.Remove(node);
        }
    }

    private sealed class BuiltTemplate(string provider, List<string> previousProviders)
    {
        public string Provider { get; } = provider;

        public List<string> PreviousProviders { get; } = previousProviders;

        /// <summary>
        /// The node the control shows now: a recycled control shows the node it was given last.
        /// </summary>
        public NodeViewModel? Node { get; set; }
    }
}
