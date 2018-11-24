using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Xenko.GameStudio2.Views
{
    public class GameStudioWindow : Window
    {
        public GameStudioWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
