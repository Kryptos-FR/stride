namespace ColorRise.Skein;

/// <summary>
/// One pick among the nodes of a node group or the lines of a line group, with the reason of each candidate: what Game
/// Studio's player shows, and what a game logs to understand its own story.
/// </summary>
/// <param name="Group">The node group, or for a line group the node the lines are in.</param>
/// <param name="IsLineGroup">Whether the candidates are lines of a line group (their names are line ids).</param>
/// <param name="Candidates">Every candidate, in the order of the scripts.</param>
/// <param name="Chosen">The name of the candidate that runs, or null when none can.</param>
public sealed record SaliencyPick(string Group, bool IsLineGroup, IReadOnlyList<SaliencyCandidate> Candidates, string? Chosen);

/// <summary>
/// A candidate of a <see cref="SaliencyPick"/>.
/// </summary>
/// <param name="Name">The name of the node in the program, or the id of the line.</param>
/// <param name="Complexity">How specific its conditions are: the most specific wins.</param>
/// <param name="Passed">Whether all its conditions hold.</param>
/// <param name="FailedCondition">For a node whose conditions don't all hold, the first <c>when:</c> that fails, as
/// written when the session knows the nodes' headers, else its place (<c>when #2</c>). Null for a line.</param>
/// <param name="ViewCount">How often it ran before, for the strategies that avoid repeats.</param>
public sealed record SaliencyCandidate(string Name, int Complexity, bool Passed, string? FailedCondition, int ViewCount);
