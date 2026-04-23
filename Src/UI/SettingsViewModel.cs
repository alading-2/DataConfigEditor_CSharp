using DataConfigEditor.Settings;
using DataConfigEditor.Workspace;

namespace DataConfigEditor.UI;

public sealed class SettingsViewModel
{
    private readonly UiSettings _originalUiSettings;
    private readonly WorkspaceSettings _originalWorkspaceSettings;

    public SettingsViewModel(UiSettings uiSettings, WorkspaceSettings workspaceSettings)
    {
        _originalUiSettings = uiSettings.Normalize();
        _originalWorkspaceSettings = workspaceSettings;
        UiDraft = _originalUiSettings;
        WorkspaceDraft = _originalWorkspaceSettings;
    }

    public UiSettings UiDraft { get; set; }

    public WorkspaceSettings WorkspaceDraft { get; set; }

    public SettingsDialogResult Apply()
    {
        UiDraft = UiDraft.Normalize();
        return new SettingsDialogResult(UiDraft, WorkspaceDraft, ShouldClose: false);
    }

    public SettingsDialogResult Confirm()
    {
        UiDraft = UiDraft.Normalize();
        return new SettingsDialogResult(UiDraft, WorkspaceDraft, ShouldClose: true);
    }

    public SettingsDialogResult Cancel()
    {
        return new SettingsDialogResult(_originalUiSettings, _originalWorkspaceSettings, ShouldClose: true);
    }

    public void ResetToDefault()
    {
        UiDraft = UiSettings.Default.Normalize();
        WorkspaceDraft = WorkspaceSettings.Default;
    }
}

public sealed record SettingsDialogResult(
    UiSettings UiSettings,
    WorkspaceSettings WorkspaceSettings,
    bool ShouldClose);
