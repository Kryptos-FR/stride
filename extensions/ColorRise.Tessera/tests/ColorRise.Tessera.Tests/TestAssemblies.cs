using System.Runtime.CompilerServices;
using ColorRise.Tessera.Assets;
using Stride.Core.Reflection;

namespace ColorRise.Tessera.Tests;

internal static class TestAssemblies
{
    // What the asset compiler and Game Studio do for the extension's assemblies: their types and YAML serializers are known
    [ModuleInitializer]
    internal static void Register()
    {
        AssemblyRegistry.Register(typeof(TileMap).Assembly, AssemblyCommonCategories.Assets);
        AssemblyRegistry.Register(typeof(TileMapAsset).Assembly, AssemblyCommonCategories.Assets);
    }
}
