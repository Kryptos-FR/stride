namespace ColorRise.Skein.Editor.Tests;

/// <summary>
/// The functions of the game the scripts of the tests call. The editor finds them in this assembly, as it finds a
/// game's: <see cref="TestAssemblies"/> registers it.
/// </summary>
internal static class GameFunctions
{
    [YarnFunction("has_item")]
    public static bool HasItem(string item) => item.Length > 0;

    [YarnFunction("debt")]
    public static float Debt(string who) => who.Length;

    [YarnFunction("tide")]
    public static float Tide() => 0;
}
