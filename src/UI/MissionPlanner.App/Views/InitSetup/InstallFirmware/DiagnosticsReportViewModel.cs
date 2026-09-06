using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Owns diagnostics panel state and commands.</summary>
public sealed partial class DiagnosticsReportViewModel : DialogViewModelBase
{
    private readonly ITextClipboardService clipboard;
    /// <summary>Initializes the diagnostics panel.</summary>
    public DiagnosticsReportViewModel(
        string report,
        string message,
        ITextClipboardService clipboard,
        ILogger<DiagnosticsReportViewModel> logger) //: base(logger, dispatcher, eventHub)
    {
        this.clipboard = clipboard;
        this.LastDiagnosticReport = report;
        this.StatusMessage = message;
    }
    [ObservableProperty]
    public partial string? LastDiagnosticReport
    {
        get;
        set;
    }


    /// <summary>Gets whether a terminal diagnostic report can be copied.</summary>
    public bool HasDiagnosticReport => !string.IsNullOrWhiteSpace(LastDiagnosticReport);
    public new bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    [RelayCommand]
    private Task CopyDiagnosticReportAsync()
    {
        return string.IsNullOrWhiteSpace(LastDiagnosticReport)
            ? Task.CompletedTask
            : clipboard.SetTextAsync(LastDiagnosticReport);
    }
    //  partial void OnLastDiagnosticReportChanged(string? value) => OnPropertyChanged(nameof(HasDiagnosticReport));

}
