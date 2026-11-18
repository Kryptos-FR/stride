// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Stride.Engine;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// The members of the game's types that the live grid shows read-only, with the reason it gives: the engine reads them once
/// (when the component joins the scene), or another part of the game writes them back each frame, so a value set while the
/// game runs would show in the grid and change nothing.
/// </summary>
/// <remarks>
/// <para>
/// Types are named by their full name, so that the editor does not need to load an engine plugin to know its members. A
/// member is found on the type that declares it and on the types that derive from it. Plugins add their own members with
/// <see cref="AddReadOnly(string, string, string)"/>, or with a rule that looks at the object (and its entity) with
/// <see cref="AddReadOnly(string, string, Func{object, string?})"/>.
/// </para>
/// <para>
/// Built in: the collider shapes and the collision filters of a Bullet physics component, read when it joins the
/// simulation; the position and the rotation of an entity that a dynamic physics body moves (a Bullet character or a
/// rigid body that is not kinematic, a Bepu body that is not kinematic), which the simulation writes back each frame, even
/// while play mode pauses the game.
/// </para>
/// </remarks>
[Experimental("STREXP012")]
public static class LiveMemberSupport
{
    private const string AttachOnly = "The engine reads this value when the component joins the scene: changing it while the game runs does nothing.";

    private static readonly ConcurrentDictionary<(string Type, string Member), string> ReadOnlyMembers = new()
    {
        // Bullet physics: the shapes are composed, and the collision filters given to the simulation, when the component is
        // added to it
        [("Stride.Engine.PhysicsComponent", "ColliderShapes")] = AttachOnly,
        [("Stride.Engine.PhysicsComponent", "CollisionGroup")] = AttachOnly,
        [("Stride.Engine.PhysicsComponent", "CanCollideWith")] = AttachOnly,
    };

    private static readonly ConcurrentDictionary<(string Type, string Member), Func<object, string?>> ReadOnlyRules = new()
    {
        [(typeof(TransformComponent).FullName!, nameof(TransformComponent.Position))] = MovedByPhysics,
        [(typeof(TransformComponent).FullName!, nameof(TransformComponent.Rotation))] = MovedByPhysics,
    };

    /// <summary>
    /// Shows a member read-only in the live grid.
    /// </summary>
    /// <param name="declaringTypeFullName">The full name of the type that declares the member.</param>
    /// <param name="memberName">The name of the member, as the type declares it.</param>
    /// <param name="reason">What the grid says on the member.</param>
    public static void AddReadOnly(string declaringTypeFullName, string memberName, string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(declaringTypeFullName);
        ArgumentException.ThrowIfNullOrEmpty(memberName);
        ArgumentException.ThrowIfNullOrEmpty(reason);
        ReadOnlyMembers[(declaringTypeFullName, memberName)] = reason;
    }

    /// <summary>
    /// Shows a member read-only in the live grid when a rule says so.
    /// </summary>
    /// <param name="declaringTypeFullName">The full name of the type that declares the member.</param>
    /// <param name="memberName">The name of the member, as the type declares it.</param>
    /// <param name="reasonFor">Gives what the grid says on the member of an object (the live grid's copy of the game's
    /// object, a component of a copy of its entity), or <c>null</c> when the member can change.</param>
    public static void AddReadOnly(string declaringTypeFullName, string memberName, Func<object, string?> reasonFor)
    {
        ArgumentException.ThrowIfNullOrEmpty(declaringTypeFullName);
        ArgumentException.ThrowIfNullOrEmpty(memberName);
        ArgumentNullException.ThrowIfNull(reasonFor);
        ReadOnlyRules[(declaringTypeFullName, memberName)] = reasonFor;
    }

    /// <summary>
    /// Why a member of an object of the given type is read-only in the live grid, or <c>null</c> when it can change.
    /// </summary>
    /// <param name="type">The type of the object that has the member.</param>
    /// <param name="memberName">The name of the member.</param>
    /// <param name="owner">The object that has the member, for the rules that look at it; <c>null</c> checks only the members
    /// that are always read-only.</param>
    public static string? GetReadOnlyReason(Type type, string memberName, object? owner = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.FullName is not { } name)
                continue;
            if (ReadOnlyMembers.TryGetValue((name, memberName), out var reason))
                return reason;
            if (owner is not null && ReadOnlyRules.TryGetValue((name, memberName), out var rule) && rule(owner) is { } ruled)
                return ruled;
        }
        return null;
    }

    private static string? MovedByPhysics(object owner)
    {
        if (owner is not TransformComponent { Entity: { } entity })
            return null;
        foreach (var component in entity.Components)
        {
            if (IsDynamicBody(component))
                return "A physics body moves this entity: the simulation writes its position and rotation back each frame, even while the game is paused. Moving a body while the game runs is not supported yet.";
        }
        return null;
    }

    // A Bullet character, a Bullet rigid body or a Bepu body that is not kinematic (a kinematic body follows the transform)
    private static bool IsDynamicBody(EntityComponent component)
    {
        for (var type = component.GetType(); type is not null; type = type.BaseType)
        {
            switch (type.FullName)
            {
                case "Stride.Physics.CharacterComponent":
                    return true;
                case "Stride.Physics.RigidbodyComponent":
                    return !IsTrue(component, "IsKinematic");
                case "Stride.BepuPhysics.BodyComponent":
                    return !IsTrue(component, "Kinematic");
            }
        }
        return false;
    }

    private static bool IsTrue(object owner, string property)
    {
        try
        {
            return owner.GetType().GetProperty(property)?.GetValue(owner) is true;
        }
        catch (Exception)
        {
            // A getter that needs the simulation: say the body moves the entity
            return false;
        }
    }
}
