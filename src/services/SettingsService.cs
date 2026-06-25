using System.IO;
using System.Text.Json;

namespace ExanimapHelper;

/// <summary>User settings persisted between launches. Only the poll interval is kept.</summary>
public sealed class AppSettings
{
    public int IntervalMs { get; set; } = 1000;
}

/// <summary>
/// Loads/saves <see cref="AppSettings"/> as JSON under %APPDATA%\ExanimapHelper.
/// Both operations are best-effort: failures (missing/corrupt file, no write access)
/// fall back to defaults rather than throwing.
/// </summary>
public static class SettingsService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExanimapHelper");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            // Ignore unreadable/corrupt settings and use defaults.
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // Best-effort; losing a persisted setting is not worth crashing over.
        }
    }
}
