using MissionPlanner.Core.ConfigTuning;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

public partial class ParametersEditorTabViewModel
{
    private ParametersEditorViewModel? quickEditor;
    private IParameterEditSession? quickEditorSession;
    private CancellationTokenSource quickEditorLifetime = new();

    /// <summary>Gets the text editor shared by the Quick Editor tab and its toolbar.</summary>
    public ParametersEditorViewModel QuickEditor
    {
        get
        {
            if (quickEditor is null)
            {
                quickEditor = domainFactory.Create<ParametersEditorViewModel>();
                quickEditor.ShowToolbar = false;
                quickEditor.ConfigureApply(async cancellationToken =>
                {
                    using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, quickEditorLifetime.Token);
                    lifetime.Token.ThrowIfCancellationRequested();
                    if (!pageActive || quickEditorSession is null)
                    {
                        throw new InvalidOperationException("Load vehicle parameters before applying Quick Editor values.");
                    }
                    quickEditor.DialogHostId = DialogHostId;
                    return await ApplyQuickEditorChangesAsync(quickEditor, quickEditorSession, lifetime.Token);
                });
                RefreshQuickEditor();
            }
            return quickEditor;
        }
    }

    private void RefreshQuickEditor()
    {
        if (quickEditor is null)
        {
            return;
        }

        quickEditorSession = EditSession;
        quickEditor.DialogHostId = DialogHostId;
        quickEditor.SetEditSession(EditSession, pageActive && HasConnection && !IsBusy);
    }

    private async Task<string> ApplyQuickEditorChangesAsync(
        ParametersEditorViewModel viewModel,
        IParameterEditSession editorSession,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(EditSession, editorSession) || !editorSession.IsValid || !HasConnection || IsBusy)
        {
            throw new InvalidOperationException("The original vehicle connection is no longer available or is busy. Reopen the editor after refreshing parameters.");
        }

        var acceptedNames = viewModel.StageTextChanges()
            .Where(name => editorSession.GetField(name) is { IsModified: true, IsValid: true })
            .ToArray();
        if (acceptedNames.Length == 0)
        {
            return "No valid modified parameters were found in the text. No values were written. See the feedback report.";
        }

        await ApplyParameterChangesAsync(cancellationToken, acceptedNames);
        return ErrorMessage ?? StatusMessage ?? "Parameter write finished.";
    }
}
