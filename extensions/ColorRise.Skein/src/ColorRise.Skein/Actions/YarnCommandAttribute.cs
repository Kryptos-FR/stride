namespace ColorRise.Skein;

/// <summary>
/// Makes a method a command of Yarn scripts: <c>&lt;&lt;open CellarDoor&gt;&gt;</c>.
/// </summary>
/// <remarks>
/// A static method is called as is. For an instance method, the first word after the command names the object to call
/// it on (an entity, for a method of a component). The other words are the parameters, converted from text: strings,
/// booleans, numbers, enums, and objects found by name as the target is. Optional parameters, a <c>params</c> array and a
/// <see cref="CancellationToken"/> work. The method returns nothing, or a task the dialogue waits for.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class YarnCommandAttribute : Attribute
{
    /// <summary>
    /// Names the command.
    /// </summary>
    /// <param name="name">The word scripts write, without spaces.</param>
    public YarnCommandAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// The word scripts write.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// What the command does, for the writers: editors show it.
    /// </summary>
    public string? Description { get; set; }
}
