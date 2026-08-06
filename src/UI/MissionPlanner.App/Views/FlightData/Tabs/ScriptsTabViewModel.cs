using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.FlightData.Scripting;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Presents constrained declarative vehicle scripts.</summary>
public partial class ScriptsTabViewModel(IVehicleScriptParser parser, IVehicleScriptValidator validator,
    IVehicleScriptExecutor executor) : ObservableObject, IDisposable
{
    private CancellationTokenSource? execution;
    /// <summary>Gets or sets the versioned JSON source.</summary>
    [ObservableProperty] public partial string ScriptJson { get; set; } = """
        { "version": 1, "name": "Example", "steps": [ { "action": "notify", "arguments": { "message": "Ready" }, "timeoutSeconds": 5 } ] }
        """;
    /// <summary>Gets the current validation or execution summary.</summary>
    [ObservableProperty] public partial string Status { get; private set; } = "Validate a script before running it.";
    /// <summary>Gets the bounded ordered execution log.</summary>
    public ObservableCollection<string> Output { get; } = [];

    [RelayCommand] private void Validate() => ValidateCore();
    [RelayCommand] private Task DryRunAsync() => RunCoreAsync(true);
    [RelayCommand] private Task RunAsync() => RunCoreAsync(false);
    [RelayCommand] private void Cancel() => execution?.Cancel();

    private VehicleScriptDocument? ValidateCore()
    {
        try
        {
            var document = parser.Parse(ScriptJson);
            var result = validator.Validate(document);
            Status = result.IsValid ? $"Valid: {document.Steps.Count} step(s)." : string.Join(Environment.NewLine, result.Errors);
            return result.IsValid ? document : null;
        }
        catch (JsonException exception) { Status = $"Invalid JSON: {exception.Message}"; return null; }
    }

    private async Task RunCoreAsync(bool dryRun)
    {
        var document = ValidateCore();
        if (document is null) return;
        execution?.Dispose(); execution = new();
        try
        {
            var results = await executor.ExecuteAsync(document, dryRun, execution.Token);
            foreach (var result in results) { Output.Add($"{result.CompletedAt:HH:mm:ss} [{result.Index + 1}] {result.Action}: {result.Message}"); while (Output.Count > 200) Output.RemoveAt(0); }
            Status = results.All(item => item.Succeeded) ? (dryRun ? "Dry run succeeded." : "Script succeeded.") : "Script stopped on failure.";
        }
        catch (OperationCanceledException) { Status = "Script cancelled."; }
    }

    /// <inheritdoc />
    public void Dispose() { execution?.Cancel(); execution?.Dispose(); }
}
