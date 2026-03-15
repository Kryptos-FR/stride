using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;

namespace StrideAssets.VisualStudio
{
    [VisualStudioContribution]
    internal class StrideExtension : Extension
    {
        private static readonly string DiagLogPath = Path.Combine(
            Path.GetTempPath(), "stride-vs-extension.log");

        internal static void DiagLog(string message)
        {
            try
            {
                File.AppendAllText(DiagLogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { /* best-effort */ }
        }

        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            RequiresInProcessHosting = true,
        };

        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);
            DiagLog("[Extension] InitializeServices called — Extensibility runtime activated");
        }

        protected override async Task OnInitializedAsync(VisualStudioExtensibility extensibility, CancellationToken cancellationToken)
        {
            await base.OnInitializedAsync(extensibility, cancellationToken);
            DiagLog("[Extension] OnInitializedAsync completed");
        }
    }
}
