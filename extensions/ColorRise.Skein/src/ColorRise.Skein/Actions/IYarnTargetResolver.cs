namespace ColorRise.Skein;

/// <summary>
/// Finds the objects commands name: the target of an instance command, and the parameters that are objects of the game.
/// </summary>
public interface IYarnTargetResolver
{
    /// <summary>
    /// Finds an object by the name a script wrote.
    /// </summary>
    /// <param name="name">The word of the command.</param>
    /// <param name="type">The type the command's method needs.</param>
    /// <returns>The object, or null when nothing of that type has that name.</returns>
    object? Find(string name, Type type);
}
