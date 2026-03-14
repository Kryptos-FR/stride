using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServices;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace StrideAssets.VisualStudio
{
    /// <summary>
    /// Handles the custom "stride/resolveCSharpSymbol" LSP request from the server.
    /// Uses the Roslyn workspace to find C# types and members directly,
    /// which is more reliable than the VS Code approach of querying symbol providers.
    /// </summary>
    public class CSharpSymbolHandler
    {
        private readonly Func<VisualStudioWorkspace?> _getWorkspace;

        public CSharpSymbolHandler(Func<VisualStudioWorkspace?> getWorkspace)
        {
            _getWorkspace = getWorkspace;
        }

        [JsonRpcMethod("stride/resolveCSharpSymbol")]
        public async Task<object> ResolveCSharpSymbolAsync(JToken arg, CancellationToken ct)
        {
            var typeName = arg["typeName"]?.Value<string>();
            var memberName = arg["memberName"]?.Value<string>();

            Log.Write($"[CSharpBridge] Resolve: type=\"{typeName}\", member=\"{memberName ?? "(none)"}\"");
            Log.Debug($"[CSharpBridge] Full request payload: {arg}");

            if (string.IsNullOrEmpty(typeName))
            {
                Log.Write("[CSharpBridge] Empty type name, returning empty result");
                return new JObject();
            }

            var workspace = _getWorkspace();
            if (workspace == null)
            {
                Log.Write("[CSharpBridge] Roslyn workspace not available");
                return new JObject();
            }

            var projects = workspace.CurrentSolution.Projects.ToList();
            Log.Write($"[CSharpBridge] Searching {projects.Count} project(s)");
            Log.Debug($"[CSharpBridge] Projects: {string.Join(", ", projects.Select(p => p.Name))}");

            foreach (var project in projects)
            {
                if (ct.IsCancellationRequested)
                    break;

                var compilation = await project.GetCompilationAsync(ct);
                if (compilation == null)
                    continue;

                var type = compilation.GetTypeByMetadataName(typeName!);
                if (type == null)
                    continue;

                Log.Write($"[CSharpBridge] Found type in project: {project.Name}");
                ISymbol targetSymbol = type;

                if (!string.IsNullOrEmpty(memberName))
                {
                    var member = type.GetMembers(memberName!)
                        .FirstOrDefault(m => m.Kind == SymbolKind.Property || m.Kind == SymbolKind.Field);
                    if (member != null)
                    {
                        Log.Write($"[CSharpBridge] Found member: {member.Name} ({member.Kind})");
                        targetSymbol = member;
                    }
                    else
                    {
                        Log.Write($"[CSharpBridge] Member \"{memberName}\" not found, falling back to type");
                    }
                }

                var location = targetSymbol.Locations.FirstOrDefault(l => l.IsInSource);
                if (location == null)
                    continue;

                var lineSpan = location.GetLineSpan();
                Log.Write($"[CSharpBridge] Resolved: {lineSpan.Path}:{lineSpan.StartLinePosition.Line}");

                return new JObject
                {
                    ["location"] = new JObject
                    {
                        ["uri"] = new Uri(lineSpan.Path).AbsoluteUri,
                        ["range"] = new JObject
                        {
                            ["start"] = new JObject
                            {
                                ["line"] = lineSpan.StartLinePosition.Line,
                                ["character"] = lineSpan.StartLinePosition.Character,
                            },
                            ["end"] = new JObject
                            {
                                ["line"] = lineSpan.EndLinePosition.Line,
                                ["character"] = lineSpan.EndLinePosition.Character,
                            },
                        },
                    },
                };
            }

            Log.Write($"[CSharpBridge] Type \"{typeName}\" not found in any project");
            return new JObject();
        }
    }
}
