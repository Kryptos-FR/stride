using System;
using Avalonia;
using Avalonia.Logging.Serilog;
using Xenko.GameStudio2.ViewModels;
using Xenko.GameStudio2.Views;

namespace Xenko.GameStudio2
{
    class Program
    {
        static void Main(string[] args)
        {
            BuildAvaloniaApp().Start<MainWindow>(() => new MainWindowViewModel());
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                           .UseWin32().UseDirect2D1()
                           .UseReactiveUI()
                           .LogToDebug();
        }
    }
}
