using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects battery monitor discovery, live readings, calibration, and failsafe editing into Setup controls.</summary>
public sealed partial class BatterySetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IBatteryConfigurationService batteryService;
    private readonly IDomainEventHub domainEventHub;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<BatterySetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;
    private IDisposable? vehicleStateSubscription;
    private VehiclePowerState? observedPower;

    /// <summary>Initializes the battery Setup workflow.</summary>
    /// <param name="descriptor">The battery workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="batteryService">The battery configuration service.</param>
    /// <param name="domainEventHub">The domain event hub used for live battery state.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public BatterySetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        IBatteryConfigurationService batteryService,
        IDomainEventHub domainEventHub,
        IDispatcher dispatcher,
        ILogger<BatterySetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.batteryService = batteryService;
        this.domainEventHub = domainEventHub;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>Gets the discovered battery instances.</summary>
    public ObservableCollection<BatteryInstanceViewModel> Instances { get; } = [];

    /// <summary>Gets configuration issues.</summary>
    public ObservableCollection<string> Issues { get; } = [];

    /// <summary>Gets the workflow status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Load the connected vehicle's battery configuration.";

    /// <summary>Gets whether at least one battery instance was discovered.</summary>
    public bool HasInstances => Instances.Count > 0;

    /// <summary>Gets whether any configuration issues exist.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Loads the battery configuration for the active vehicle.</summary>
    /// <returns>A task that completes after the configuration is projected.</returns>
    public async Task LoadAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before loading battery configuration.";
            return;
        }

        var token = StartOperation();
        try
        {
            var configuration = await batteryService.GetConfigurationAsync(vehicleId, token);
            dispatcher.Dispatch(() => Show(configuration));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Loading battery configuration failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    protected override void OnActivated()
    {
        activeVehicle.Changed += OnActiveVehicleChanged;
        observedPower = activeVehicle.State?.Power;
        vehicleStateSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
        _ = LoadAsync();
    }

    /// <inheritdoc />
    protected override void OnDeactivated()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        vehicleStateSubscription?.Dispose();
        vehicleStateSubscription = null;
        observedPower = null;
        base.OnDeactivated();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        base.Dispose();
    }

    /// <summary>Writes a single battery setting and reloads on success.</summary>
    /// <param name="instance">The battery instance index.</param>
    /// <param name="setting">The setting to write.</param>
    /// <param name="value">The value to write.</param>
    /// <returns>A task that completes after the write is confirmed or reported failed.</returns>
    internal async Task ApplyAsync(int instance, BatterySetting setting, double value)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before editing battery settings.";
            return;
        }

        var token = StartOperation();
        try
        {
            var result = await batteryService.SetValueAsync(vehicleId, instance, setting, value, token);
            Status = result.RequiresReboot ? $"{result.Message} Reboot required." : result.Message;
            if (result.Success)
            {
                await ReloadAsync(vehicleId, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Applying battery setting failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    /// <summary>Calibrates voltage or current using the live reading and an external reference.</summary>
    /// <param name="instance">The battery instance index.</param>
    /// <param name="current">Whether to calibrate current instead of voltage.</param>
    /// <param name="measured">The live measured value.</param>
    /// <param name="reference">The externally measured reference value.</param>
    /// <returns>A task that completes after the calibration write.</returns>
    internal async Task CalibrateAsync(int instance, bool current, double measured, double reference)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before calibrating.";
            return;
        }

        var token = StartOperation();
        try
        {
            var result = current
                ? await batteryService.CalibrateCurrentAsync(vehicleId, instance, measured, reference, token)
                : await batteryService.CalibrateVoltageAsync(vehicleId, instance, measured, reference, token);
            Status = result.Message;
            if (result.Success)
            {
                await ReloadAsync(vehicleId, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Battery calibration failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return RefreshCoreAsync();
    }

    private async Task RefreshCoreAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            return;
        }

        var token = StartOperation();
        try
        {
            await batteryService.RefreshAsync(vehicleId, token);
            var configuration = await batteryService.GetConfigurationAsync(vehicleId, token);
            dispatcher.Dispatch(() => Show(configuration));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Refreshing battery configuration failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    private async Task ReloadAsync(VehicleId vehicleId, CancellationToken token)
    {
        var configuration = await batteryService.GetConfigurationAsync(vehicleId, token);
        dispatcher.Dispatch(() => Show(configuration, true));
    }

    private CancellationToken StartOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        Error = null;
        return operationCancellation.Token;
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(() =>
        {
            if (IsActive)
            {
                observedPower = args.Current.State?.Power;
                _ = LoadAsync();
            }
        });
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId && evt.VehicleState.Power != observedPower)
        {
            dispatcher.Dispatch(() =>
            {
                if (IsActive &&
                    evt.VehicleId == activeVehicle.VehicleId &&
                    evt.VehicleState.Power != observedPower)
                {
                    observedPower = evt.VehicleState.Power;
                    RefreshLiveReadings();
                }
            });
        }

        return Task.CompletedTask;
    }

    private void RefreshLiveReadings()
    {
        if (!IsActive || activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline || Instances.Count == 0)
        {
            return;
        }

        _ = UpdateLiveAsync(vehicleId);
    }

    private async Task UpdateLiveAsync(VehicleId vehicleId)
    {
        try
        {
            var configuration = await batteryService.GetConfigurationAsync(vehicleId, activeVehicle.ConnectionCancellationToken);
            dispatcher.Dispatch(() =>
            {
                foreach (var instance in configuration.Instances)
                {
                    Instances.FirstOrDefault(item => item.Index == instance.Index)?.UpdateLive(instance.Live);
                }
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogDebug(exception, "Live battery refresh failed for {VehicleId}.", vehicleId);
        }
    }

    private void Show(BatteryConfiguration configuration, bool preserveStatus = false)
    {
        Instances.Clear();
        foreach (var instance in configuration.Instances)
        {
            Instances.Add(new BatteryInstanceViewModel(instance, configuration, this));
        }

        Issues.Clear();
        foreach (var issue in configuration.Issues)
        {
            Issues.Add($"[{issue.Severity}] {issue.Message}");
        }

        if (!preserveStatus)
        {
            Status = Instances.Count == 0
                ? "No battery monitors were detected. Enable BATT_MONITOR and refresh."
                : "Review battery monitors, live readings, and failsafe thresholds.";
        }

        OnPropertyChanged(nameof(HasInstances));
        OnPropertyChanged(nameof(HasIssues));
    }
}

/// <summary>Presents one battery monitor instance with live readings, editing, and calibration.</summary>
public sealed partial class BatteryInstanceViewModel : ObservableObject
{
    private readonly BatterySetupViewModel parent;
    private double liveVoltage;
    private double liveCurrent;

    /// <summary>Initializes a battery instance row.</summary>
    /// <param name="instance">The discovered instance.</param>
    /// <param name="configuration">The owning configuration projection.</param>
    /// <param name="parent">The owning battery workflow.</param>
    public BatteryInstanceViewModel(BatteryMonitorInstance instance, BatteryConfiguration configuration, BatterySetupViewModel parent)
    {
        this.parent = parent;
        Index = instance.Index;
        MonitorName = instance.MonitorName;
        MonitorOptions = configuration.MonitorOptions;
        LowActionOptions = configuration.LowActionOptions;
        CriticalActionOptions = configuration.CriticalActionOptions;
        Capacity = instance.Get(BatterySetting.Capacity) ?? 0;
        LowVoltage = instance.Get(BatterySetting.LowVoltage) ?? 0;
        CriticalVoltage = instance.Get(BatterySetting.CriticalVoltage) ?? 0;
        LowCapacity = instance.Get(BatterySetting.LowCapacity) ?? 0;
        CriticalCapacity = instance.Get(BatterySetting.CriticalCapacity) ?? 0;
        SupportsCapacity = instance.Get(BatterySetting.Capacity) is not null;
        SupportsVoltageFailsafe = instance.Get(BatterySetting.LowVoltage) is not null;
        SupportsCapacityFailsafe = instance.Get(BatterySetting.LowCapacity) is not null;
        SupportsVoltageCalibration = instance.Get(BatterySetting.VoltageMultiplier) is not null;
        SupportsCurrentCalibration = instance.Get(BatterySetting.CurrentPerVolt) is not null;
        UpdateLive(instance.Live);
    }

    /// <summary>Gets the one-based instance index.</summary>
    public int Index { get; }

    /// <summary>Gets the monitor backend name.</summary>
    public string MonitorName { get; }

    /// <summary>Gets the monitor backend options.</summary>
    public IReadOnlyList<BatterySettingOption> MonitorOptions { get; }

    /// <summary>Gets the low-failsafe action options.</summary>
    public IReadOnlyList<BatterySettingOption> LowActionOptions { get; }

    /// <summary>Gets the critical-failsafe action options.</summary>
    public IReadOnlyList<BatterySettingOption> CriticalActionOptions { get; }

    /// <summary>Gets the instance header.</summary>
    public string Header => $"Battery {Index} · {MonitorName}";

    /// <summary>Gets whether capacity is configurable.</summary>
    public bool SupportsCapacity { get; }

    /// <summary>Gets whether voltage failsafe thresholds are configurable.</summary>
    public bool SupportsVoltageFailsafe { get; }

    /// <summary>Gets whether capacity failsafe thresholds are configurable.</summary>
    public bool SupportsCapacityFailsafe { get; }

    /// <summary>Gets whether voltage calibration is supported.</summary>
    public bool SupportsVoltageCalibration { get; }

    /// <summary>Gets whether current calibration is supported.</summary>
    public bool SupportsCurrentCalibration { get; }

    /// <summary>Gets or sets the pack capacity in milliampere-hours.</summary>
    [ObservableProperty]
    public partial double Capacity { get; set; }

    /// <summary>Gets or sets the low-voltage failsafe threshold.</summary>
    [ObservableProperty]
    public partial double LowVoltage { get; set; }

    /// <summary>Gets or sets the critical-voltage failsafe threshold.</summary>
    [ObservableProperty]
    public partial double CriticalVoltage { get; set; }

    /// <summary>Gets or sets the low-capacity failsafe threshold.</summary>
    [ObservableProperty]
    public partial double LowCapacity { get; set; }

    /// <summary>Gets or sets the critical-capacity failsafe threshold.</summary>
    [ObservableProperty]
    public partial double CriticalCapacity { get; set; }

    /// <summary>Gets or sets the external reference voltage for calibration.</summary>
    [ObservableProperty]
    public partial double ReferenceVoltage { get; set; }

    /// <summary>Gets or sets the external reference current for calibration.</summary>
    [ObservableProperty]
    public partial double ReferenceCurrent { get; set; }

    /// <summary>Gets the live readings summary.</summary>
    [ObservableProperty]
    public partial string LiveReadings { get; private set; } = string.Empty;

    /// <summary>Gets whether the live telemetry is stale.</summary>
    [ObservableProperty]
    public partial bool IsStale { get; private set; }

    /// <summary>Updates the live readings from a new projection.</summary>
    /// <param name="live">The live reading projection.</param>
    public void UpdateLive(BatteryLiveReading live)
    {
        liveVoltage = live.VoltageVolts ?? 0;
        liveCurrent = live.CurrentAmps ?? 0;
        IsStale = live.IsStale;
        if (!live.HasTelemetry)
        {
            LiveReadings = "No live telemetry for this instance.";
            return;
        }

        var remaining = live.RemainingPercent is { } percent ? $"{percent}% (estimated)" : "—";
        LiveReadings = $"V {Format(live.VoltageVolts)} · A {Format(live.CurrentAmps)} · used {Format(live.ConsumedMah)} mAh · remaining {remaining}";
    }

    [RelayCommand]
    private Task ApplyCapacity()
    {
        return parent.ApplyAsync(Index, BatterySetting.Capacity, Capacity);
    }

    [RelayCommand]
    private async Task ApplyVoltageFailsafe()
    {
        await parent.ApplyAsync(Index, BatterySetting.CriticalVoltage, CriticalVoltage);
        await parent.ApplyAsync(Index, BatterySetting.LowVoltage, LowVoltage);
    }

    [RelayCommand]
    private async Task ApplyCapacityFailsafe()
    {
        await parent.ApplyAsync(Index, BatterySetting.CriticalCapacity, CriticalCapacity);
        await parent.ApplyAsync(Index, BatterySetting.LowCapacity, LowCapacity);
    }

    [RelayCommand]
    private Task CalibrateVoltage()
    {
        return parent.CalibrateAsync(Index, false, liveVoltage, ReferenceVoltage);
    }

    [RelayCommand]
    private Task CalibrateCurrent()
    {
        return parent.CalibrateAsync(Index, true, liveCurrent, ReferenceCurrent);
    }

    private static string Format(double? value)
    {
        return value is { } number ? number.ToString("0.##") : "—";
    }
}
