// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Design;

namespace Stride.PlayMode.Tests
{
    /// <summary>
    /// A component an entity can have several of.
    /// </summary>
    [DataContract]
    [AllowMultipleComponents]
    public sealed class RepeatableComponent : EntityComponent
    {
        public int Value { get; set; }
    }

    /// <summary>
    /// A component that needs an <see cref="EditableComponent"/> on its entity.
    /// </summary>
    [DataContract]
    [RequireComponent(typeof(EditableComponent))]
    public sealed class NeedsEditableComponent : EntityComponent
    {
    }

    /// <summary>
    /// A component that cannot be made.
    /// </summary>
    [DataContract]
    public abstract class AbstractTestComponent : EntityComponent
    {
    }
}

// The bodies of the physics engines, as the agent finds them: by the full names of their types and their members. The agent
// references no physics engine, and these tests load none.
namespace Stride.BepuPhysics
{
    [DataContract]
    public class BodyComponent : EntityComponent
    {
        public Vector3 CenterOfMass { get; set; }

        [DataMemberIgnore]
        public List<(Vector3 Position, Quaternion Orientation)> Teleports { get; } = [];

        public void Teleport(Vector3 position, Quaternion orientation) => Teleports.Add((position, orientation));
    }
}

namespace Stride.Engine
{
    [DataContract]
    public class PhysicsComponent : EntityComponent
    {
        public int BoneIndex = -1;

        // In the simulation once set
        [DataMemberIgnore]
        internal Stride.Physics.FakeCollisionObject? NativeCollisionObject;

        [DataMemberIgnore]
        public Matrix PhysicsWorldTransform { get; set; }

        [DataMemberIgnore]
        public int Updates { get; private set; }

        [DataMemberIgnore]
        public bool Activated { get; private set; }

        public void UpdatePhysicsTransformation(bool forceUpdateTransform)
        {
            Updates++;
            PhysicsWorldTransform = Entity.Transform.WorldMatrix;
            if (NativeCollisionObject is { } native)
                native.WorldTransform = PhysicsWorldTransform;
        }

        public void Activate(bool forceActivation) => Activated = true;
    }
}

namespace Stride.Physics
{
    public sealed class FakeCollisionObject
    {
        public Matrix WorldTransform { get; set; }

        public Matrix InterpolationWorldTransform { get; set; }
    }

    [DataContract]
    public sealed class RigidbodyComponent : PhysicsComponent
    {
    }

    [DataContract]
    public sealed class CharacterComponent : PhysicsComponent
    {
        public Quaternion Orientation { get; set; } = Quaternion.Identity;
    }

    [DataContract]
    public sealed class StaticColliderComponent : PhysicsComponent
    {
    }
}
