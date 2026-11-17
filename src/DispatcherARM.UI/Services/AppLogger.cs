using System.IO;

namespace DispatcherARM.UI.Services;

/// <summary>
/// Простой файловый логгер приложения
/// Путь: %LocalAppData%\DispatcherARM\logs\app-{date}.log
/// </summary>
public static class AppLogger
{
    private static readonly object _lock = new();
    private static string LogFile => Path.Combine(
        AppSettings.AppDataDir, "logs",
        $"app-{DateTime.Now:yyyy-MM-dd}.log");

    static AppLogger()
    {
        Directory.CreateDirectory(Path.Combine(AppSettings.AppDataDir, "logs"));
    }

    public static void Info(string message) => Write("INFO ", message, null);
    public static void Warn(string message) => Write("WARN ", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    public static void Financial(string message) => Write("MONEY", message, null);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (_lock)
            {
                var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
                if (ex != null) line += $"\n         Exception: {ex.GetType().Name}: {ex.Message}";
                File.AppendAllText(LogFile, line + Environment.NewLine,
                    System.Text.Encoding.UTF8);
            }
        }
        catch { /* Логгер не должен падать приложение */ }
    }

    /// <summary>Получить путь к текущему файлу лога</summary>
    public static string GetCurrentLogPath() => LogFile;

    /// <summary>Читать последние N строк лога</summary>
    public static string[] ReadLastLines(int count = 200)
    {
        try
        {
            if (!File.Exists(LogFile)) return Array.Empty<string>();
            var lines = File.ReadAllLines(LogFile, System.Text.Encoding.UTF8);
            return lines.Length <= count ? lines : lines[^count..];
        }
        catch { return Array.Empty<string>(); }
    }
}
