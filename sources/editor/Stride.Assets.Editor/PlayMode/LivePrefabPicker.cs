// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Stride.Assets.Entities;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Presentation.ViewModels;
using Stride.Core.Diagnostics;
using Stride.Editor.PlayMode;

namespace Stride.Assets.Editor.PlayMode;

/// <summary>
/// The picker of the Live tab's Spawn prefab: the asset picker lists the prefabs of the session, those of the game's build
/// pickable and the others disabled with the reason.
/// </summary>
/// <param name="session">The session of the editor.</param>
[Experimental("STREXP012")]
public sealed class LivePrefabPicker(ISessionViewModel session) : ILivePrefabPicker
{
    private static readonly Logger Log = GlobalLogger.GetLogger("PlayMode");

    /// <inheritdoc/>
    public async Task<string?> PickPrefabAsync(PlaySession playSession, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(playSession);
        if (session.ServiceProvider.TryGet<IAssetPickerService>() is not { } pickers)
            return null;
        var context = new LiveReferenceContext(session, () => playSession, () => null, () => null, Log);
        var (end, registration) = context.WatchEnd();
        using (registration)
        using (var closing = CancellationTokenSource.CreateLinkedTokenSource(end, cancellationToken))
        {
            var options = new AssetPickerOptions([typeof(PrefabAsset)])
            {
                DisabledReason = context.ReasonFor,
                Note = context.NoteFor,
                Message = "Prefabs of the game's build can be spawned while it runs; the others are listed disabled.",
                Cancellation = closing.Token,
            };
            var picked = await pickers.OpenAssetPickerAsync(session, options);
            // The game stopped meanwhile: nothing is spawned
            if (picked is null || closing.IsCancellationRequested || context.ReasonFor(picked) is not null)
                return null;
            if (context.NoteFor(picked) is { } note)
                Log.Info($"{picked.Url}: {note}");
            return context.BuildUrlOf(picked);
        }
    }
}
