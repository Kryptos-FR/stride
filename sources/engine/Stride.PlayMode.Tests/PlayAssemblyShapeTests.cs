// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using System.Reflection.Emit;
using Stride.Core;
using Stride.Engine;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Tests;

/// <summary>
/// Tests <see cref="PlayAssemblyShape"/>: two builds of the same types have the same shape, whatever their method bodies; a
/// member that a serializer writes changes it.
/// </summary>
public sealed class PlayAssemblyShapeTests
{
    [Fact]
    public void TheSameTypesWithOtherMethodBodiesHaveTheSameShape()
    {
        // What another platform's conditional code or another configuration does to the game's code
        var editorBuild = Build(x => x.Field("Speed", typeof(float)), returned: 1);
        var gameBuild = Build(x => x.Field("Speed", typeof(float)), returned: 2);

        Assert.NotEqual(Guid.Empty, PlayAssemblyShape.Compute(editorBuild));
        Assert.Equal(PlayAssemblyShape.Compute(editorBuild), PlayAssemblyShape.Compute(gameBuild));
    }

    [Fact]
    public void AMemberAddedRenamedOrRetypedChangesTheShape()
    {
        var shape = PlayAssemblyShape.Compute(Build(x => x.Field("Speed", typeof(float))));

        Assert.NotEqual(shape, PlayAssemblyShape.Compute(Build(x => x.Field("Speed", typeof(float)).Field("Jump", typeof(float)))));
        Assert.NotEqual(shape, PlayAssemblyShape.Compute(Build(x => x.Field("Pace", typeof(float)))));
        Assert.NotEqual(shape, PlayAssemblyShape.Compute(Build(x => x.Field("Speed", typeof(double)))));
        Assert.NotEqual(shape, PlayAssemblyShape.Compute(Build(x => x.Field("Speed", typeof(float)).Property("Name", typeof(string)))));
    }

    [Fact]
    public void OnlyTheMembersASerializerWritesCount()
    {
        var shape = PlayAssemblyShape.Compute(Build(x => x.Field("Speed", typeof(float))));

        // A private field, or a public one marked DataMemberIgnore, is not written
        Assert.Equal(shape, PlayAssemblyShape.Compute(Build(x => x.Field("Speed", typeof(float)).Field("cache", typeof(int), FieldAttributes.Private))));
        Assert.Equal(shape, PlayAssemblyShape.Compute(Build(x => x.Field("Speed", typeof(float)).Field("Debug", typeof(int), ignored: true))));
        // A private field marked DataMember is, and so is the order of a DataMember
        Assert.NotEqual(shape, PlayAssemblyShape.Compute(Build(x => x.Field("Speed", typeof(float)).Field("cache", typeof(int), FieldAttributes.Private, dataMember: 10))));
        Assert.NotEqual(shape, PlayAssemblyShape.Compute(Build(x => x.Field("Speed", typeof(float), dataMember: 10))));
    }

    [Fact]
    public void StrideAndAssembliesThatDoNotUseItAreLeftOut()
    {
        Assert.False(PlayAssemblyShape.AppliesTo(typeof(Entity).Assembly));
        Assert.False(PlayAssemblyShape.AppliesTo(typeof(FactAttribute).Assembly));
    }

    /// <summary>
    /// Emits an assembly with one component type, <c>MyGame.Player</c>, whose method <c>Value</c> returns <paramref name="returned"/>.
    /// </summary>
    private static Assembly Build(Action<Members> members, int returned = 0)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("MyGame.Game"), AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("MyGame.Game");
        var type = module.DefineType("MyGame.Player", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, typeof(EntityComponent));
        members(new Members(type));
        var method = type.DefineMethod("Value", MethodAttributes.Public, typeof(int), Type.EmptyTypes);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldc_I4, returned);
        il.Emit(OpCodes.Ret);
        type.CreateType();
        return assembly;
    }

    private sealed class Members(TypeBuilder type)
    {
        public Members Field(string name, Type fieldType, FieldAttributes attributes = FieldAttributes.Public, int? dataMember = null, bool ignored = false)
        {
            var field = type.DefineField(name, fieldType, attributes);
            if (dataMember is { } order)
                field.SetCustomAttribute(new CustomAttributeBuilder(typeof(DataMemberAttribute).GetConstructor([typeof(int)])!, [order]));
            if (ignored)
                field.SetCustomAttribute(new CustomAttributeBuilder(typeof(DataMemberIgnoreAttribute).GetConstructor(Type.EmptyTypes)!, []));
            return this;
        }

        public Members Property(string name, Type propertyType)
        {
            var field = type.DefineField("_" + name, propertyType, FieldAttributes.Private);
            var property = type.DefineProperty(name, PropertyAttributes.None, propertyType, null);
            var getter = type.DefineMethod("get_" + name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
            var il = getter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);
            return this;
        }
    }
}
