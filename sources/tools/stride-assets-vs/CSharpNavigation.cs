using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

namespace StrideAssets.VisualStudio
{
    internal static class CSharpNavigation
    {
        // Matches !TypeName,Assembly or !TypeName anywhere in a line.
        // Group 1: full match after ! (e.g. SpaceEscape.CharacterScript,SpaceEscape.Game)
        // Group 2: type name  (e.g. SpaceEscape.CharacterScript)
        // Group 3: assembly name (e.g. SpaceEscape.Game, optional)
        internal static readonly Regex TypeTagRegex = new Regex(
            @"!(([A-Za-z_][\w.]*)(?:,\s*([A-Za-z_][\w.]*))?)",
            RegexOptions.Compiled);

        // Matches an indented YAML property key: leading whitespace + identifier + colon.
        // Group 1: property name (e.g. CharacterScript, Priority)
        internal static readonly Regex PropertyNameRegex = new Regex(
            @"^\s+([A-Za-z_]\w*):",
            RegexOptions.Compiled);

        /// <summary>
        /// Returns true if <paramref name="assemblyName"/> is the AssemblyName of a project
        /// in the current solution (i.e. it's user code, not an engine/framework assembly).
        /// </summary>
        internal static bool IsLocalAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return false;
            var componentModel = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            var workspace = componentModel?.GetService<VisualStudioWorkspace>();
            if (workspace == null) return false;
            return workspace.CurrentSolution.Projects.Any(
                p => string.Equals(p.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Scans backward from <paramref name="lineNumber"/> to find the type tag that encloses
        /// the property on that line, using indentation to avoid crossing into a sibling scope.
        /// </summary>
        internal static bool TryGetEnclosingTypeTag(ITextSnapshot snapshot, int lineNumber, string lineText,
            out string typeName, out string assemblyName)
        {
            typeName = assemblyName = string.Empty;
            int currentIndent = lineText.Length - lineText.TrimStart().Length;

            for (int i = lineNumber - 1; i >= Math.Max(0, lineNumber - 500); i--)
            {
                var prevText = snapshot.GetLineFromLineNumber(i).GetText();
                int prevIndent = prevText.Length - prevText.TrimStart().Length;
                if (prevIndent >= currentIndent) continue; // same or deeper nesting — skip

                var tm = TypeTagRegex.Match(prevText);
                if (tm.Success)
                {
                    typeName = tm.Groups[2].Value;
                    assemblyName = tm.Groups[3].Value;
                    return true;
                }
                // Less-indented line without a type tag: keep scanning upward.
            }
            return false;
        }

        /// <summary>
        /// Tries to navigate to the C# type declaration at <paramref name="col"/> in <paramref name="lineText"/>.
        /// Only navigates if the type's assembly is a local project.
        /// Must be called on the UI thread.
        /// </summary>
        internal static bool TryNavigate(string lineText, int col)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (Match m in TypeTagRegex.Matches(lineText))
            {
                if (col < m.Index || col > m.Index + m.Length) continue;

                var typeName = m.Groups[2].Value;       // e.g. SpaceEscape.CharacterScript
                var assemblyName = m.Groups[3].Value;   // e.g. SpaceEscape.Game (may be empty)
                if (!IsLocalAssembly(assemblyName)) return false;
                return ResolveAndNavigate(typeName, assemblyName);
            }
            return false;
        }

        private static bool ResolveAndNavigate(string typeName, string assemblyName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var componentModel = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            var workspace = componentModel?.GetService<VisualStudioWorkspace>();
            if (workspace == null)
            {
                Log.Write($"[CSharpNav] workspace is null for {typeName}");
                return false;
            }

            var projects = string.IsNullOrEmpty(assemblyName)
                ? workspace.CurrentSolution.Projects
                : workspace.CurrentSolution.Projects.Where(
                    p => string.Equals(p.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase));

            bool anyProject = false;
            foreach (var project in projects)
            {
                anyProject = true;
                if (!project.TryGetCompilation(out var compilation) || compilation == null)
                {
                    Log.Write($"[CSharpNav] {project.Name}: no cached compilation, queuing async");
                    QueueNavigation(project, typeName);
                    return true; // consume the event; navigation will happen async
                }

                var symbol = compilation.GetTypeByMetadataName(typeName);
                if (symbol == null)
                {
                    Log.Write($"[CSharpNav] {project.Name}: type '{typeName}' not found in compilation");
                    continue;
                }

                var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
                var filePath = location?.SourceTree?.FilePath;
                if (string.IsNullOrEmpty(filePath))
                {
                    Log.Write($"[CSharpNav] {project.Name}: no source location for '{typeName}'");
                    continue;
                }

                var line = location!.GetLineSpan().StartLinePosition.Line;
                Log.Write($"[CSharpNav] navigating to {filePath}:{line}");
                AssetNavigation.NavigateTo(filePath!, line);
                return true;
            }

            if (!anyProject)
                Log.Write($"[CSharpNav] no project matched assembly='{assemblyName}' for type '{typeName}'");

            return false;
        }

        /// <summary>
        /// Tries to navigate to the C# member declaration for a YAML property name at
        /// <paramref name="col"/> in <paramref name="lineText"/>.
        /// Only navigates if the enclosing type's assembly is a local project.
        /// Must be called on the UI thread.
        /// </summary>
        internal static bool TryNavigateToProperty(string lineText, int col, ITextSnapshot snapshot, int lineNumber)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var pm = PropertyNameRegex.Match(lineText);
            if (!pm.Success) return false;

            var nameGroup = pm.Groups[1];
            if (col < nameGroup.Index || col > nameGroup.Index + nameGroup.Length) return false;

            var memberName = nameGroup.Value;
            if (memberName == "Id") return false;

            if (!TryGetEnclosingTypeTag(snapshot, lineNumber, lineText, out var typeName, out var assemblyName))
                return false;

            if (!IsLocalAssembly(assemblyName)) return false;
            return ResolveAndNavigateToMember(typeName, assemblyName, memberName);
        }

        private static bool ResolveAndNavigateToMember(string typeName, string assemblyName, string memberName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var componentModel = ServiceProvider.GlobalProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            var workspace = componentModel?.GetService<VisualStudioWorkspace>();
            if (workspace == null) return false;

            var projects = string.IsNullOrEmpty(assemblyName)
                ? workspace.CurrentSolution.Projects
                : workspace.CurrentSolution.Projects.Where(
                    p => string.Equals(p.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase));

            foreach (var project in projects)
            {
                if (!project.TryGetCompilation(out var compilation) || compilation == null)
                {
                    QueueMemberNavigation(project, typeName, memberName);
                    return true;
                }

                var typeSymbol = compilation.GetTypeByMetadataName(typeName);
                // GetMembers returns only members declared directly on this type (not inherited).
                var member = typeSymbol?.GetMembers(memberName).FirstOrDefault();
                var location = member?.Locations.FirstOrDefault(l => l.IsInSource);
                var filePath = location?.SourceTree?.FilePath;
                if (string.IsNullOrEmpty(filePath)) continue;

                var line = location!.GetLineSpan().StartLinePosition.Line;
                AssetNavigation.NavigateTo(filePath!, line);
                return true;
            }
            return false;
        }

        private static void QueueMemberNavigation(Project project, string typeName, string memberName)
        {
#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
                if (compilation == null) return;

                var typeSymbol = compilation.GetTypeByMetadataName(typeName);
                var member = typeSymbol?.GetMembers(memberName).FirstOrDefault();
                var location = member?.Locations.FirstOrDefault(l => l.IsInSource);
                var filePath = location?.SourceTree?.FilePath;
                if (string.IsNullOrEmpty(filePath)) return;

                var line = location!.GetLineSpan().StartLinePosition.Line;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                AssetNavigation.NavigateTo(filePath!, line);
            }).FileAndForget("stride/member-navigation");
#pragma warning restore VSSDK007
        }

        private static void QueueNavigation(Project project, string typeName)
        {
#pragma warning disable VSSDK007 // FileAndForget is the correct fire-and-forget pattern here
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
                if (compilation == null) return;

                var symbol = compilation.GetTypeByMetadataName(typeName);
                var location = symbol?.Locations.FirstOrDefault(l => l.IsInSource);
                var filePath = location?.SourceTree?.FilePath;
                if (string.IsNullOrEmpty(filePath)) return;

                var line = location!.GetLineSpan().StartLinePosition.Line;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                AssetNavigation.NavigateTo(filePath!, line);
            }).FileAndForget("stride/csharp-navigation");
#pragma warning restore VSSDK007
        }
    }
}
