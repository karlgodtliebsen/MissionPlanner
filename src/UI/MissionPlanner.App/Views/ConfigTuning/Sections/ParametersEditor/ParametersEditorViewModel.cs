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
    /// <summary>The owning window's overlay host for apply errors.</summary>
    public string? DialogHostId { get; set; }

    /// <summary>Gets or sets whether standalone editors show their local Clear and Apply buttons.</summary>
    [ObservableProperty]
    public partial bool ShowToolbar { get; set; } = true;

    private Func<CancellationToken, Task<string>>? applyModified;
    private readonly Dictionary<string, KeyValuePair<string, double>> currentParameters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Available names for parameter search.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> ParameterNames { get; private set; } = [];

    /// <summary>Name being searched for or selected from the suggestions.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddParameterCommand))]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Refreshes the confirmed vehicle values used by search and the read-only preview.</summary>
    public void SetCurrentParameters(IEnumerable<KeyValuePair<string, double>> parameters)
    {
        currentParameters.Clear();
        foreach (var parameter in parameters)
        {
            currentParameters[parameter.Key] = parameter;
        }
        var names = currentParameters.Values.Select(parameter => parameter.Key).Order(StringComparer.Ordinal).ToArray();
        if (!ParameterNames.SequenceEqual(names))
        {
            ParameterNames = names;
        }
        RefreshUsedParameters();
        AddParameterCommand.NotifyCanExecuteChanged();
    }

    private static IEnumerable<string> ReadParameterNames(string text)
    {
        foreach (var line in text.Replace('\r', '\n').Split('\n'))
        {
            var data = line.Split("//", 2, StringSplitOptions.None)[0];
            var name = data.Split(['=', ',', ':', ';'], 2)[0].Trim();
            if (name.Length > 0)
            {
                yield return name;
            }
        }
    }

    private static string FormatParameter(KeyValuePair<string, double> parameter)
    {
        return $"{parameter.Key} = {FormatValue(parameter.Value)}";
    }

    private void RefreshUsedParameters()
    {
        UsedParametersText = string.Join(Environment.NewLine,
            ReadParameterNames(Text).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(currentParameters.ContainsKey).Select(name => FormatParameter(currentParameters[name])));
    }

    private bool CanAddParameter()
    {
        return currentParameters.ContainsKey(SearchText?.Trim() ?? string.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanAddParameter))]
    private void AddParameter()
    {
        if (!CanAddParameter() || ApplyModifiedCommand.IsRunning)
        {
            return;
        }
        var parameter = currentParameters[SearchText.Trim()];
        if (pendingValues.TryGetValue(parameter.Key, out var pending))
        {
            parameter = new(parameter.Key, pending);
        }
        if (!ReadParameterNames(Text).Contains(parameter.Key, StringComparer.OrdinalIgnoreCase))
        {
            var separator = Text.Length == 0 || Text.EndsWith('\n') || Text.EndsWith('\r') ? string.Empty : Environment.NewLine;
            Text += separator + FormatParameter(parameter) + Environment.NewLine;
        }
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

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

    partial void OnTextChanged(string value)
    {
        ApplyModifiedCommand.NotifyCanExecuteChanged();
        RefreshUsedParameters();
        StageTextChanges();
    }

    private bool CanApplyModified()
    {
        return applyModified is not null && !string.IsNullOrWhiteSpace(Text) &&
            (editSession is null || canEditSession && editSession.IsValid);
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
            await dialogService.ConfirmAsync(options, exception.Message, cancellationToken, overLayHost: DialogHostId);
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
    /// Whenever the user enters a parameter value in Text, this property shows the parameter=value for the existing parameters.
    /// </summary>
    [ObservableProperty]
    public partial string UsedParametersText
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// 
    /// Gets or sets a value indicating whether the operation was applied.
    /// </summary>
    public bool UseParameters
    {
        get;
        set;
    }

    [RelayCommand]
    private void Clear()
    {
        Text = string.Empty;
    }

    /// <summary>
    /// Updates the parameters in the provided list based on the current Text property.
    /// </summary>
    /// <param name="fullParametersList">The list of vehicle parameters to update.</param>
    /// <returns>The recognized parameter values, without modifying the supplied list.</returns>
    public List<VehicleParameter> UpdateParameters(List<VehicleParameter> fullParametersList)
    {
        var parameters = fullParametersList.ToDictionary(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase);
        return ParseAssignments(parameters.Keys)
            .Select(item => parameters[item.Key] with { Value = (float)item.Value }).ToList();
    }

    private Dictionary<string, double> ParseAssignments(IEnumerable<string> parameterNames)
    {
        var names = parameterNames.ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);
        FeedbackReport = string.Empty;
        if (string.IsNullOrEmpty(Text))
        {
            return [];
        }

        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

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
            var name = parts[0].Trim();
            names.TryGetValue(name, out var canonicalName);
            // The last assignment wins, including an unfinished or invalid last line.
            if (canonicalName is not null)
            {
                result.Remove(canonicalName);
            }
            if (parts.Length != 2)
            {
                feedback.Add($"Line {lineNumber}: skipped invalid assignment: {line.Trim()}");
                continue;
            }

            if (canonicalName is not null)
            {
                var p = parts[1].Trim();
                if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ||
                    !double.IsFinite(v) || v > float.MaxValue || v < -float.MaxValue)
                {
                    feedback.Add($"Line {lineNumber}: skipped {name} — invalid numeric value: {p}");
                    continue;
                }

                result[canonicalName] = v;
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
