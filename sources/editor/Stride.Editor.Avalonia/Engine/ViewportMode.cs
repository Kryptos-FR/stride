// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.Editor.Avalonia.Engine;

/// <summary>
/// How the editor shows the frames of an embedded game (viewport spike).
/// </summary>
public enum ViewportMode
{
    /// <summary>
    /// Option A: the game renders into its own SDL window, which a <c>NativeControlHost</c> reparents into the editor.
    /// </summary>
    NativeWindow,

    /// <summary>
    /// Option B': the game renders offscreen (no window), and each frame is read back to the CPU and copied into a
    /// <c>WriteableBitmap</c>.
    /// </summary>
    CpuReadback,

    /// <summary>
    /// Option B: the game renders offscreen into a texture shared with Avalonia's compositor through its GPU interop
    /// (<c>ICompositionGpuInterop</c>). Falls back to <see cref="CpuReadback"/> when the platform has no interop.
    /// </summary>
    GpuInterop,
}

/// <summary>
/// Selects the <see cref="ViewportMode"/> of the editor's embedded games (viewport spike).
/// </summary>
/// <remarks>
/// The environment variable <c>STRIDE_EDITOR_VIEWPORT</c> selects it: <c>native</c> (the default), <c>readback</c> or
/// <c>gpu</c>. <c>STRIDE_EDITOR_VIEWPORT_SIZE</c> (for example <c>1920x1080</c>) renders the offscreen modes at a fixed
/// size, scaled into the control, to measure their cost at a size the panel doesn't have.
/// </remarks>
public static class ViewportModeSelector
{
    private const string ModeVariable = "STRIDE_EDITOR_VIEWPORT";
    private const string SizeVariable = "STRIDE_EDITOR_VIEWPORT_SIZE";

    /// <summary>
    /// Gets the mode selected for this process.
    /// </summary>
    public static ViewportMode Current { get; } = Parse(Environment.GetEnvironmentVariable(ModeVariable));

    /// <summary>
    /// Gets the fixed back buffer size of the offscreen modes, or <c>null</c> to follow the control.
    /// </summary>
    public static (int Width, int Height)? ForcedSize { get; } = ParseSize(Environment.GetEnvironmentVariable(SizeVariable));

    private static ViewportMode Parse(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "readback" or "cpu" => ViewportMode.CpuReadback,
            "gpu" or "interop" => ViewportMode.GpuInterop,
            _ => ViewportMode.NativeWindow,
        };
    }

    private static (int, int)? ParseSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var parts = value.Split('x', 'X');
        if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height) && width > 0 && height > 0)
            return (width, height);
        return null;
    }
}
