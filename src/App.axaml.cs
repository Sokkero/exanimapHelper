using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ExanimapHelper;

public partial class App : Application
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "crash.log");

    public override void Initialize()
    {
        // Avalonia has no DispatcherUnhandledException; these two cover background
        // and task faults. UI-thread exceptions surface through the platform and are
        // logged here as best-effort. Logging only — no modal dialog, since at this
        // point a usable window may not exist.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Report("AppDomain", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report("Task", e.Exception);
            e.SetObserved();
        };

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }

    private static void Report(string source, Exception? ex)
    {
        string message = ex?.ToString() ?? "Unknown error.";
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:u}] {source}\n{message}\n\n");
        }
        catch
        {
            // Logging is best-effort; never let it mask the original failure.
        }
        Console.Error.WriteLine($"[{source}] {message}");
    }
}
