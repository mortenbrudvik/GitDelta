using GitDelta.UI.ViewModels;
using Wpf.Ui.Controls;

namespace GitDelta.UI.Views;

public partial class SettingsDialog : ContentDialog
{
    public SettingsDialog(ContentDialogHost? dialogHost, SettingsViewModel viewModel)
        : base(dialogHost)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
