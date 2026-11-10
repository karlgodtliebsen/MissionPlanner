using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed partial class MotorTestViewModel
{
    private readonly SemaphoreSlim setupNavigation = new(1, 1);
    private int navigationVersion;
    private VehicleId? diagramVehicle;
    private readonly Dictionary<int, string> initialOutputs = [];
    private CancellationTokenSource? manualTestCancellation;
    private bool manualTestBusy;

    public MotorOrderViewModel? Wizard { get; }
    public Task SetupNavigationTask { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    public partial int SelectedPageTab { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWizardTab))]
    [NotifyPropertyChangedFor(nameof(IsDiagramInteractive))]
    [NotifyPropertyChangedFor(nameof(DiagramSelectCommand))]
    public partial int SelectedSetupTab { get; set; }

    [ObservableProperty]
    public partial bool IsChangingSetupMode { get; private set; }

    public bool IsWizardTab => SelectedPageTab == 1 && SelectedSetupTab == 2;
    public bool IsDiagramInteractive => SelectedPageTab == 1 && SelectedSetupTab is 0 or 2;
    public ICommand? DiagramSelectCommand => IsWizardTab ? Wizard?.SelectMotorCommand : SelectedSetupTab == 0 ? TestMotorCommand : null;

    partial void OnSelectedSetupTabChanged(int value) => QueueSetupNavigation();
    partial void OnSelectedPageTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsWizardTab));
        OnPropertyChanged(nameof(IsDiagramInteractive));
        OnPropertyChanged(nameof(DiagramSelectCommand));
        QueueSetupNavigation();
    }

    partial void OnIsMotorTestSafetyAcknowledgedChanged(bool value)
    {
        // Clearing the acknowledgement must cancel a repeating motor test immediately.
        if (!value || IsWizardTab) QueueSetupNavigation();
    }

    private void QueueSetupNavigation()
    {
        if (disposed || !activated) return;
        var version = ++navigationVersion;
        manualTestCancellation?.Cancel();
        Wizard?.RequestPause();
        IsChangingSetupMode = true;
        NotifySetupCommands();
        SetupNavigationTask = ChangeSetupModeAsync(version);
    }

    private async Task ChangeSetupModeAsync(int version)
    {
        await setupNavigation.WaitAsync();
        try
        {
            if (Wizard is not null) await Wizard.DeactivateAsync(endSession: true);
            if (thresholdAssistant?.HasSession == true && (SelectedSetupTab != 1 || SelectedPageTab != 1 || !IsMotorTestSafetyAcknowledged))
                await thresholdAssistant.CancelAsync();
            await StopActiveMotorTestAsync();
            if (version != navigationVersion || disposed) return;
            if (IsWizardTab && Wizard is not null) await Wizard.ActivateAsync(IsMotorTestSafetyAcknowledged);
        }
        catch (Exception exception) { SetMessages(errorMessage: exception.Message); }
        finally
        {
            setupNavigation.Release();
            if (version == navigationVersion)
            {
                IsChangingSetupMode = false;
                RefreshDiagram();
                RefreshThreshold();
                NotifySetupCommands();
            }
        }
    }

    private async Task StopSetupAsync(bool endSession)
    {
        ++navigationVersion;
        manualTestCancellation?.Cancel();
        Wizard?.RequestPause();
        await setupNavigation.WaitAsync();
        try
        {
            if (Wizard is not null) await Wizard.DeactivateAsync(endSession);
            await StopActiveMotorTestAsync();
        }
        finally { setupNavigation.Release(); }
    }

    private async Task StopActiveMotorTestAsync()
    {
        var snapshot = service.Current;
        if (snapshot?.State is not (MotorTestState.Running or MotorTestState.Stopped)) return;
        if (snapshot.State == MotorTestState.Running) await service.EmergencyStopAsync();
        if (snapshot.VehicleId is { } id && activeVehicle.VehicleId == id && activeVehicle.IsOnline)
            await MotorTestTelemetry.WaitUntilDisarmedAsync(activeVehicle, id, activeVehicle.ConnectionCancellationToken);
    }

    private void NotifySetupCommands()
    {
        TestMotorCommand.NotifyCanExecuteChanged();
        TestAllCommand.NotifyCanExecuteChanged();
        TestSequenceCommand.NotifyCanExecuteChanged();
        StartThresholdCommand.NotifyCanExecuteChanged();
        PulseThresholdCommand.NotifyCanExecuteChanged();
        SetMotorSpinArmCommand.NotifyCanExecuteChanged();
        SetMotorSpinMinCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private void WizardChanged()
    {
        if (disposed) return;
        RefreshDiagram();
        NotifySetupCommands();
    }

    private void ParameterChanged(VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId != activeVehicle.VehicleId || disposed) return;
        Dispatcher.Dispatch(Refresh);
    }

    private void RefreshDiagram()
    {
        var id = activeVehicle.VehicleId;
        if (id != diagramVehicle)
        {
            initialOutputs.Clear();
            diagramVehicle = id;
        }
        var pending = IsWizardTab ? Wizard?.PendingAssignments ?? [] : [];
        DiagramMotors = layout?.Motors.Select(motor =>
        {
            var output = id is { } vehicle ? (outputResolver ?? new MotorOutputResolver(parameters)).Resolve(vehicle, motor.MotorNumber) : null;
            var signature = output is null ? string.Empty : string.Join(",", output.OutputChannels);
            if (!initialOutputs.ContainsKey(motor.MotorNumber)) initialOutputs[motor.MotorNumber] = signature;
            var observation = pending.FirstOrDefault(item => item.ObservedMotor?.MotorNumber == motor.MotorNumber);
            return new MotorDiagramPoint(motor, output,
                observation?.OutputChannel,
                IsWizardTab && Wizard?.Finished != true ? observation is not null : initialOutputs[motor.MotorNumber] != signature,
                !IsWizardTab && service.Current?.State == MotorTestState.Running &&
                    service.Current.VehicleId == id &&
                    (service.Current.ActiveMotor is null || service.Current.ActiveMotor == motor.TestOrder),
                IsWizardTab && Wizard?.Finished != true);
        }).ToArray() ?? [];
    }
}
