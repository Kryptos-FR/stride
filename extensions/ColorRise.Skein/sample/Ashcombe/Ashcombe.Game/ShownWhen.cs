using ColorRise.Skein;
using Stride.Core;
using Stride.Engine;

namespace Ashcombe;

/// <summary>
/// Shows its entity while a variable of the story is true: the wreck once the ship ran aground, the light of the lamp
/// once it burns. With <see cref="Not"/>, while it is false: the oil can until it is taken.
/// </summary>
[Display("Shown when")]
[ComponentCategory("Ashcombe")]
public sealed class ShownWhen : SyncScript
{
    private bool applied = true;

    /// <summary>
    /// The variable of the story, a boolean: <c>$lamp_lit</c>.
    /// </summary>
    /// <userdoc>The boolean variable of the story that shows this entity, as in $lamp_lit.</userdoc>
    [DataMember(10)]
    public string Variable { get; set; } = "";

    /// <summary>
    /// Whether the entity shows while the variable is false.
    /// </summary>
    /// <userdoc>Show the entity while the variable is false, and hide it once it is true.</userdoc>
    [DataMember(20)]
    public bool Not { get; set; }

    /// <summary>
    /// Whether the entity shows now.
    /// </summary>
    [DataMemberIgnore]
    public bool IsShown { get; private set; } = true;

    /// <inheritdoc />
    public override void Start() => Update();

    /// <inheritdoc />
    public override void Update()
    {
        // The variables are the ones of the game's dialogues, which the runner of the scene shares
        var variables = Services.GetService<DialogueSystem>()?.Variables;
        IsShown = variables is null || !variables.TryGetValue<bool>(Variable, out var value) ? Not : value != Not;
        if (IsShown == applied)
            return;

        applied = IsShown;
        Entity.EnableAll(IsShown, applyOnChildren: true);
    }
}
