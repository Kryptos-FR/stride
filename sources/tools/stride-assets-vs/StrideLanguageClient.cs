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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace StrideAssets.VisualStudio
{
    [ContentType("stride-asset")]
    [Export(typeof(ILanguageClient))]
    [RunOnContext(RunningContext.RunOnHost)]
    public class StrideLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
    {
        private static readonly string DiagLogPath = Path.Combine(
            Path.GetTempPath(), "stride-vs-extension.log");

        private static void DiagLog(string message)
        {
            try
            {
                File.AppendAllText(DiagLogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { /* best-effort diagnostic logging */ }
        }

        public StrideLanguageClient()
        {
            DiagLog("StrideLanguageClient constructor called");
        }

        [Import]
        internal VisualStudioWorkspace? Workspace { get; set; }

        [Import(typeof(SVsServiceProvider))]
        internal IServiceProvider? ServiceProvider { get; set; }

        public string Name => "Stride Asset Navigator";

        public IEnumerable<string>? ConfigurationSections => null;

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
        private string? _solutionDir;
        private bool _serverReady;

        public async Task<Connection?> ActivateAsync(CancellationToken token)
        {
            DiagLog("ActivateAsync called");
            Log.Write("ActivateAsync called — starting server...");

            var serverPath = FindServerPath();
            if (serverPath == null)
            {
                Log.Error("Could not find server.js. Searched: STRIDE_LSP_SERVER_PATH env, VSIX dir, repo tree.");
                throw new FileNotFoundException(
                    "Could not find the Stride LSP server (server.js). " +
                    "Ensure Node.js is installed and the server is built. " +
                    "Set STRIDE_LSP_SERVER_PATH environment variable to override.");
            }

            Log.Write($"Server path: {serverPath}");

            var processInfo = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{serverPath}\" --stdio",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            Log.Write("Starting Node.js process...");
            var process = Process.Start(processInfo);
            if (process == null)
            {
                Log.Error("Failed to start Node.js process. Is Node.js installed and on PATH?");
                throw new InvalidOperationException(
                    "Failed to start Node.js process. Ensure Node.js is installed and on PATH.");
            }

            Log.Write($"Node.js process started (PID: {process.Id})");

            // Forward server stderr to the output pane
            _ = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await process.StandardError.ReadLineAsync()) != null)
                    {
                        Log.Write($"[server] {line}");
                    }
                }
                catch { /* process exited */ }
            });

            return new Connection(
                process.StandardOutput.BaseStream,
                process.StandardInput.BaseStream);
        }

        public async Task OnLoadedAsync()
        {
            DiagLog("OnLoadedAsync called");

            // Initialize the output window pane on the UI thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            DiagLog("Switched to main thread");
            if (ServiceProvider != null)
            {
                Log.Initialize(ServiceProvider);
                DiagLog("Log.Initialize completed");
            }
            else
            {
                DiagLog("WARNING: ServiceProvider is null!");
            }
            // Cache the solution directory while on the UI thread
            if (ServiceProvider?.GetService(typeof(SVsSolution)) is IVsSolution solution)
            {
                solution.GetSolutionInfo(out string solutionDir, out _, out _);
                _solutionDir = solutionDir;
            }

            // Subscribe to settings changes — pushes updates to server live
            StrideSettings.Changed += PushSettingsToServer;

            Log.Write("Stride Asset Navigator extension loaded");
            Log.Write($"Extension assembly: {Assembly.GetExecutingAssembly().Location}");
            Log.Write($"Solution directory: {_solutionDir ?? "(not available)"}");
            Log.Write($"Workspace available: {Workspace != null}");

            if (StartAsync != null)
            {
                await StartAsync.InvokeAsync(this, EventArgs.Empty);
            }
        }

        public Task OnServerInitializedAsync()
        {
            Log.Write("LSP server initialized successfully");
            _serverReady = true;

            // Push current settings to server (may be defaults if package hasn't loaded yet;
            // the package will push again when it loads persisted values from the registry)
            PushSettingsToServer();

            return Task.CompletedTask;
        }

        public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(
            ILanguageClientInitializationInfo initializationState)
        {
            Log.Error($"LSP server initialization failed: {initializationState.StatusMessage}");
            return Task.FromResult<InitializationFailureContext?>(
                new InitializationFailureContext
                {
                    FailureMessage = $"Stride LSP server failed to initialize: {initializationState.StatusMessage}"
                });
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc)
        {
            _rpc = rpc;
            Log.Write("Custom message handler attached (C# symbol resolution ready)");
            Log.Debug($"[LanguageClient] JsonRpc attached, handler type: {_handler?.GetType().Name ?? "(null)"}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sends current settings to the LSP server via workspace/didChangeConfiguration.
        /// Called on initial server connection and whenever the user changes settings.
        /// </summary>
        private void PushSettingsToServer()
        {
            if (_rpc == null || !_serverReady)
            {
                Log.Debug("[LanguageClient] PushSettingsToServer: skipped (server not ready)");
                return;
            }

            try
            {
                var settings = new JObject
                {
                    ["strideAssets"] = new JObject
                    {
                        ["diagnosticsEnabled"] = StrideSettings.DiagnosticsEnabled,
                        ["scriptNavigationEnabled"] = StrideSettings.ScriptNavigationEnabled,
                        ["backLinksEnabled"] = StrideSettings.BackLinksEnabled,
                        ["scanWorkspaceForBrokenLinks"] = StrideSettings.ScanWorkspaceForBrokenLinks,
                    },
                };

                Log.Write($"Pushing settings to server: diagnostics={StrideSettings.DiagnosticsEnabled}, " +
                           $"scriptNav={StrideSettings.ScriptNavigationEnabled}, " +
                           $"backLinks={StrideSettings.BackLinksEnabled}");

#pragma warning disable CS4014 // fire-and-forget is intentional
                _rpc.NotifyWithParameterObjectAsync(
                    "workspace/didChangeConfiguration",
                    new JObject { ["settings"] = settings });
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to push settings: {ex.Message}");
            }
        }

        private string? FindServerPath()
        {
            const string relativeServerPath = @"sources\tools\stride-assets\packages\server\out\server.js";

            // 1. Environment variable override
            var envPath = Environment.GetEnvironmentVariable("STRIDE_LSP_SERVER_PATH");
            Log.Write($"STRIDE_LSP_SERVER_PATH env: {envPath ?? "(not set)"}");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
                return envPath;

            var extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            Log.Write($"Extension directory: {extensionDir}");

            // 2. VSIX directory (installed extension ships server alongside)
            var vsixPath = Path.Combine(extensionDir, "server", "out", "server.js");
            Log.Write($"Checking VSIX path: {vsixPath} — {(File.Exists(vsixPath) ? "FOUND" : "not found")}");
            if (File.Exists(vsixPath))
                return vsixPath;

            // 3. Walk up from extension directory to find repo root
            var dir = extensionDir;
            for (int i = 0; i < 10 && dir != null; i++)
            {
                var devPath = Path.Combine(dir, relativeServerPath);
                if (File.Exists(devPath))
                {
                    Log.Write($"Found server via extension dir walk: {devPath}");
                    return devPath;
                }
                dir = Path.GetDirectoryName(dir);
            }

            // 4. Walk up from the solution directory (F5 debugging: extension is in AppData,
            //    but the solution is in the repo tree)
            if (_solutionDir != null)
            {
                Log.Write($"Searching from solution directory: {_solutionDir}");
                dir = _solutionDir;
                for (int i = 0; i < 10 && dir != null; i++)
                {
                    var devPath = Path.Combine(dir, relativeServerPath);
                    if (File.Exists(devPath))
                    {
                        Log.Write($"Found server via solution dir walk: {devPath}");
                        return devPath;
                    }
                    dir = Path.GetDirectoryName(dir);
                }
            }

            Log.Write("Server not found in any location");
            return null;
        }
    }

    /// <summary>
    /// Middleware that intercepts hover responses to strip markdown for VS plaintext rendering.
    /// Settings are pushed via workspace/didChangeConfiguration instead of being intercepted here.
    /// </summary>
    internal class SettingsMiddleLayer : ILanguageClientMiddleLayer
    {
        public bool CanHandle(string methodName)
        {
            var result = methodName == "textDocument/hover";
            Log.Debug($"[Middleware] CanHandle({methodName}) = {result}");
            return result;
        }

        public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
        {
            Log.Debug($"[Middleware] HandleNotificationAsync: {methodName}");
            return sendNotification(methodParam);
        }

        public async Task<JToken?> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken?>> sendRequest)
        {
            if (methodName == "textDocument/hover")
            {
                var result = await sendRequest(methodParam);
                if (result != null)
                    StripMarkdown(result);
                return result;
            }

            return await sendRequest(methodParam);
        }

        /// <summary>
        /// Converts MarkupContent markdown to plaintext since VS doesn't render markdown in hovers.
        /// </summary>
        private static void StripMarkdown(JToken hover)
        {
            var contents = hover["contents"];
            if (contents == null) return;

            // MarkupContent: { kind: "markdown", value: "..." }
            if (contents["kind"]?.Value<string>() == "markdown")
            {
                var value = contents["value"]?.Value<string>() ?? "";
                value = System.Text.RegularExpressions.Regex.Replace(value, @"\[([^\]]+)\]\([^)]+\)", "$1"); // [text](url) → text
                value = System.Text.RegularExpressions.Regex.Replace(value, @"^#{1,3}\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline); // ### headings
                value = System.Text.RegularExpressions.Regex.Replace(value, @"^[-*]\s+", "• ", System.Text.RegularExpressions.RegexOptions.Multiline); // bullets
                value = value
                    .Replace("**", "")           // bold
                    .Replace("`", "")            // inline code
                    .Replace("  \n", "\n")       // hard line breaks
                    .Replace("---", "────────"); // horizontal rules
                contents["value"] = value;
                contents["kind"] = "plaintext";
            }
        }
    }
}
