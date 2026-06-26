using Avalonia;

namespace ExanimapHelper;

internal static class Program
{
    // Avalonia configuration. Called by the visual designer too, so it must stay
    // side-effect free apart from building the app.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
