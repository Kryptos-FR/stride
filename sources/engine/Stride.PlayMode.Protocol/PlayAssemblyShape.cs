// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Stride.PlayMode.Protocol;

/// <summary>
/// A fingerprint of the data an assembly's types serialize: two builds of the same code have the same shape, even when they
/// differ in their method bodies (another platform's conditional code, another configuration), and a member added, removed,
/// renamed or retyped changes it.
/// </summary>
/// <remarks>
/// <para>
/// The editor loads the game's code built for itself, and the game runs a build for its platform: their module version
/// identifiers always differ, while their types read the same snapshots.
/// </para>
/// <para>
/// The shape covers every type of the assembly (but the compiler's own), its base type, and in declaration order the instance
/// fields and properties that a Stride serializer may write: the public ones and those marked <c>DataMember</c>, but those
/// marked <c>DataMemberIgnore</c>, with the arguments of their <c>DataMember</c>. Attributes are matched by name: the protocol
/// references no Stride assembly.
/// </para>
/// </remarks>
public static class PlayAssemblyShape
{
    private const string DataMemberAttribute = "Stride.Core.DataMemberAttribute";
    private const string DataMemberIgnoreAttribute = "Stride.Core.DataMemberIgnoreAttribute";
    private const BindingFlags DeclaredInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Whether an assembly may hold types that the game's snapshots carry: it references Stride's core, and is not Stride's own
    /// (compared by version).
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    public static bool AppliesTo(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var name = assembly.GetName().Name ?? string.Empty;
        if (name.StartsWith("Stride.", StringComparison.Ordinal))
            return false;
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            if (reference.Name == "Stride.Core")
                return true;
        }
        return false;
    }

    /// <summary>
    /// Computes the shape of an assembly.
    /// </summary>
    /// <param name="assembly">The assembly, loaded.</param>
    /// <returns>The shape, never <see cref="Guid.Empty"/>.</returns>
    public static Guid Compute(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            // A type that cannot load is left out on both sides alike
            types = e.Types;
        }

        var text = new StringBuilder();
        foreach (var type in types.OfType<Type>().Where(x => !IsCompilerGenerated(x)).OrderBy(x => x.FullName ?? x.Name, StringComparer.Ordinal))
        {
            text.Append("T ").Append(NameOf(type)).Append(" : ").Append(type.BaseType is { } baseType ? NameOf(baseType) : "-").Append('\n');
            foreach (var field in type.GetFields(DeclaredInstance))
            {
                if (!IsCompilerGenerated(field) && IsSerializable(field, field.IsPublic))
                    AppendMember(text, 'F', field, field.FieldType);
            }
            foreach (var property in type.GetProperties(DeclaredInstance))
            {
                if (property.GetIndexParameters().Length == 0 && IsSerializable(property, property.GetMethod?.IsPublic == true))
                    AppendMember(text, 'P', property, property.PropertyType);
            }
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        var shape = new Guid(hash.AsSpan(0, 16));
        return shape == Guid.Empty ? new Guid(hash.AsSpan(16, 16)) : shape;
    }

    private static bool IsSerializable(MemberInfo member, bool isPublic)
    {
        var dataMember = false;
        foreach (var attribute in member.GetCustomAttributesData())
        {
            switch (attribute.AttributeType.FullName)
            {
                case DataMemberIgnoreAttribute:
                    return false;
                case DataMemberAttribute:
                    dataMember = true;
                    break;
            }
        }
        return isPublic || dataMember;
    }

    private static void AppendMember(StringBuilder text, char kind, MemberInfo member, Type memberType)
    {
        text.Append(kind).Append(' ').Append(member.Name).Append(" : ").Append(NameOf(memberType));
        foreach (var attribute in member.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName != DataMemberAttribute)
                continue;
            text.Append(" [");
            text.AppendJoin(", ", attribute.ConstructorArguments.Select(x => x.ToString()));
            foreach (var named in attribute.NamedArguments)
                text.Append(", ").Append(named.MemberName).Append('=').Append(named.TypedValue.ToString());
            text.Append(']');
        }
        text.Append('\n');
    }

    private static bool IsCompilerGenerated(MemberInfo member)
        => member.Name.Contains('<', StringComparison.Ordinal) || member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    // Assembly-qualified names would carry the versions of the references, which may differ between the two builds
    private static string NameOf(Type type) => type.ToString();
}
