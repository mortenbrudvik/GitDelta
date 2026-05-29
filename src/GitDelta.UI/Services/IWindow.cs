using System.Windows;

namespace GitDelta.UI.Services;

public interface IWindow
{
    event RoutedEventHandler Loaded;
    void Show();
    void Close();
}
