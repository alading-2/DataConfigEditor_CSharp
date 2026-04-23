using DataConfigEditor.Documents;
using DataConfigEditor.Presentation;

namespace DataConfigEditor.Tests.Presentation;

public class WorkspacePresenterTests
{
    [Fact]
    public void ShowWelcome_UsesRecentDirectories()
    {
        var presenter = new WorkspacePresenter();

        var state = presenter.ShowWelcome(new[] { @"E:\One", @"E:\Two" });

        Assert.Equal(WorkspaceViewKind.Welcome, state.Kind);
        Assert.Equal(2, state.RecentDirectories.Count);
    }

    [Fact]
    public void ShowDocumentError_UsesDiagnosticView()
    {
        var presenter = new WorkspacePresenter();
        var document = TableDocument.Error("A.cs", "A", "无法转换为表格视图");

        var state = presenter.ShowDocument(document);

        Assert.Equal(WorkspaceViewKind.Message, state.Kind);
        Assert.Equal("无法转换为表格视图", state.Message);
    }
}
