using ColorRise.Saga.Content;
using ColorRise.Saga.Expressions;
using Stride.Core.Assets;
using Stride.Core.Mathematics;

namespace ColorRise.Saga.Assets;

/// <summary>
/// Makes the story a template creates: one act to start from, and the player's name to show in lines.
/// </summary>
public sealed class StoryAssetFactory : AssetFactory<StoryAsset>
{
    public override StoryAsset New() => new()
    {
        Declarations = new StoryDeclarations
        {
            Variables = { new VariableDeclaration { Name = "player_name", Type = SagaTypeKind.String, Default = "\"Hero\"" } },
        },
        FirstActId = "act_1",
        Acts = { new ActCard { Id = "act_1", Title = "Act 1", Position = Vector2.Zero } },
    };
}
