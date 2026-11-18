// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Globalization;
using Stride.Assets.Presentation;
using Stride.Core.Assets.Editor.Avalonia.Converters;
using Stride.Core.Presentation.Avalonia.Converters;
using Stride.Core.Reflection;
using Stride.Engine;

namespace Stride.GameStudio.Avalonia.Views.Panels;

/// <summary>
/// Converts the component type names of a live entity to its icon in the Live tab: the image of its main component, as the
/// scene editor shows it. The types the editor does not have (the game's scripts when its code is not loaded) are skipped.
/// </summary>
public sealed class LiveEntityIcon : OneWayValueConverter<LiveEntityIcon>
{
    // The type names repeat from one entity to the next
    private static readonly ConcurrentDictionary<string, Type?> types = new();

    /// <inheritdoc />
    public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<string> typeNames)
            return null;

        var componentTypes = typeNames.Select(x => types.GetOrAdd(x, Resolve)).OfType<Type>();
        var componentType = EntityComponentMetadata.GetComponentsByOrder(componentTypes, false).FirstOrDefault();
        return TypeToResource.FetchResourceFromType(componentType, true);
    }

    private static Type? Resolve(string typeName)
    {
        var type = AssemblyRegistry.GetType(typeName, throwOnError: false) ?? Type.GetType(typeName, throwOnError: false);
        return type is not null && typeof(EntityComponent).IsAssignableFrom(type) ? type : null;
    }
}
