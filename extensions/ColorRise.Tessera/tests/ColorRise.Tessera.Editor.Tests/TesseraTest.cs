using Stride.Assets.Editor.Testing;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.Testing;
using Stride.Core.Presentation.Services;

namespace ColorRise.Tessera.Editor.Tests;

/// <summary>
/// Opens a session of the editor test kit as Game Studio opens one once Tessera is loaded.
/// </summary>
internal static class TesseraTest
{
    public static DispatcherTask<EditorTestSession> OpenAsync(params (string Location, Asset Asset)[] assets)
    {
        var options = new StrideEditorTestSessionOptions
        {
            Plugins = [new TesseraEditorPlugin()],
            InitializeSession = true,
        };
        return StrideEditorTestSession.OpenAsync(options, assets);
    }
}
