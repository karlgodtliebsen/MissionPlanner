using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects firmware flight-mode slot configuration and confirmed slot writes into Setup controls.</summary>
public sealed partial class FlightModesSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IFlightModeConfigurationService modeService;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<FlightModesSetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the flight-mode Setup workflow.</summary>
    /// <param name="descriptor">The flight-mode workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="modeService">The flight-mode configuration service.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public FlightModesSetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        IFlightModeConfigurationService modeService,
        IDispatcher dispatcher,
        ILogger<FlightModesSetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.modeService = modeService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        activeVehicle.Changed += OnActiveVehicleChanged;
        Load();
    }

    /// <summary>Gets the six flight-mode slots.</summary>
    public ObservableCollection<FlightModeSlotViewModel> Slots { get; } = [];

    /// <summary>Gets the workflow status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Load the connected vehicle's flight-mode configuration.";

    /// <summary>Gets the configured mode channel description.</summary>
    [ObservableProperty]
    public partial string ModeChannelDescription { get; private set; } = string.Empty;

    /// <summary>Gets whether the connected firmware supports flight-mode slots.</summary>
    [ObservableProperty]
    public partial bool IsSupported { get; private set; }

    /// <inheritdoc />
    public override void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        base.Dispose();
    }

    /// <summary>Applies the reviewed mode assignment for one slot with readback confirmation.</summary>
    /// <param name="slot">The slot to apply.</param>
    /// <returns>A task that completes after the write is confirmed or reported failed.</returns>
    internal async Task ApplySlotAsync(FlightModeSlotViewModel slot)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle before editing flight modes.";
            return;
        }

        if (slot.SelectedMode is not { } mode)
        {
            return;
        }

        Cancel();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        Error = null;
        try
        {
            var result = await modeService.SetSlotAsync(vehicleId, slot.Slot, (int)mode.CustomMode, operationCancellation.Token);
            Status = result.Message;
            dispatcher.Dispatch(Load);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Applying flight-mode slot failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        Load();
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(Load);
    }

    private void Load()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Slots.Clear();
            IsSupported = false;
            Status = "Connect a vehicle to configure flight modes.";
            return;
        }

        FlightModeConfiguration configuration;
        try
        {
            configuration = modeService.GetConfiguration(vehicleId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Loading flight-mode configuration failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
            return;
        }

        IsSupported = configuration.IsSupported;
        if (!configuration.IsSupported)
        {
            Slots.Clear();
            ModeChannelDescription = string.Empty;
            Status = $"{configuration.Family} does not expose a switch-based flight-mode channel.";
            return;
        }

        ModeChannelDescription = configuration.ModeChannel > 0
            ? $"Mode channel: RC{configuration.ModeChannel}"
            : "Mode channel is not configured.";

        if (Slots.Count == configuration.Slots.Count)
        {
            for (var index = 0; index < configuration.Slots.Count; index++)
            {
                Slots[index].Update(configuration.Slots[index], configuration.Options);
            }
        }
        else
        {
            Slots.Clear();
            foreach (var slot in configuration.Slots)
            {
                Slots.Add(new FlightModeSlotViewModel(slot, configuration.Options, this));
            }
        }

        Status = "Assign a mode to each switch position. The active slot updates live from the transmitter.";
    }
}

/// <summary>Presents one flight-mode slot with a mode picker and live active indicator.</summary>
public sealed partial class FlightModeSlotViewModel : ObservableObject
{
    private readonly FlightModesSetupViewModel parent;
    private bool suppressApply;

    /// <summary>Initializes a slot row.</summary>
    /// <param name="slot">The slot projection.</param>
    /// <param name="options">The available modes.</param>
    /// <param name="parent">The owning flight-mode workflow.</param>
    public FlightModeSlotViewModel(FlightModeSlot slot, IReadOnlyList<VehicleModeOption> options, FlightModesSetupViewModel parent)
    {
        this.parent = parent;
        Slot = slot.Slot;
        Options = options;
        Update(slot, options);
    }

    /// <summary>Gets the one-based slot number.</summary>
    public int Slot { get; }

    /// <summary>Gets the available modes.</summary>
    public IReadOnlyList<VehicleModeOption> Options { get; private set; }

    /// <summary>Gets the PWM band description.</summary>
    [ObservableProperty]
    public partial string BandDescription { get; private set; } = string.Empty;

    /// <summary>Gets whether the mode channel currently selects this slot.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; private set; }

    /// <summary>Gets or sets the selected mode.</summary>
    [ObservableProperty]
    public partial VehicleModeOption? SelectedMode { get; set; }

    /// <summary>Updates the slot from a new projection.</summary>
    /// <param name="slot">The slot projection.</param>
    /// <param name="options">The available modes.</param>
    public void Update(FlightModeSlot slot, IReadOnlyList<VehicleModeOption> options)
    {
        suppressApply = true;
        Options = options;
        BandDescription = slot.PwmLow == 0 ? $"Slot {slot.Slot}: PWM ≤ {slot.PwmHigh}" : $"Slot {slot.Slot}: PWM {slot.PwmLow}-{slot.PwmHigh}";
        IsActive = slot.IsActive;
        SelectedMode = options.FirstOrDefault(option => option.CustomMode == (uint)slot.SelectedModeNumber);
        suppressApply = false;
    }

    partial void OnSelectedModeChanged(VehicleModeOption? value)
    {
        if (!suppressApply && value is not null)
        {
            _ = parent.ApplySlotAsync(this);
        }
    }
}
