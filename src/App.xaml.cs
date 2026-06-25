using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ExanimapHelper;

public partial class App : Application
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "crash.log");

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            Report("Dispatcher", e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Report("AppDomain", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report("Task", e.Exception);
            e.SetObserved();
        };
    }

    private static void Report(string source, Exception? ex)
    {
        var message = ex?.ToString() ?? "Unknown error.";
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:u}] {source}\n{message}\n\n");
        }
        catch
        {
            // Logging is best-effort; never let it mask the original failure.
        }

        MessageBox.Show(
            $"ExanimapHelper hit an unhandled error and may be unstable.\n\n{message}\n\nLogged to: {LogPath}",
            "ExanimapHelper", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
