namespace Ashcombe.Tests;

/// <summary>
/// What writing Ashcombe found in Yarn Spinner (found on 3.2.2, still true on 3.3.1) and Skein, kept as tests so that a change shows.
/// </summary>
[Collection(GameCollection.Name)]
public sealed class FindingTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("tracking: always", true)]
    public void A_node_whose_visits_only_a_when_header_reads_is_not_tracked_unless_it_says_so(string tracking, bool seen)
    {
        // Yarn tracks the visits of a node that a visited() of a node's body names; one named only in a "when:"
        // header is not tracked, and visited() stays false there. Ashcombe's scripts say "tracking: always".
        var project = new TestProject("Story", $"""
            title: Summons
            {tracking}
            ---
            Maren: The Countess wants you.
            ===
            title: Greeting
            when: visited("Summons")
            ---
            Corvin: You came.
            ===
            """);
        using var game = new SceneHarness();
        game.Places.Add("Places/Village", () => Make.Root(project, new Recorder()));
        game.Start("Places/Village");

        var summons = game.Story.Talk("Summons");
        game.StepUntil(() => summons.IsCompleted);

        Assert.Equal(seen, game.Story.CanTalk("Greeting"));
    }
}
