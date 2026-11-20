// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine;

namespace Stride.AvaloniaUI;

/// <summary>
/// Creates the Avalonia view of each <see cref="AvaloniaUIComponent"/> in a running scene and hands it to the
/// <see cref="AvaloniaUISystem"/>, which draws it and gives it the input.
/// </summary>
internal sealed class AvaloniaUIProcessor : EntityProcessor<AvaloniaUIComponent, AvaloniaUIView>
{
    private AvaloniaUISystem? system;

    protected override void OnSystemAdd()
    {
        system = Services.GetService<AvaloniaUISystem>();
    }

    protected override void OnSystemRemove()
    {
        system = null;
    }

    protected override AvaloniaUIView GenerateComponentData(Entity entity, AvaloniaUIComponent component)
    {
        AvaloniaUISystem.EnsureAvalonia();
        return new AvaloniaUIView(component);
    }

    protected override bool IsAssociatedDataValid(Entity entity, AvaloniaUIComponent component, AvaloniaUIView associatedData)
        => ReferenceEquals(associatedData.Component, component);

    protected override void OnEntityComponentAdding(Entity entity, AvaloniaUIComponent component, AvaloniaUIView data)
    {
        system?.AddView(data);
    }

    protected override void OnEntityComponentRemoved(Entity entity, AvaloniaUIComponent component, AvaloniaUIView data)
    {
        system?.RemoveView(data);
        data.Dispose();
    }
}
