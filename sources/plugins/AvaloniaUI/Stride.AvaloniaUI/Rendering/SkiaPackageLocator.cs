// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stride.Core.Reflection;

namespace Stride.AvaloniaUI.Rendering;

/// <summary>
/// Finds the Skia package among assemblies, the way a game finds its <c>[GameSystem]</c> classes.
/// </summary>
internal static class SkiaPackageLocator
{
    /// <summary>
    /// Creates the first <see cref="IAvaloniaUISkia"/> in the scan indexes of the assemblies, ordered by full name.
    /// </summary>
    /// <returns><c>null</c> if none of the assemblies has one.</returns>
    public static IAvaloniaUISkia? Find(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies.OrderBy(x => x.FullName, StringComparer.Ordinal))
        {
            foreach (var type in AssemblyRegistry.GetScanTypes(assembly, typeof(IAvaloniaUISkia)).OrderBy(x => x.FullName, StringComparer.Ordinal))
            {
                if (!type.IsAbstract && typeof(IAvaloniaUISkia).IsAssignableFrom(type))
                    return (IAvaloniaUISkia)Activator.CreateInstance(type)!;
            }
        }
        return null;
    }
}
