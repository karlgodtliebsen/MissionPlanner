using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

/// <summary>
/// ViewModel for editing vehicle parameters in a text format.
/// It allows users to input parameter values and updates the corresponding parameters in a provided list.
/// </summary>
/// <param name="dialogService"></param>
public partial class ParametersEditorViewModel(IDialogService dialogService) : ViewModelBase
{
    private Func<CancellationToken, Task<string>>? applyModified;

    /// <summary>Gets or sets live progress from the confirmed parameter-write workflow.</summary>
    [ObservableProperty]
    public partial string? WriteProgress
    {
        get; set;
    }

    /// <summary>Gets the report of input lines or values skipped during the last import.</summary>
    [ObservableProperty]
    public partial string FeedbackReport { get; private set; } = string.Empty;

    /// <summary>Adds metadata validation results to the input report.</summary>
    public void ReportSkippedParameters(IEnumerable<string> messages)
    {
        var report = string.Join(Environment.NewLine, messages);
        if (!string.IsNullOrEmpty(report))
        {
            FeedbackReport = string.IsNullOrEmpty(FeedbackReport) ? report : FeedbackReport + Environment.NewLine + report;
        }
    }

    /// <summary>Connects the editor to its owning vehicle's confirmed parameter-write workflow.</summary>
    public void ConfigureApply(Func<CancellationToken, Task<string>> apply)
    {
        applyModified = apply;
        ApplyModifiedCommand.NotifyCanExecuteChanged();
    }

    partial void OnTextChanged(string value) => ApplyModifiedCommand.NotifyCanExecuteChanged();

    private bool CanApplyModified()
    {
        return applyModified is not null && !string.IsNullOrWhiteSpace(Text);
    }

    [RelayCommand(CanExecute = nameof(CanApplyModified))]
    private async Task ApplyModifiedAsync(CancellationToken cancellationToken)
    {
        if (!CanApplyModified())
        {
            return;
        }

        try
        {
            WriteProgress = null;
            SetMessages("Applying modified parameters...");
            SetMessages(await applyModified!(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            SetMessages("Parameter apply cancelled.");
        }
        catch (Exception exception)
        {
            SetMessages(errorMessage: exception.Message);
            var options = dialogService.CreateOptions("Parameters could not be applied", "OK", null);
            await dialogService.ConfirmAsync(options, exception.Message, cancellationToken);
        }
        finally
        {
            WriteProgress = null;
        }
    }
    /// <summary>
    /// Gets or sets the text input by the user, which contains parameter values in a specific format. This property is bound to the view and is used to update the parameters in the provided list.
    /// </summary>
    [ObservableProperty]
    public partial string Text
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the operation was applied.
    /// </summary>
    public bool UseParameters
    {
        get;
        set;
    }

    ///// <summary>
    ///// Gets or sets a value indicating whether the operation was cancelled.
    ///// </summary>
    //public bool Cancelled
    //{
    //    get;
    //    set;
    //}

    //[RelayCommand]
    //private Task CancelAsync(CancellationToken cancellationToken)
    //{
    //    Cancelled = true;
    //    return dialogService.CloseAsync(cancellationToken);
    //}

    //[RelayCommand]
    //private Task UseParametersAsync(CancellationToken cancellationToken)
    //{
    //    UseParameters = true;
    //    useCallback(this);
    //    return dialogService.CloseAsync(cancellationToken);
    //}

    [RelayCommand]
    private void Clear()
    {
        Text = string.Empty;
    }

    //[RelayCommand]
    //private Task WriteParametersAsync(CancellationToken cancellationToken)
    //{
    //    applyCallback(this);
    //    return dialogService.CloseAsync(cancellationToken);
    //}

    /// <summary>
    /// Updates the parameters in the provided list based on the current Text property.
    /// </summary>
    /// <param name="fullParametersList">The list of vehicle parameters to update.</param>
    /// <returns>The number of parameters that were updated.</returns>
    public List<VehicleParameter> UpdateParameters(List<VehicleParameter> fullParametersList)
    {
        FeedbackReport = string.Empty;
        if (string.IsNullOrEmpty(Text))
        {
            return [];
        }

        var result = new List<VehicleParameter>();

        //accepted format
        //FRAME_CLASS=1//Quad
        //FRAME_CLASS,1//Quad
        //FRAME_CLASS:1//Quad
        //FRAME_CLASS;1//Quad

        var lines = Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var feedback = new List<string>();
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            var data = line.Trim();
            if (data.Contains("//"))
            {
                data = data.Substring(0, data.IndexOf("//", StringComparison.Ordinal));
            }
            if (string.IsNullOrWhiteSpace(data))
            {
                continue;
            }

            var parts = data.Split(['=', ',', ':', ';']);
            if (parts.Length != 2)
            {
                feedback.Add($"Line {lineNumber}: skipped invalid assignment: {line.Trim()}");
                continue;
            }

            var name = parts[0].Trim();
            var parameter = fullParametersList.FirstOrDefault(p => p.Name == name);
            if (parameter is not null)
            {
                var p = parts[1].Trim();
                if (!float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !float.IsFinite(v))
                {
                    feedback.Add($"Line {lineNumber}: skipped {name} — invalid numeric value: {p}");
                    continue;
                }

                var param = parameter with
                {
                    Value = v
                };
                result.RemoveAll(item => item.Name == name);
                result.Add(param);
            }
            else
            {
                feedback.Add($"Line {lineNumber}: skipped unknown parameter: {name}");
            }
        }

        FeedbackReport = $"Recognized {result.Count} parameter value(s); skipped {feedback.Count} input line(s).";
        ReportSkippedParameters(feedback);
        return result;
    }
}
