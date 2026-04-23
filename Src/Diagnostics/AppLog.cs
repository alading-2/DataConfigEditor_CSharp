namespace DataConfigEditor.Diagnostics;

public static class AppLog
{
    private static readonly object SyncRoot = new();
    public static string LogPath => Path.Combine(AppDirectory, "app.log");
    private static string AppDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DataConfigEditor");

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception ex)
    {
        Write("ERROR", $"{message}\n{ex}");
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(AppDirectory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";

            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // Swallow logging failures. Diagnostics must never crash the app.
        }
    }
}
