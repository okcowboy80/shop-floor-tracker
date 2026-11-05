using System.Windows;
using System.Windows.Threading;

namespace InventoryTracker;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}",
            "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;   // keep the app running instead of crashing
    }
}