#pragma warning disable VSTHRD010 // OutputStringThreadSafe is explicitly designed for cross-thread use

using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace StrideAssets.VisualStudio
{
    /// <summary>
    /// Writes to a dedicated "Stride Asset Navigator" pane in the VS Output window.
    /// All methods are thread-safe (uses OutputStringThreadSafe).
    /// </summary>
    internal static class Log
    {
        private static readonly Guid PaneGuid = new Guid("e7b5c6a7-8e9f-4a1b-b2c3-d4e5f6a7b8c9");
        private static IVsOutputWindowPane? _pane;

        public static void Initialize(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (serviceProvider.GetService(typeof(SVsOutputWindow)) is IVsOutputWindow outputWindow)
            {
                var guid = PaneGuid;
                outputWindow.CreatePane(ref guid, "Stride Asset Navigator", fInitVisible: 1, fClearWithSolution: 0);
                outputWindow.GetPane(ref guid, out _pane);
            }
        }

        public static void Write(string message)
        {
            _pane?.OutputStringThreadSafe($"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }

        public static void Error(string message)
        {
            _pane?.OutputStringThreadSafe($"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}\n");
        }

        /// <summary>
        /// Verbose debug logging — only active in DEBUG builds, stripped in Release.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Debug(string message)
        {
            _pane?.OutputStringThreadSafe($"[{DateTime.Now:HH:mm:ss.fff}] [DEBUG] {message}\n");
        }
    }
}
