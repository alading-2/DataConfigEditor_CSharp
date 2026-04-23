using DataConfigEditor.Diagnostics;
using DataConfigEditor.Workspace;

namespace DataConfigEditor;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            AppLog.Error("UI thread exception", e.Exception);
            MessageBox.Show(
                $"发生未处理异常：\n{e.Exception.Message}\n\n日志：{AppLog.LogPath}",
                "DataConfigEditor 错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLog.Error("Unhandled exception", ex);
        };

        var options = AppLaunchOptions.Parse(args);
        Application.Run(new MainForm(options));
    }
}
