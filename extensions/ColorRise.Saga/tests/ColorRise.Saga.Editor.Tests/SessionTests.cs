using ColorRise.Saga.Assets;
using ColorRise.Saga.Editor.Tests.Helpers;
using ColorRise.Saga.Editor.ViewModels;

namespace ColorRise.Saga.Editor.Tests;

public sealed class SessionTests
{
    [Fact]
    public Task SessionShowsSagaAssetsWithTheirViewModels() => SagaSessionFixture.RunAsync(fixture =>
    {
        Assert.False(fixture.Result.HasErrors, fixture.Log);
        Assert.IsType<StoryViewModel>(fixture.Asset<StoryViewModel>("Story/Main"));
        Assert.IsType<DialogueViewModel>(fixture.Asset<DialogueViewModel>("Dialogues/Foreman"));
        return Task.CompletedTask;
    }, ("Story/Main", new StoryAssetFactory().New()), ("Dialogues/Foreman", new DialogueAsset()));
}
