using DataConfigEditor.Settings;
using DataConfigEditor.UI;
using DataConfigEditor.Workspace;

namespace DataConfigEditor.Tests.UI;

public class SettingsViewModelTests
{
    [Fact]
    public void Apply_ReturnsCurrentDraftWithoutClosing()
    {
        var model = new SettingsViewModel(UiSettings.Default, WorkspaceSettings.Default);
        model.UiDraft = model.UiDraft with
        {
            GridRowHeight = 36,
            MetadataAssemblyPath = "  /tmp/Game.dll ",
        };

        var result = model.Apply();

        Assert.False(result.ShouldClose);
        Assert.Equal(36, result.UiSettings.GridRowHeight);
        Assert.Equal("/tmp/Game.dll", result.UiSettings.MetadataAssemblyPath);
    }

    [Fact]
    public void Confirm_ReturnsCurrentDraftAndCloses()
    {
        var model = new SettingsViewModel(UiSettings.Default, WorkspaceSettings.Default);
        model.WorkspaceDraft = model.WorkspaceDraft with { ShowHiddenEntries = true };

        var result = model.Confirm();

        Assert.True(result.ShouldClose);
        Assert.True(result.WorkspaceSettings.ShowHiddenEntries);
    }

    [Fact]
    public void Cancel_ReturnsOriginalSettingsAndCloses()
    {
        var originalUi = UiSettings.Default with { GridRowHeight = 30 };
        var model = new SettingsViewModel(originalUi, WorkspaceSettings.Default);
        model.UiDraft = model.UiDraft with { GridRowHeight = 44 };

        var result = model.Cancel();

        Assert.True(result.ShouldClose);
        Assert.Equal(30, result.UiSettings.GridRowHeight);
    }

    [Fact]
    public void ResetToDefault_ReplacesDrafts()
    {
        var model = new SettingsViewModel(
            UiSettings.Default with { GridRowHeight = 44 },
            WorkspaceSettings.Default with { ShowHiddenEntries = true });

        model.ResetToDefault();

        Assert.Equal(UiSettings.Default.Normalize(), model.UiDraft);
        Assert.Equal(WorkspaceSettings.Default, model.WorkspaceDraft);
    }
}
