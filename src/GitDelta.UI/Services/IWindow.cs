using System.Windows;

namespace GitDelta.UI.Services;

/// <summary>
/// Abstraction over the application's main window so the hosted service can
/// show it and assign its DataContext without a hard reference to the concrete
/// <c>MainWindow</c> type. <see cref="System.Windows.FrameworkElement"/>
/// already supplies <see cref="DataContext"/>, <see cref="Loaded"/>,
/// <see cref="Show"/>, and <see cref="Close"/>.
/// </summary>
public interface IWindow
{
    object? DataContext { get; set; }
    event RoutedEventHandler Loaded;
    void Show();
    void Close();
}
