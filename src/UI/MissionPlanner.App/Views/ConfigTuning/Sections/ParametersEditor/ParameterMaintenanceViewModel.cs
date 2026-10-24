using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

/// <summary>Offers a backup-first, explicitly confirmed parameter reset with manual reboot guidance.</summary>
public sealed partial class ParameterMaintenanceViewModel(ParameterMaintenanceService maintenance, IFileSaveService files,
    IUserConfirmationService confirmation, ILogger<ParameterMaintenanceViewModel> logger) : ViewModelBase(logger)
{
    private CancellationTokenSource lifetime = new();

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task PrepareResetAsync(CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true;
        var attempted = false;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var snapshot = maintenance.Capture();
            using var connected = CancellationTokenSource.CreateLinkedTokenSource(linked.Token, snapshot.Connection);
            var text = "# MissionPlanner Next Gen complete live parameter backup\n" + string.Join('\n', snapshot.Parameters.Select(p => p.Name + "," + p.Value.ToString("R", CultureInfo.InvariantCulture))) + "\n";
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
            if (await files.SaveNewAsync("parameters-before-reset.param", content, connected.Token) is null) { SetMessages("Reset cancelled because the backup was not saved."); return; }
            if (!await confirmation.ConfirmPhraseAsync("Reset all vehicle parameters", $"A live backup of {snapshot.Parameters.Count} parameters was saved. Prepare reset on vehicle {snapshot.Session.Id} by writing FORMAT_VERSION=0? At the next reboot this removes calibration, tuning, output assignments and the DroneCAN dynamic address table. Pending editor changes are not backed up or applied. Complete setup again before flight. The application will not reboot automatically.", "RESET ALL PARAMETERS", connected.Token)) return;
            attempted = true;
            await maintenance.PrepareResetAsync(snapshot, true, connected.Token);
            SetMessages("FORMAT_VERSION=0 readback confirmed. Disconnect, power-cycle the secured controller, then reconnect and download all parameters. Redo setup and calibrations before flight. Do not apply old pending edits; discard them and refresh after reconnecting.");
        }
        catch (OperationCanceledException) { SetMessages(attempted ? "Reset preparation cancelled or timed out after sending may have begun. Check FORMAT_VERSION before rebooting; do not assume it was unchanged." : "Reset cancelled; no reset request was made."); }
        catch (Exception ex) { SetMessages(ex.Message + (attempted ? " Check FORMAT_VERSION before rebooting: preparation may already have been stored." : "")); }
        finally { IsBusy = false; }
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        return base.ActivateAsync();
    }
    /// <inheritdoc />
    public override Task DeactivateAsync() { lifetime.Cancel(); return base.DeactivateAsync(); }
    /// <inheritdoc />
    public override void Dispose() { lifetime.Cancel(); base.Dispose(); }
}
