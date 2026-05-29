using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace GitDelta.UI.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
    }
}
