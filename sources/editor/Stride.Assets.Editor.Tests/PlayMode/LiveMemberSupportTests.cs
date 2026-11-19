// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Editor.PlayMode;
using Stride.Engine;
using Stride.Physics;

namespace Stride.Assets.Editor.Tests.PlayMode;

/// <summary>
/// Tests <see cref="LiveMemberSupport"/>, the members the live grid shows read-only.
/// </summary>
public sealed class LiveMemberSupportTests
{
    [Fact]
    public void TheMembersBulletReadsWhenTheComponentJoinsAreReadOnly()
    {
        Assert.NotNull(LiveMemberSupport.GetReadOnlyReason(typeof(StaticColliderComponent), nameof(PhysicsComponent.ColliderShapes)));
        Assert.NotNull(LiveMemberSupport.GetReadOnlyReason(typeof(RigidbodyComponent), nameof(PhysicsComponent.CollisionGroup)));
        Assert.Null(LiveMemberSupport.GetReadOnlyReason(typeof(RigidbodyComponent), nameof(RigidbodyComponent.Mass)));
    }

    [Fact]
    public void TheTransformOfAnEntityThatABodyMovesCanChangeTheGameMovesTheBody()
    {
        var character = new Entity("Player") { new CharacterComponent() };
        var dynamicBody = new Entity("Crate") { new RigidbodyComponent { ColliderShapes = { new BoxColliderShapeDesc() } } };

        Assert.Null(Reason(character, nameof(TransformComponent.Position)));
        Assert.Null(Reason(character, nameof(TransformComponent.Rotation)));
        Assert.Null(Reason(dynamicBody, nameof(TransformComponent.Position)));
        Assert.Null(Reason(dynamicBody, nameof(TransformComponent.Scale)));
    }

    [Fact]
    public void TheScaleOfABulletBodyWithAColliderShapeAssetIsReadOnly()
    {
        var fromAsset = new Entity("Rock") { new StaticColliderComponent { ColliderShapes = { new ColliderShapeAssetDesc() } } };
        var plain = new Entity("Lamp");

        Assert.Contains("collider shape asset", Reason(fromAsset, nameof(TransformComponent.Scale)));
        Assert.Null(Reason(fromAsset, nameof(TransformComponent.Position)));
        Assert.Null(Reason(plain, nameof(TransformComponent.Scale)));
        // Without the object, only the members that are always read-only
        Assert.Null(LiveMemberSupport.GetReadOnlyReason(typeof(TransformComponent), nameof(TransformComponent.Scale)));
    }

    [Fact]
    public void APluginAddsItsOwnMembers()
    {
        LiveMemberSupport.AddReadOnly(typeof(LiveSupportTestComponent).FullName!, nameof(LiveSupportTestComponent.Seed), "Read in Start.");
        LiveMemberSupport.AddReadOnly(typeof(LiveSupportTestComponent).FullName!, nameof(LiveSupportTestComponent.Size), x => ((LiveSupportTestComponent)x).Locked ? "Locked." : null);

        Assert.Equal("Read in Start.", LiveMemberSupport.GetReadOnlyReason(typeof(LiveSupportTestComponent), nameof(LiveSupportTestComponent.Seed)));
        Assert.Equal("Locked.", LiveMemberSupport.GetReadOnlyReason(typeof(LiveSupportTestComponent), nameof(LiveSupportTestComponent.Size), new LiveSupportTestComponent { Locked = true }));
        Assert.Null(LiveMemberSupport.GetReadOnlyReason(typeof(LiveSupportTestComponent), nameof(LiveSupportTestComponent.Size), new LiveSupportTestComponent()));
    }

    private static string? Reason(Entity entity, string member) => LiveMemberSupport.GetReadOnlyReason(typeof(TransformComponent), member, entity.Transform);
}

/// <summary>
/// A component of a plugin.
/// </summary>
public sealed class LiveSupportTestComponent : EntityComponent
{
    public int Seed { get; set; }

    public float Size { get; set; }

    public bool Locked { get; set; }
}
