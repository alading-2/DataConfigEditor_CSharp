namespace DataConfigEditor.Workspace;

public sealed class AppLaunchOptions
{
    public string? InitialDirectory { get; init; }

    public static AppLaunchOptions Parse(string[] args)
    {
        var initialDirectory = args.FirstOrDefault(Directory.Exists);
        return new AppLaunchOptions
        {
            InitialDirectory = initialDirectory,
        };
    }
}
