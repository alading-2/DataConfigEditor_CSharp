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
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

        try
        {
            Directory.CreateDirectory(AppDirectory);

            lock (SyncRoot)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Swallow logging failures. Diagnostics must never crash the app.
        }

        try
        {
            if (level == "ERROR")
                Console.Error.WriteLine(line);
            else
                Console.WriteLine(line);
        }
        catch
        {
            // Swallow console logging failures as well.
        }
    }
}
