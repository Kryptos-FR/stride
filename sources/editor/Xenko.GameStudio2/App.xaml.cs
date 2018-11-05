using Avalonia;
using Avalonia.Markup.Xaml;

namespace Xenko.GameStudio2
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
