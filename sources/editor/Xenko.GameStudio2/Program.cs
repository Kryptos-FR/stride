using System;
using System.Runtime.ExceptionServices;
using System.Security;
using Avalonia;
using Avalonia.Logging.Serilog;
using Xenko.GameStudio2.ViewModels;
using Xenko.GameStudio2.Views;

namespace Xenko.GameStudio2
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            BuildAvaloniaApp().Start<GameStudioWindow>(() => new GameStudioViewModel());
        }

        private static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                           .UseWin32().UseDirect2D1()
                           .UseReactiveUI()
                           .LogToDebug();
        }
        
        [SecurityCritical]
        [HandleProcessCorruptedStateExceptions]
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.IsTerminating)
            {
                HandleException(e.ExceptionObject as Exception, 1);
            }
        }
        
        private static void HandleException(Exception exception, int location)
        {
            if (exception == null) return;

            // TODO: crash report
        }
    }
}
