using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;

namespace ColorRise.Skein.Tests.Compilation;

/// <summary>
/// An assembly as the compiler of a game would make it, with a class "Places" of static methods that carry Skein's
/// attributes. It lets a test have commands and functions that the test assembly itself must not hold.
/// </summary>
internal sealed class GeneratedGame : IDisposable
{
    private readonly AssemblyLoadContext context;

    public GeneratedGame(string name, params (string Method, Type Returns, Type Attribute, string YarnName, (string Name, Type Type)[] Parameters)[] methods)
    {
        var builder = new PersistedAssemblyBuilder(new AssemblyName(name), typeof(object).Assembly);
        var type = builder.DefineDynamicModule(name).DefineType("Places", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        foreach (var (methodName, returns, attribute, yarnName, parameters) in methods)
        {
            var method = type.DefineMethod(methodName, MethodAttributes.Public | MethodAttributes.Static, returns, [.. parameters.Select(p => p.Type)]);
            for (var i = 0; i < parameters.Length; i++)
            {
                method.DefineParameter(i + 1, ParameterAttributes.None, parameters[i].Name);
            }

            method.SetCustomAttribute(new CustomAttributeBuilder(attribute.GetConstructor([typeof(string)])!, [yarnName]));

            // Returns the default of its type
            var il = method.GetILGenerator();
            il.DeclareLocal(returns);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ret);
        }

        type.CreateType();

        using var stream = new MemoryStream();
        builder.Save(stream);
        stream.Position = 0;
        context = new AssemblyLoadContext(name, isCollectible: true);
        Assembly = context.LoadFromStream(stream);
    }

    public Assembly Assembly { get; }

    public void Dispose() => context.Unload();
}
