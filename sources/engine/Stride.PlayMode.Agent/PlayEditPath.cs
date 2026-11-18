// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Stride.PlayMode.Protocol;

namespace Stride.PlayMode.Agent;

/// <summary>
/// Reads and changes the value at a path of an edit (<see cref="PlayEdit.Path"/>) in a live object, by reflection: members by
/// their name in the code, items of lists and arrays by position, items of dictionaries by key.
/// </summary>
/// <remarks>
/// <para>
/// A value is always set through the setter of its member (or the indexer of its collection), so that a setter that clamps or
/// refuses does so. A struct on the way (<c>Transform.Position.X</c>) is read, changed, and set back through the member that
/// owns it, up to the first object.
/// </para>
/// <para>
/// The agent references no design-time assembly, so the members are found by reflection, as the editor's type descriptors
/// find them: public or internal, properties and fields, on the type or a base type.
/// </para>
/// </remarks>
internal static class PlayEditPath
{
    private const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly ConcurrentDictionary<(Type Type, string Name), MemberInfo?> Members = new();

    /// <summary>
    /// Reads the value at a path.
    /// </summary>
    /// <exception cref="PlayEditException">A step of the path is not in the object.</exception>
    public static object? Get(object root, IReadOnlyList<PlayPathSegment> path, Func<ReadOnlyMemory<byte>, object?> readKey)
    {
        var current = root;
        for (var i = 0; i < path.Count; i++)
        {
            if (current is null)
                throw new PlayEditException($"{Format(path, i)} is null: {path[i]} cannot be reached.");
            current = GetStep(current, path[i], readKey);
        }
        return current;
    }

    /// <summary>
    /// Changes the container at a path (<paramref name="path"/> without its last step for a collection operation, the whole
    /// path for a set), and sets the structs on the way back through their owners.
    /// </summary>
    /// <param name="root">The component.</param>
    /// <param name="path">The path from the component to the container's step that changes.</param>
    /// <param name="change">Changes the object that holds the last step (the last step's container): it returns that object
    /// changed (a struct is a copy, set back by the caller).</param>
    /// <param name="readKey">Reads a dictionary key.</param>
    public static void Change(object root, IReadOnlyList<PlayPathSegment> path, Func<object, object> change, Func<ReadOnlyMemory<byte>, object?> readKey)
    {
        if (path.Count == 0)
        {
            change(root);
            return;
        }
        ChangeAt(root, path, 0, change, readKey);
    }

    /// <summary>
    /// Sets the value of one step in its container, through the member's setter or the collection's indexer.
    /// </summary>
    /// <returns>The container (a struct is a changed copy).</returns>
    public static object SetStep(object container, PlayPathSegment step, object? value, Func<ReadOnlyMemory<byte>, object?> readKey)
    {
        switch (step.Kind)
        {
            case PlayPathSegmentKind.Member:
                var member = FindMember(container.GetType(), step.Name!);
                switch (member)
                {
                    case PropertyInfo property:
                        if (property.SetMethod is null)
                            throw new PlayEditException($"{step.Name} has no setter: it cannot be changed.");
                        property.SetValue(container, Convert(value, property.PropertyType, step));
                        break;
                    case FieldInfo field:
                        if (field.IsInitOnly)
                            throw new PlayEditException($"{step.Name} is read-only: it cannot be changed.");
                        field.SetValue(container, Convert(value, field.FieldType, step));
                        break;
                }
                return container;
            case PlayPathSegmentKind.Index:
                var list = AsList(container, step);
                CheckIndex(list.Count, step.Index, step);
                list[step.Index] = value;
                return container;
            case PlayPathSegmentKind.Key:
                var dictionary = AsDictionary(container, step);
                var key = ReadKey(step, readKey);
                if (!dictionary.Contains(key))
                    throw new PlayEditException($"The dictionary has no key {step}.");
                dictionary[key] = value;
                return container;
            default:
                throw new PlayEditException($"A path step of kind {(byte)step.Kind} is not known.");
        }
    }

    /// <summary>
    /// The list (or array) at this step's container, as a non-generic list.
    /// </summary>
    public static IList AsList(object container, PlayPathSegment step)
    {
        return container switch
        {
            IList list => list,
            _ => GenericList.TryWrap(container) ?? throw new PlayEditException($"The value that holds {step} is a {container.GetType().Name}, not a list."),
        };
    }

    /// <summary>
    /// The dictionary at this step's container, as a non-generic dictionary.
    /// </summary>
    public static IDictionary AsDictionary(object container, PlayPathSegment step)
    {
        return container switch
        {
            IDictionary dictionary => dictionary,
            _ => GenericDictionary.TryWrap(container) ?? throw new PlayEditException($"The value that holds {step} is a {container.GetType().Name}, not a dictionary."),
        };
    }

    /// <summary>
    /// The element type of a list, or the value type of a dictionary.
    /// </summary>
    public static Type ItemType(object collection)
    {
        foreach (var type in collection.GetType().GetInterfaces().Prepend(collection.GetType()))
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() is var definition && (definition == typeof(IList<>) || definition == typeof(ICollection<>)))
                return type.GetGenericArguments()[0];
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                return type.GetGenericArguments()[1];
        }
        return collection.GetType().IsArray ? collection.GetType().GetElementType()! : typeof(object);
    }

    public static object? ReadKey(PlayPathSegment step, Func<ReadOnlyMemory<byte>, object?> readKey)
    {
        try
        {
            return readKey(step.Key) ?? throw new PlayEditException("A dictionary key is null.");
        }
        catch (PlayEditException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new PlayEditException($"The key {step} cannot be read in the game: {e.Message}");
        }
    }

    public static void CheckIndex(int count, int index, PlayPathSegment step)
    {
        if (index < 0 || index >= count)
            throw new PlayEditException($"The list has {count} items: {step} is not one of them.");
    }

    private static object ChangeAt(object container, IReadOnlyList<PlayPathSegment> path, int index, Func<object, object> change, Func<ReadOnlyMemory<byte>, object?> readKey)
    {
        if (index == path.Count)
            return change(container);
        var step = path[index];
        var child = GetStep(container, step, readKey) ?? throw new PlayEditException($"{Format(path, index + 1)} is null: it cannot be changed.");
        var changed = ChangeAt(child, path, index + 1, change, readKey);
        // A struct was changed as a copy: it goes back through its owner's setter (an object was changed in place)
        if (child.GetType().IsValueType)
            container = SetStep(container, step, changed, readKey);
        return container;
    }

    private static object? GetStep(object container, PlayPathSegment step, Func<ReadOnlyMemory<byte>, object?> readKey)
    {
        switch (step.Kind)
        {
            case PlayPathSegmentKind.Member:
                return FindMember(container.GetType(), step.Name!) switch
                {
                    PropertyInfo property => property.GetValue(container),
                    FieldInfo field => field.GetValue(container),
                    _ => null,
                };
            case PlayPathSegmentKind.Index:
                var list = AsList(container, step);
                CheckIndex(list.Count, step.Index, step);
                return list[step.Index];
            case PlayPathSegmentKind.Key:
                var dictionary = AsDictionary(container, step);
                var key = ReadKey(step, readKey);
                return dictionary.Contains(key!) ? dictionary[key!] : throw new PlayEditException($"The dictionary has no key {step}.");
            default:
                throw new PlayEditException($"A path step of kind {(byte)step.Kind} is not known.");
        }
    }

    private static MemberInfo FindMember(Type type, string name)
    {
        var member = Members.GetOrAdd((type, name), static key =>
        {
            // The most derived member of that name (a property hidden by "new" is the derived one)
            for (var current = key.Type; current is not null; current = current.BaseType)
            {
                var property = current.GetProperty(key.Name, MemberFlags | BindingFlags.DeclaredOnly);
                if (property is not null && property.GetIndexParameters().Length == 0)
                    return property;
                var field = current.GetField(key.Name, MemberFlags | BindingFlags.DeclaredOnly);
                if (field is not null)
                    return field;
            }
            return null;
        });
        return member ?? throw new PlayEditException($"{type.Name} has no member {name} in the game (its code may have changed since the editor loaded it).");
    }

    private static object? Convert(object? value, Type type, PlayPathSegment step)
    {
        if (value is null)
        {
            if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
                throw new PlayEditException($"{step} cannot be null.");
            return null;
        }
        if (type.IsInstanceOfType(value))
            return value;
        // A number of another type, as the editor's numeric boxes can give
        if (value is IConvertible && (type.IsPrimitive || type.IsEnum || Nullable.GetUnderlyingType(type) is { IsPrimitive: true }))
        {
            try
            {
                var target = Nullable.GetUnderlyingType(type) ?? type;
                return target.IsEnum ? Enum.ToObject(target, value) : System.Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException)
            {
                // Reported below
            }
        }
        throw new PlayEditException($"{step} is a {type.Name} in the game, and the editor sent a {value.GetType().Name}.");
    }

    private static string Format(IReadOnlyList<PlayPathSegment> path, int count) => count == 0 ? "The component" : PlayPathSegment.Format(path.Take(count));

    /// <summary>
    /// An <see cref="IList{T}"/> that is not an <see cref="IList"/> (Stride's own collections), seen as one.
    /// </summary>
    private sealed class GenericList(object list, Type itemType) : IList
    {
        private readonly PropertyInfo indexer = typeof(IList<>).MakeGenericType(itemType).GetProperty("Item")!;
        private readonly MethodInfo insert = typeof(IList<>).MakeGenericType(itemType).GetMethod("Insert")!;
        private readonly MethodInfo removeAt = typeof(IList<>).MakeGenericType(itemType).GetMethod("RemoveAt")!;
        private readonly MethodInfo add = typeof(ICollection<>).MakeGenericType(itemType).GetMethod("Add")!;
        private readonly PropertyInfo count = typeof(ICollection<>).MakeGenericType(itemType).GetProperty("Count")!;

        public static IList? TryWrap(object value)
        {
            var type = value.GetType().GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>));
            return type is null ? null : new GenericList(value, type.GetGenericArguments()[0]);
        }

        public object? this[int index] { get => indexer.GetValue(list, [index]); set => indexer.SetValue(list, value, [index]); }

        public bool IsFixedSize => false;

        public bool IsReadOnly => false;

        public int Count => (int)count.GetValue(list)!;

        public bool IsSynchronized => false;

        public object SyncRoot => list;

        public int Add(object? value)
        {
            Invoke(add, value);
            return Count - 1;
        }

        public void Insert(int index, object? value) => Invoke(insert, index, value);

        public void RemoveAt(int index) => Invoke(removeAt, index);

        public void Clear() => throw new NotSupportedException();

        public bool Contains(object? value) => throw new NotSupportedException();

        public int IndexOf(object? value) => throw new NotSupportedException();

        public void Remove(object? value) => throw new NotSupportedException();

        public void CopyTo(Array array, int index) => throw new NotSupportedException();

        public IEnumerator GetEnumerator() => ((IEnumerable)list).GetEnumerator();

        private void Invoke(MethodInfo method, params object?[] arguments)
        {
            try
            {
                method.Invoke(list, arguments);
            }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            {
                throw e.InnerException;
            }
        }
    }

    /// <summary>
    /// An <see cref="IDictionary{TKey, TValue}"/> that is not an <see cref="IDictionary"/>, seen as one.
    /// </summary>
    private sealed class GenericDictionary(object dictionary, Type keyType, Type valueType) : IDictionary
    {
        private readonly Type type = typeof(IDictionary<,>).MakeGenericType(keyType, valueType);

        public static IDictionary? TryWrap(object value)
        {
            var type = value.GetType().GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDictionary<,>));
            return type is null ? null : new GenericDictionary(value, type.GetGenericArguments()[0], type.GetGenericArguments()[1]);
        }

        public object? this[object key] { get => type.GetProperty("Item")!.GetValue(dictionary, [key]); set => type.GetProperty("Item")!.SetValue(dictionary, value, [key]); }

        public bool IsFixedSize => false;

        public bool IsReadOnly => false;

        public ICollection Keys => throw new NotSupportedException();

        public ICollection Values => throw new NotSupportedException();

        public int Count => (int)typeof(ICollection<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType)).GetProperty("Count")!.GetValue(dictionary)!;

        public bool IsSynchronized => false;

        public object SyncRoot => dictionary;

        public void Add(object key, object? value) => Invoke(type.GetMethod("Add", [keyType, valueType])!, key, value);

        public bool Contains(object key) => (bool)type.GetMethod("ContainsKey")!.Invoke(dictionary, [key])!;

        public void Remove(object key) => Invoke(type.GetMethod("Remove", [keyType])!, key);

        public void Clear() => throw new NotSupportedException();

        public void CopyTo(Array array, int index) => throw new NotSupportedException();

        public IDictionaryEnumerator GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)dictionary).GetEnumerator();

        private void Invoke(MethodInfo method, params object?[] arguments)
        {
            try
            {
                method.Invoke(dictionary, arguments);
            }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            {
                throw e.InnerException;
            }
        }
    }
}

/// <summary>
/// An edit the game cannot apply: the message says why, to the user.
/// </summary>
internal sealed class PlayEditException(string message) : Exception(message);
