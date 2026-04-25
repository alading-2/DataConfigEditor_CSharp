namespace DataConfigEditor.Workspace;

public sealed class AppLaunchOptions
{
    public string? InitialDirectory { get; init; }
    public string? MetadataAssemblyPath { get; init; }

    public static AppLaunchOptions Parse(string[] args)
    {
        string? metadataAssemblyPath = null;
        var candidateDirectories = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--dll" or "--metadata-dll")
            {
                if (i + 1 < args.Length)
                {
                    metadataAssemblyPath = args[i + 1];
                    i++;
                }

                continue;
            }

            candidateDirectories.Add(arg);
        }

        var initialDirectory = candidateDirectories.FirstOrDefault(Directory.Exists);
        return new AppLaunchOptions
        {
            InitialDirectory = initialDirectory,
            MetadataAssemblyPath = metadataAssemblyPath,
        };
    }
}
