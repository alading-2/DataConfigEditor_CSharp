using DataConfigEditor.Workspace;

namespace DataConfigEditor;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var options = AppLaunchOptions.Parse(args);
        Application.Run(new MainForm(options));
    }
}
