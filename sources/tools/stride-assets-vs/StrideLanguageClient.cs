#pragma warning disable CS0067 // StopAsync event is required by ILanguageClient but not raised directly

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace StrideAssets.VisualStudio
{
    [ContentType("stride-asset")]
    [Export(typeof(ILanguageClient))]
    public class StrideLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
    {
        [Import]
        internal VisualStudioWorkspace? Workspace { get; set; }

        public string Name => "Stride Asset Navigator";

        public IEnumerable<string>? ConfigurationSections => new[] { "strideAssets" };

        public object? InitializationOptions => null;

        public IEnumerable<string>? FilesToWatch => null;

        public bool ShowNotificationOnInitializeFailed => true;

        public event AsyncEventHandler<EventArgs>? StartAsync;
        public event AsyncEventHandler<EventArgs>? StopAsync;

        // ILanguageClientCustomMessage2
        public object? MiddleLayer { get; } = new SettingsMiddleLayer();

        private CSharpSymbolHandler? _handler;
        public object? CustomMessageTarget => _handler ??= new CSharpSymbolHandler(() => Workspace);

        private JsonRpc? _rpc;

        public async Task<Connection?> ActivateAsync(CancellationToken token)
        {
            var serverPath = FindServerPath();
            if (serverPath == null)
            {
                throw new FileNotFoundException(
                    "Could not find the Stride LSP server (server.js). " +
                    "Ensure Node.js is installed and the server is built. " +
                    "Set STRIDE_LSP_SERVER_PATH environment variable to override.");
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{serverPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var process = Process.Start(processInfo)
                ?? throw new InvalidOperationException(
                    "Failed to start Node.js process. Ensure Node.js is installed and on PATH.");

            return new Connection(
                process.StandardOutput.BaseStream,
                process.StandardInput.BaseStream);
        }

        public async Task OnLoadedAsync()
        {
            if (StartAsync != null)
            {
                await StartAsync.InvokeAsync(this, EventArgs.Empty);
            }
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(
            ILanguageClientInitializationInfo initializationState)
        {
            return Task.FromResult<InitializationFailureContext?>(
                new InitializationFailureContext
                {
                    FailureMessage = $"Stride LSP server failed to initialize: {initializationState.StatusMessage}"
                });
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc)
        {
            _rpc = rpc;
            return Task.CompletedTask;
        }

        private static string? FindServerPath()
        {
            // 1. Environment variable override
            var envPath = Environment.GetEnvironmentVariable("STRIDE_LSP_SERVER_PATH");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
                return envPath;

            var extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            // 2. VSIX directory (installed extension ships server alongside)
            var vsixPath = Path.Combine(extensionDir, "server", "out", "server.js");
            if (File.Exists(vsixPath))
                return vsixPath;

            // 3. Walk up to find the repo root (development / F5 debugging)
            var dir = extensionDir;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                var devPath = Path.Combine(dir, "sources", "tools", "stride-assets",
                    "packages", "server", "out", "server.js");
                if (File.Exists(devPath))
                    return devPath;
                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }
    }

    /// <summary>
    /// Middleware that intercepts workspace/configuration requests from the server
    /// and returns settings from the VS options page.
    /// </summary>
    internal class SettingsMiddleLayer : ILanguageClientMiddleLayer
    {
        public bool CanHandle(string methodName)
            => methodName == "workspace/configuration";

        public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
        {
            return sendNotification(methodParam);
        }

        public Task<JToken?> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken?>> sendRequest)
        {
            var settings = new JObject
            {
                ["diagnosticsEnabled"] = StrideSettings.DiagnosticsEnabled,
                ["scriptNavigationEnabled"] = StrideSettings.ScriptNavigationEnabled,
                ["backLinksEnabled"] = StrideSettings.BackLinksEnabled,
                ["scanWorkspaceForBrokenLinks"] = StrideSettings.ScanWorkspaceForBrokenLinks,
            };
            return Task.FromResult<JToken?>(new JArray(settings));
        }
    }
}
