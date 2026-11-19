// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Editor.PlayMode;
using Stride.PlayMode.Protocol;

namespace Stride.Editor.Tests.PlayMode;

/// <summary>
/// Tests <see cref="PlayEditRecord"/>: what the user changed in the running game, which Apply to asset tells from what the
/// game changed itself.
/// </summary>
public sealed class PlayEditRecordTests
{
    private const int Player = 1;
    private const int Other = 2;
    private static readonly Guid Script = Guid.NewGuid();
    private static readonly Guid Transform = Guid.NewGuid();

    [Fact]
    public void ASetMarksItsMemberWhatIsInsideItAndWhatHoldsIt()
    {
        var record = new PlayEditRecord();
        record.Add(Set(Player, Transform, PlayPathSegment.Member("Position")));

        Assert.True(record.IsEdited(Player, Transform, "Position"));
        Assert.True(record.IsEdited(Player, Transform, "Position.X"));
        // The component itself holds it: its header lists it as edited live
        Assert.True(record.IsEdited(Player, Transform, string.Empty));
        Assert.False(record.IsEdited(Player, Transform, "Rotation"));
        Assert.False(record.IsEdited(Player, Transform, "PositionOffset"));
        Assert.False(record.IsEdited(Player, Script, "Position"));
        Assert.False(record.IsEdited(Other, Transform, "Position"));
        Assert.True(record.HasEdits(Player));
        Assert.False(record.HasEdits(Other));
        Assert.Equal(1, record.Count);
    }

    [Fact]
    public void AnItemAddedRemovedOrMovedKeepsThePathOfItsCollection()
    {
        var record = new PlayEditRecord();
        record.Add(Edit(Player, Script, PlayEditOperation.Insert, PlayPathSegment.Member("Values"), PlayPathSegment.Item(1)));
        record.Add(Edit(Other, Script, PlayEditOperation.Add, PlayPathSegment.Member("Values")));

        var edit = Assert.Single(record.GetEdits(Player));
        Assert.Equal("Values", edit.Path);
        Assert.True(edit.IsCollectionChange);
        Assert.Equal("Values", Assert.Single(record.GetEdits(Other)).Path);
        Assert.True(record.HasCollectionChange(Player, Script, "Values"));
        Assert.True(record.HasCollectionChange(Other, Script, "Values"));
        // An item's value set does not move the items
        record.Add(Set(Player, Script, PlayPathSegment.Member("Table"), PlayPathSegment.Item(0)));
        Assert.False(record.HasCollectionChange(Player, Script, "Table"));
        // A new list set in place of the old one does
        record.Add(Set(Player, Script, PlayPathSegment.Member("Items")));
        Assert.True(record.HasCollectionChange(Player, Script, "Items"));
    }

    [Fact]
    public void AComponentAddedMarksAllItsMembersAndOneRemovedIsKept()
    {
        var record = new PlayEditRecord();
        var added = Guid.NewGuid();
        var changed = 0;
        record.Changed += (_, _) => ++changed;

        record.Add(new PlayStructureEdit(1, PlayStructureOperation.AddComponent, Player, TypeName: "Test.Light, Test"), new PlayStructureResult(1, 10, PlayEditStatus.Applied, [], added, null));
        record.Add(new PlayStructureEdit(2, PlayStructureOperation.RemoveComponent, Player, Script), new PlayStructureResult(2, 11, PlayEditStatus.Applied, [], Guid.Empty, null));
        // Failed: not kept
        record.Add(new PlayStructureEdit(3, PlayStructureOperation.RemoveComponent, Other, Script), new PlayStructureResult(3, 12, PlayEditStatus.Failed, [], Guid.Empty, "No."));

        Assert.True(record.IsComponentAdded(Player, added));
        Assert.True(record.IsEdited(Player, added, "Color"));
        Assert.True(record.IsComponentRemoved(Player, Script));
        Assert.False(record.IsComponentRemoved(Other, Script));
        Assert.True(record.HasEdits(Player));
        Assert.False(record.HasEdits(Other));
        Assert.Equal(2, changed);
    }

    private static PlayEdit Set(int handle, Guid componentId, params PlayPathSegment[] path)
        => Edit(handle, componentId, PlayEditOperation.Set, path);

    private static PlayEdit Edit(int handle, Guid componentId, PlayEditOperation operation, params PlayPathSegment[] path)
        => new(1, handle, componentId, path, operation, PlayEditValue.Null);
}
