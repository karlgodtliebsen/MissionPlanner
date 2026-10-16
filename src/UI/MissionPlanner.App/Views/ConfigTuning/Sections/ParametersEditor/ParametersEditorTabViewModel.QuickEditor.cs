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

        // Text entered for a previous session must not be written to another vehicle.
        if (!ReferenceEquals(quickEditorSession, EditSession))
        {
            quickEditorSession = EditSession;
            quickEditor.ClearCommand.Execute(null);
            quickEditor.SearchText = string.Empty;
        }
        quickEditor.DialogHostId = DialogHostId;
        quickEditor.SetCurrentParameters(EditSession is { IsValid: true } session && HasConnection
            ? session.Fields.Select(field => new KeyValuePair<string, double>(field.Name, field.LiveValue))
            : []);
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

        var parameters = viewModel.UpdateParameters(editorSession.Fields.Select(ToVehicleParameter).ToList());
        if (parameters.Count == 0)
        {
            return "No valid parameter assignments were found. No values were written. See the feedback report.";
        }

        var errors = new List<string>();
        var acceptedNames = new List<string>();
        var previousValues = editorSession.Fields.ToDictionary(field => field.Name, field => field.PendingValue);
        using (editorSession.DeferChangeNotifications())
        {
            foreach (var parameter in parameters)
            {
                if (!editorSession.TrySetPending(parameter.Name, parameter.Value, out var error))
                {
                    errors.Add($"Skipped {parameter.Name}: {error}");
                    editorSession.TrySetPending(parameter.Name, previousValues[parameter.Name], out _);
                }
                else
                {
                    acceptedNames.Add(parameter.Name);
                }
            }
        }
        viewModel.ReportSkippedParameters(errors);
        if (acceptedNames.Count == 0)
        {
            return "No valid parameter values could be staged. No values were written. See the feedback report.";
        }

        await ApplyParameterChangesAsync(cancellationToken, acceptedNames);
        return ErrorMessage ?? StatusMessage ?? "Parameter write finished.";
    }
}
