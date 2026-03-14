using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace StrideAssets.VisualStudio
{
    public class StrideOptionsPage : DialogPage
    {
        [Category("Diagnostics")]
        [DisplayName("Enable Diagnostics")]
        [Description("Show diagnostics (warnings/errors) for broken asset references, missing source files, and optionally missing script types.")]
        public bool DiagnosticsEnabled { get; set; } = true;

        [Category("Diagnostics")]
        [DisplayName("Scan Workspace for Broken Links")]
        [Description("Scan all asset files in the workspace for broken references on startup. May be slow on large projects.")]
        public bool ScanWorkspaceForBrokenLinks { get; set; } = false;

        [Category("Features")]
        [DisplayName("Enable Script Navigation")]
        [Description("Ctrl+click on script/component type references (e.g. !MyNamespace.MyScript,MyProject) to jump to the C# source file. Also enables property key navigation to C# fields.")]
        public bool ScriptNavigationEnabled { get; set; } = false;

        [Category("Features")]
        [DisplayName("Enable Back-Links")]
        [Description("Scan all asset files to find cross-references. Shows reference counts as CodeLens above Id: lines. May increase memory usage on large projects.")]
        public bool BackLinksEnabled { get; set; } = false;

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
            StrideSettings.Update(DiagnosticsEnabled, ScriptNavigationEnabled,
                BackLinksEnabled, ScanWorkspaceForBrokenLinks);
        }
    }

    /// <summary>
    /// Static settings holder. Notifies subscribers when settings change
    /// so the language client can push updates to the server immediately.
    /// </summary>
    internal static class StrideSettings
    {
        public static bool DiagnosticsEnabled { get; set; } = true;
        public static bool ScriptNavigationEnabled { get; set; }
        public static bool BackLinksEnabled { get; set; }
        public static bool ScanWorkspaceForBrokenLinks { get; set; }

        public static event Action? Changed;

        public static void Update(bool diagnostics, bool scriptNav, bool backLinks, bool scanWorkspace)
        {
            DiagnosticsEnabled = diagnostics;
            ScriptNavigationEnabled = scriptNav;
            BackLinksEnabled = backLinks;
            ScanWorkspaceForBrokenLinks = scanWorkspace;
            Changed?.Invoke();
        }
    }
}
