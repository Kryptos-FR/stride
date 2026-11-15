namespace ColorRise.Skein;

/// <summary>
/// Says that a string member holds the title of a node, so that Game Studio offers the nodes of the project to pick from.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class YarnNodeAttribute : Attribute
{
    /// <summary>
    /// Ties the member to the project its nodes come from.
    /// </summary>
    /// <param name="projectMember">The name of the member of the same object that holds the <see cref="YarnProject"/>.</param>
    public YarnNodeAttribute(string projectMember)
    {
        ProjectMember = projectMember;
    }

    /// <summary>
    /// The name of the member of the same object that holds the <see cref="YarnProject"/>.
    /// </summary>
    public string ProjectMember { get; }
}
