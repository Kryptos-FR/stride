using ColorRise.Tessera.Editor.Pictures;
using Stride.Core.Assets.Editor.Services;
using Stride.Editor;

namespace ColorRise.Tessera.Editor;

/// <summary>
/// Game Studio's entry point into Tessera: the editors, previews and commands declared in this assembly are found through it.
/// </summary>
public sealed class TesseraEditorPlugin : StrideAssetsPlugin
{
    /// <summary>
    /// Registers the session's services: the pictures of the tile sets.
    /// </summary>
    public override void InitializeSession(ISessionPluginContext context)
    {
        base.InitializeSession(context);

        var pictures = new TilePictureService(context.Session);
        context.Services.Register(pictures);
        context.OnClose(pictures.Dispose);
    }
}
