using Stride.Core.Serialization.Contents;
using Stride.Engine;

namespace Ashcombe;

/// <summary>
/// Loads the scene of a place by its URL, and lets it go. The game loads them from its content; a test makes them in
/// code.
/// </summary>
public interface IPlaceLoader
{
    /// <summary>
    /// Loads the scene of a place.
    /// </summary>
    /// <param name="url">The URL of the scene, as <c>Places/Village/Inn</c>.</param>
    Scene Load(string url);

    /// <summary>
    /// Lets a scene go that <see cref="Load"/> gave.
    /// </summary>
    void Unload(Scene scene);

    /// <summary>
    /// The URL of a scene that was loaded, the one the game started with included.
    /// </summary>
    /// <returns>Null when the scene was not loaded from a URL.</returns>
    string? UrlOf(Scene scene);
}

/// <summary>
/// Loads the places from the game's content.
/// </summary>
public sealed class ContentPlaceLoader(ContentManager content) : IPlaceLoader
{
    /// <inheritdoc />
    public Scene Load(string url) => content.Load<Scene>(url);

    /// <inheritdoc />
    public void Unload(Scene scene) => content.Unload(scene);

    /// <inheritdoc />
    public string? UrlOf(Scene scene) => content.TryGetAssetUrl(scene, out var url) ? Places.Normalize(url) : null;
}

/// <summary>
/// What the game knows of the URLs of its places.
/// </summary>
public static class Places
{
    /// <summary>
    /// The folder of the places among the assets: every scene of a place is under it.
    /// </summary>
    public const string Folder = "Places/";

    /// <summary>
    /// The URL of a place as the game compares it: from <see cref="Folder"/> on, whatever the content manager puts
    /// before it (the package's name, a slash).
    /// </summary>
    public static string Normalize(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var start = url.IndexOf(Folder, StringComparison.Ordinal);
        return start >= 0 ? url[start..] : url.TrimStart('/');
    }
}
