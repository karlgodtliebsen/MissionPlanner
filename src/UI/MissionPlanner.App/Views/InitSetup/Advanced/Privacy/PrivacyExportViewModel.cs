using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup.Advanced.Privacy;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Privacy;

/// <summary>Creates a separate reduced diagnostic CSV using the documented privacy policy.</summary>
public sealed partial class PrivacyExportViewModel(DiagnosticPrivacyExport exporter, IFileOpenService open,
    IFileSaveService save, ILogger<PrivacyExportViewModel> logger) : ViewModelBase(logger)
{
    private CancellationTokenSource lifetime = new();

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            using var selected = await open.OpenAsync("Select log for reduced privacy export", ["*.tlog", "*.bin", "*.log"], linked.Token);
            if (selected is null)
            {
                return;
            }

            using var output = new MemoryStream();
            var result = await Task.Run(() => exporter.ExportAsync(selected.Content, Path.GetExtension(selected.FileName), output, linked.Token), linked.Token);
            if (result.Rows == 0)
            {
                SetMessages("No approved diagnostic fields were found. No export created.");
                return;
            }
            output.Position = 0;
            // The generic name deliberately avoids source paths, vehicle identity and capture dates.
            var path = await save.SaveNewAsync("diagnostics-privacy-v1.csv", output, linked.Token);
            SetMessages(path is null ? "Save cancelled." : $"Exported {result.Rows:N0} numeric rows. {result.ExcludedRecords:N0} of {result.ExaminedRecords:N0} decoded records were excluded. Review the CSV before sharing; signal patterns may still be identifying.");
        }
        catch (OperationCanceledException) { SetMessages("Privacy export cancelled."); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; }
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (lifetime.IsCancellationRequested)
        {
            lifetime.Dispose();
            lifetime = new();
        }
        return base.ActivateAsync();
    }
    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        lifetime.Cancel();
        return base.DeactivateAsync();
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        lifetime.Cancel();
        base.Dispose();
    }
}
