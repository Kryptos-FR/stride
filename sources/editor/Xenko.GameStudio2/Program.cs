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
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .UseReactiveUI()
                .LogToDebug();
    }
}
