using System.Runtime.CompilerServices;
using ColorRise.Skein.Assets;
using Stride.Core.Reflection;

namespace ColorRise.Skein.Editor.Tests;

internal static class TestAssemblies
{
    /// <summary>
    /// The session of a test loads its package without an MSBuild evaluation, so nothing registers the assemblies of Skein's
    /// assets for it: the tests do, before any of them runs.
    /// </summary>
#pragma warning disable CA2255 // A module initializer in a test assembly
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register()
    {
        AssemblyRegistry.Register(typeof(SkeinEditorPlugin).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(YarnScriptAsset).Assembly, AssemblyCommonCategories.Assets);
    }
}
