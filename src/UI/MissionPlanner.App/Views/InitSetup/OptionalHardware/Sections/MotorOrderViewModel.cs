using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Inline motor identification with cancellable bursts and staged assignments.</summary>
public sealed partial class MotorOrderViewModel : ViewModelBase
{
    private readonly MotorOrderService service;
    private CancellationTokenSource? bursts;
    private CancellationTokenSource? writeLifetime;
    private Task burstTask = Task.CompletedTask;
    private bool active;
    private bool safetyAcknowledged;
    private bool disposed;
    private int interactionVersion;

    public MotorOrderViewModel(MotorOrderService service, int throttlePercent)
    {
        this.service = service;
        ThrottlePercent = Math.Clamp(throttlePercent, 1, 20);
        service.ObservationReady += ObservationReady;
    }

    public event Action? Changed;
    public IReadOnlyList<MotorOrderAssignment> PendingAssignments => !Finished && service.HasSession ? service.Assignments : [];
    public bool HasSession => service.HasSession;
    public bool CollectingOrder => !Finished && !NeedsRepair;
    public Task BurstsCompletion => burstTask;

    [ObservableProperty] public partial string Instruction { get; private set; } = "Confirm propeller removal to begin identifying the motors.";
    [ObservableProperty] public partial string StepDisplay { get; private set; } = string.Empty;
    [ObservableProperty] public partial string Review { get; private set; } = string.Empty;
    [ObservableProperty] public partial IReadOnlyList<int> SelectedMotors { get; private set; } = [];
    [ObservableProperty] public partial bool OperationBusy { get; private set; }
    [ObservableProperty] public partial bool IsPulsing { get; private set; }
    [ObservableProperty] public partial bool IsComplete { get; private set; }
    [ObservableProperty] public partial bool NeedsRepair { get; private set; }
    [ObservableProperty] public partial bool Finished { get; private set; }
    [ObservableProperty] public partial int ThrottlePercent { get; set; } = 10;

    private void Refresh()
    {
        NeedsRepair = service.HasSession && service.NeedsRepair;
        IsComplete = service.IsComplete && (service.HasSession || Finished);
        SelectedMotors = PendingAssignments.Where(item => item.ObservedMotor is not null)
            .Select(item => item.ObservedMotor!.MotorNumber).ToArray();
        StepDisplay = IsComplete ? "All motors identified" : service.HasSession
            ? $"Motor {SelectedMotors.Count + 1} of {service.Assignments.Count} · Output {service.Current?.OutputChannel}" : string.Empty;
        Review = NeedsRepair || IsComplete
            ? string.Join("\n", service.Assignments.Select(item => $"{item.ParameterName} = {item.Function}")) : string.Empty;
        OnPropertyChanged(nameof(CollectingOrder));
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        SelectMotorCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        RepairCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
        Changed?.Invoke();
    }

    private void ObservationReady() => Dispatcher.Dispatch(() => { if (!disposed) Refresh(); });

    public async Task ActivateAsync(bool propsRemoved)
    {
        active = true;
        safetyAcknowledged = propsRemoved;
        if (!propsRemoved)
        {
            Instruction = "Confirm that all propellers are removed using the checkbox above.";
            Refresh();
            return;
        }
        if (!Finished) await StartAsync();
        else Refresh();
    }

    private bool CanStart() => active && safetyAcknowledged && !OperationBusy && !IsPulsing && !Finished && !IsComplete && !NeedsRepair;
    private bool CanSelect(MotorLayoutMotor? motor) => active && safetyAcknowledged && !OperationBusy && !Finished &&
        service.HasSession && service.CanObserve && motor is not null && !SelectedMotors.Contains(motor.MotorNumber);
    private bool CanSave() => active && safetyAcknowledged && !OperationBusy && IsComplete && !Finished && service.HasSession;
    private bool CanRepair() => active && safetyAcknowledged && !OperationBusy && NeedsRepair && !Finished && service.HasSession;
    private bool CanRestart() => active && safetyAcknowledged && !OperationBusy;
    private bool CanStop() => IsPulsing || OperationBusy;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (!CanStart()) return;
        var version = interactionVersion;
        OperationBusy = true;
        Refresh();
        try
        {
            await PauseBurstsAsync();
            if (!active || !safetyAcknowledged || version != interactionVersion) return;
            if (!service.HasSession) service.Start(safetyAcknowledged);
            Refresh();
            if (NeedsRepair)
            {
                Instruction = "Duplicate or missing motor functions prevent individual testing. Repair saves the unique assignments below immediately. Reboot and return to this tab afterward.";
                return;
            }
            if (!IsComplete) BeginBursts();
        }
        catch (Exception exception) { Instruction = exception.Message; Refresh(); }
        finally { OperationBusy = false; Refresh(); }
    }

    private void BeginBursts()
    {
        if (!active || !safetyAcknowledged || disposed || !service.HasSession || service.IsComplete || service.NeedsRepair) return;
        bursts = new CancellationTokenSource();
        IsPulsing = true;
        Instruction = "Click the motor that is pulsing in the diagram above. Short bursts repeat until you select a motor or leave this tab.";
        Refresh();
        burstTask = RepeatBurstsAsync(bursts.Token);
    }

    private async Task RepeatBurstsAsync(CancellationToken token)
    {
        try
        {
            while (active && safetyAcknowledged && !token.IsCancellationRequested)
            {
                await service.PulseAsync(ThrottlePercent, token);
                Refresh();
                await Task.Delay(TimeSpan.FromMilliseconds(800), token);
            }
        }
        catch (OperationCanceledException)
        {
            if (!token.IsCancellationRequested) Instruction = "Motor pulses stopped because the vehicle connection changed. Start again after reconnecting and disarming.";
        }
        catch (Exception exception) { Instruction = exception.Message; }
        finally { IsPulsing = false; Refresh(); }
    }

    /// <summary>Prevents another burst immediately, before asynchronous navigation cleanup.</summary>
    public void RequestPause()
    {
        ++interactionVersion;
        active = false;
        bursts?.Cancel();
        writeLifetime?.Cancel();
        Refresh();
    }

    private async Task PauseBurstsAsync()
    {
        bursts?.Cancel();
        await burstTask;
        bursts?.Dispose();
        bursts = null;
    }

    public async Task DeactivateAsync(bool endSession = false)
    {
        RequestPause();
        await PauseBurstsAsync();
        if (endSession || writeLifetime is not null) await service.CancelAsync();
        if (endSession)
        {
            Finished = false;
            Instruction = service.LastWriteMessage ?? "Wizard stopped. Unsaved motor-order selections were discarded.";
        }
        else if (!Finished) Instruction = "Pulses paused. Your selections are retained and have not been saved.";
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        ++interactionVersion;
        writeLifetime?.Cancel();
        await PauseBurstsAsync();
        Instruction = "Pulses stopped. Select the observed motor, resume bursts, or restart.";
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanSelect))]
    private async Task SelectMotorAsync(MotorLayoutMotor motor)
    {
        if (!CanSelect(motor)) return;
        var version = interactionVersion;
        OperationBusy = true;
        Refresh();
        try
        {
            await PauseBurstsAsync();
            if (!active || !safetyAcknowledged || version != interactionVersion) return;
            service.Observe(motor.MotorNumber);
            Refresh();
            if (service.IsComplete)
                Instruction = "All motors identified. The orange assignments are a preview. Save motor order to write them to the controller.";
            else BeginBursts();
        }
        catch (Exception exception) { Instruction = exception.Message; }
        finally { OperationBusy = false; Refresh(); }
    }

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private async Task RestartAsync()
    {
        if (!CanRestart()) return;
        var version = interactionVersion;
        OperationBusy = true;
        Refresh();
        try
        {
            await PauseBurstsAsync();
            if (!active || !safetyAcknowledged || version != interactionVersion) return;
            if (service.HasSession) service.Restart();
            else service.Start(safetyAcknowledged);
            Finished = false;
            Refresh();
            if (!NeedsRepair) BeginBursts();
        }
        catch (Exception exception) { Instruction = exception.Message; Refresh(); }
        finally { OperationBusy = false; Refresh(); }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => CanSave() ? WriteAsync(false) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanRepair))]
    private Task RepairAsync() => CanRepair() ? WriteAsync(true) : Task.CompletedTask;

    private async Task WriteAsync(bool repair)
    {
        OperationBusy = true;
        writeLifetime = new CancellationTokenSource();
        Refresh();
        try
        {
            await PauseBurstsAsync();
            var result = await service.SaveAsync(repair, writeLifetime.Token);
            Instruction = result.Message;
            Finished = result.Success || !service.HasSession;
        }
        catch (Exception exception) { Instruction = exception.Message; }
        finally
        {
            writeLifetime.Dispose();
            writeLifetime = null;
            OperationBusy = false;
            Refresh();
        }
    }

    public override void Dispose()
    {
        disposed = true;
        active = false;
        bursts?.Cancel();
        writeLifetime?.Cancel();
        service.ObservationReady -= ObservationReady;
        base.Dispose();
    }
}
