// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Concurrent;
using System.Reflection;
using Stride.Core.Mathematics;
using Stride.Engine;

namespace Stride.PlayMode.Agent;

/// <summary>
/// Moves the physics bodies of an entity (and of its descendants) to where its transform now is, through each engine's own
/// call, so that an edit of the transform, or a move to another parent, is not written back by the simulation at its next
/// step.
/// </summary>
/// <remarks>
/// <para>
/// The agent references no physics engine: a game may have Bullet, Bepu, both or none. The bodies are found by the full name of
/// their type, and their members by reflection, once per type.
/// </para>
/// <list type="bullet">
/// <item>Bullet (<c>Stride.Engine.PhysicsComponent</c> and its kinds): <c>UpdatePhysicsTransformation</c>, the engine's own call
/// that sets the body's world transform from the entity's (with the collider's offset), and scales its shapes with the entity
/// (except shapes of a collider asset). A rigid body also gets the same
/// transform as the start of its interpolation, which Bullet writes back to the entity at each step, even a step of no time
/// (a paused game), and is woken up. A character's transform is its body's turned by its <c>Orientation</c>: the body gets the
/// inverse turn, so that the entity ends where it was put. Velocities are kept, as Bullet's own <c>Teleport</c> does. A body
/// that follows a bone of a model is left alone: the animation moves it.</item>
/// <item>Bepu (<c>Stride.BepuPhysics.BodyComponent</c> and its characters): <c>Teleport(position, orientation)</c>, with the
/// body's center of mass added as Bepu's own attach does. Velocities are kept. Bepu's statics follow their transform on their
/// own.</item>
/// </list>
/// </remarks>
internal static class PlayPhysicsTeleport
{
    private const string BulletPhysicsComponent = "Stride.Engine.PhysicsComponent";
    private const string BulletCharacter = "Stride.Physics.CharacterComponent";
    private const string BulletRigidbody = "Stride.Physics.RigidbodyComponent";
    private const string BepuBody = "Stride.BepuPhysics.BodyComponent";

    private static readonly ConcurrentDictionary<Type, Teleporter?> Teleporters = new();

    /// <summary>
    /// Whether an edit of this member of a transform moves the entity: its position, its rotation, its scale (Bullet scales the
    /// shapes of a body with it), or its matrices.
    /// </summary>
    public static bool MovesEntity(string? member)
        => member is nameof(TransformComponent.Position) or nameof(TransformComponent.Rotation) or nameof(TransformComponent.Scale)
            or nameof(TransformComponent.LocalMatrix) or nameof(TransformComponent.WorldMatrix);

    /// <summary>
    /// Moves the bodies of the entity and its descendants to their transforms.
    /// </summary>
    /// <returns>The bodies moved.</returns>
    /// <exception cref="PlayEditException">A body's engine refused the move (its error).</exception>
    public static int Teleport(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var moved = 0;
        // The world matrices of the parents are up to date before the children's
        entity.Transform.UpdateWorldMatrix();
        Visit(entity, ref moved);
        return moved;
    }

    /// <summary>
    /// The engine and kind of a body, for the report of what was used: "Bullet character", "Bullet rigid body", "Bepu body";
    /// <c>null</c> for a component that is no body.
    /// </summary>
    public static string? DescribeBody(EntityComponent component) => Find(component.GetType())?.Kind;

    private static void Visit(Entity entity, ref int moved)
    {
        foreach (var component in entity.Components)
        {
            if (Find(component.GetType()) is not { } teleporter)
                continue;
            try
            {
                if (teleporter.Move(component))
                    moved++;
            }
            catch (Exception e)
            {
                var root = e is TargetInvocationException { InnerException: { } inner } ? inner : e;
                throw new PlayEditException($"The {teleporter.Kind} of {entity.Name} could not be moved: {root.Message}");
            }
        }
        foreach (var child in entity.Transform.Children)
        {
            if (child.Entity is { } childEntity)
            {
                childEntity.Transform.UpdateWorldMatrix();
                Visit(childEntity, ref moved);
            }
        }
    }

    private static Teleporter? Find(Type type) => Teleporters.GetOrAdd(type, static x =>
    {
        for (var current = x; current is not null; current = current.BaseType)
        {
            switch (current.FullName)
            {
                case BepuBody:
                    return BepuTeleporter.Create(x);
                case BulletPhysicsComponent:
                    return BulletTeleporter.Create(x);
            }
        }
        return null;
    });

    private abstract class Teleporter(string kind)
    {
        public string Kind { get; } = kind;

        /// <returns>Whether the body was moved (one that follows a bone, or is not in a simulation, is not).</returns>
        public abstract bool Move(EntityComponent component);
    }

    private sealed class BulletTeleporter : Teleporter
    {
        private readonly MethodInfo update;
        private readonly PropertyInfo? orientation;
        private readonly PropertyInfo? physicsWorldTransform;
        private readonly FieldInfo? boneIndex;
        private readonly FieldInfo? nativeObject;
        private readonly MethodInfo? activate;
        private readonly bool isCharacter;
        private readonly bool isRigidbody;

        private BulletTeleporter(Type type, string kind, MethodInfo update)
            : base(kind)
        {
            this.update = update;
            isCharacter = Is(type, BulletCharacter);
            isRigidbody = Is(type, BulletRigidbody);
            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            orientation = isCharacter ? type.GetProperty("Orientation", Instance) : null;
            physicsWorldTransform = type.GetProperty("PhysicsWorldTransform", Instance);
            boneIndex = type.GetField("BoneIndex", Instance);
            // The body's start of interpolation is not in Stride's API: it is Bullet's own, on its collision object
            nativeObject = type.GetField("NativeCollisionObject", Instance);
            activate = type.GetMethod("Activate", Instance, [typeof(bool)]);
        }

        public static Teleporter? Create(Type type)
        {
            var update = type.GetMethod("UpdatePhysicsTransformation", BindingFlags.Instance | BindingFlags.Public, [typeof(bool)]);
            if (update is null)
                return null;
            var kind = Is(type, BulletCharacter) ? "Bullet character" : Is(type, BulletRigidbody) ? "Bullet rigid body" : "Bullet collider";
            return new BulletTeleporter(type, kind, update);
        }

        public override bool Move(EntityComponent component)
        {
            if (boneIndex?.GetValue(component) is int bone && bone >= 0)
                return false;
            var native = nativeObject?.GetValue(component);
            if (native is null)
                return false;
            update.Invoke(component, [true]);
            if (isCharacter && orientation?.GetValue(component) is Quaternion turn && physicsWorldTransform is not null && turn != Quaternion.Identity)
            {
                // The entity is the body turned by Orientation: the body gets the inverse turn
                var body = (Matrix)physicsWorldTransform.GetValue(component)!;
                physicsWorldTransform.SetValue(component, Matrix.RotationQuaternion(Quaternion.Invert(turn)) * body);
            }
            if (isRigidbody)
            {
                // Bullet writes the interpolated transform back to the entity at each step, from this start
                var nativeType = native.GetType();
                var world = nativeType.GetProperty("WorldTransform");
                var interpolation = nativeType.GetProperty("InterpolationWorldTransform");
                if (world is not null && interpolation is { CanWrite: true })
                    interpolation.SetValue(native, world.GetValue(native));
                activate?.Invoke(component, [true]);
            }
            return true;
        }
    }

    private sealed class BepuTeleporter(MethodInfo teleport, PropertyInfo? centerOfMass) : Teleporter("Bepu body")
    {
        public static Teleporter? Create(Type type)
        {
            var teleport = type.GetMethod("Teleport", BindingFlags.Instance | BindingFlags.Public, [typeof(Vector3), typeof(Quaternion)]);
            return teleport is null ? null : new BepuTeleporter(teleport, type.GetProperty("CenterOfMass", BindingFlags.Instance | BindingFlags.Public));
        }

        public override bool Move(EntityComponent component)
        {
            var transform = component.Entity.Transform;
            transform.WorldMatrix.Decompose(out _, out Quaternion rotation, out Vector3 translation);
            var center = centerOfMass?.GetValue(component) is Vector3 offset ? offset : Vector3.Zero;
            // As Bepu's attach: the body's position is the entity's, plus its center of mass turned with it
            teleport.Invoke(component, [translation + Vector3.Transform(center, rotation), rotation]);
            return true;
        }
    }

    private static bool Is(Type type, string fullName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.FullName == fullName)
                return true;
        }
        return false;
    }
}
