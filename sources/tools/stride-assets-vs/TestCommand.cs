using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;

namespace StrideAssets.VisualStudio
{
    /// <summary>
    /// Minimal command to ensure the Extensibility runtime activates.
    /// Appears under Tools menu. DELETE once CodeLens works independently (Phase 6).
    /// </summary>
    [VisualStudioContribution]
    internal class TestCommand : Command
    {
        public override CommandConfiguration CommandConfiguration => new("%StrideAssets.VisualStudio.TestCommand.DisplayName%")
        {
            Placements = [CommandPlacement.KnownPlacements.ToolsMenu],
        };

        public override Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
        {
            StrideExtension.DiagLog("TestCommand executed — Extensibility runtime is working");
            return Task.CompletedTask;
        }
    }
}
