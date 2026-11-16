// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Reflection;

namespace Stride.AvaloniaUI.Skia;

internal static class Module
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // The core finds the Skia package among the registered assemblies
        AssemblyRegistry.Register(typeof(Module).Assembly, AssemblyCommonCategories.Engine);
    }
}
