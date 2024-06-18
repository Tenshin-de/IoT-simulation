using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LastTry.ViewModels;
using System.Collections.Generic;

namespace LastTry.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            DataContext = new MainViewModel();
        }

        public static IEnumerable<DeviceLogModel> DataSource { get; internal set; }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
