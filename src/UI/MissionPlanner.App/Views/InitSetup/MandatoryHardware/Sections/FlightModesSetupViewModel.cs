using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects firmware flight-mode slot configuration and confirmed slot writes into Setup controls.</summary>
public sealed partial class FlightModesSetupViewModel : ViewModelBase
{
    private readonly INavigationService navigation;

    private readonly IActiveVehicleContext activeVehicle;
    private readonly IFlightModeConfigurationService modeService;
    private readonly IVehicleParameterRegistry parameters;
    private VehicleId? loadedVehicleId;
    private CancellationTokenSource? operationCancellation;
    private readonly Avalonia.Threading.DispatcherTimer switchTimer;

    /// <summary>Initializes the flight-mode Setup workflow.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="modeService">The flight-mode configuration service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="navigation">The application navigation service.</param>
    /// <param name="parameters">The vehicle parameter notifications.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="eventHub">The application event hub.</param>
    public FlightModesSetupViewModel(
        IActiveVehicleContext activeVehicle,
        IFlightModeConfigurationService modeService, ILogger<FlightModesSetupViewModel> logger, INavigationService navigation,
        IVehicleParameterRegistry parameters, IUiDispatcher dispatcher, IDomainEventHub eventHub)
        : base(logger, dispatcher, eventHub)
    {
        this.navigation = navigation;
        this.activeVehicle = activeVehicle;
        this.modeService = modeService;
        this.parameters = parameters;
        switchTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(200), Avalonia.Threading.DispatcherPriority.Background,
            (_, _) => RefreshSwitchEvidence());
        switchTimer.Stop();
    }

    /// <summary>Gets the six flight-mode slots.</summary>
    public ObservableCollection<FlightModeSlotViewModel> Slots
    {
        get;
    } = [];


    /// <summary>Gets the configured mode channel description.</summary>
    [ObservableProperty]
    public partial string ModeChannelDescription
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>Gets whether the connected firmware supports flight-mode slots.</summary>
    [ObservableProperty]
    public partial bool IsSupported
    {
        get;
        private set;
    }

    /// <summary>Raw RC switch evidence and heartbeat mode, kept separate from pending assignments.</summary>
    [ObservableProperty]
    public partial string SwitchEvidence { get; private set; } = "RC input unavailable";

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        SetMessages("Load the connected vehicle's flight-mode configuration.");
        activeVehicle.Changed += OnActiveVehicleChanged;
        parameters.Changed += OnParameterChanged;
        Slots.Clear();
        Load();
        switchTimer.Start();
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        switchTimer.Stop();
        Cancel();
        activeVehicle.Changed -= OnActiveVehicleChanged;
        parameters.Changed -= OnParameterChanged;
        return base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        switchTimer.Stop();
        Cancel();
        activeVehicle.Changed -= OnActiveVehicleChanged;
        parameters.Changed -= OnParameterChanged;
        base.Dispose();
    }

    /// <inheritdoc />
    public void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }


    /// <summary>Applies the reviewed mode assignment for one slot with readback confirmation.</summary>
    /// <param name="slot">The slot to apply.</param>
    /// <returns>A task that completes after the write is confirmed or reported failed.</returns>
    internal async Task ApplySlotAsync(FlightModeSlotViewModel slot)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            SetMessages("Connect a vehicle before editing flight modes.");
            return;
        }

        if (loadedVehicleId != vehicleId || !Slots.Contains(slot) || !slot.CanApply || slot.SelectedMode?.ModeNumber is not { } mode)
        {
            return;
        }

        Cancel();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        SetMessages(null, null);
        try
        {
            var result = await modeService.SetSlotAsync(vehicleId, slot.Slot, (int)mode, operationCancellation.Token);
            if (activeVehicle.VehicleId == vehicleId)
            {
                Dispatcher.Dispatch(() =>
                {
                    Load();
                    SetMessages(result.Message);
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Applying flight-mode slot failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
        }
    }

    /// <summary>Opens the shared Parameters Editor workspace.</summary>
    [RelayCommand]
    private Task OpenFullParametersAsync()
    {
        return navigation.NavigateAsync(MissionPlannerRoutes.ConfigurationParametersEditor);
    }

    [RelayCommand]
    private void Refresh()
    {
        Load();
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            Cancel();
            Dispatcher.Dispatch(() =>
            {
                Slots.Clear();
                Load();
            });
        }
    }

    private void OnParameterChanged(VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId == activeVehicle.VehicleId && (args.Parameter is null ||
            args.Parameter.Name.StartsWith("FLTMODE", StringComparison.Ordinal) ||
            args.Parameter.Name.StartsWith("MODE", StringComparison.Ordinal)))
        {
            Dispatcher.Dispatch(Load);
        }
    }

    private void Load()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Slots.Clear();
            loadedVehicleId = null;
            ModeChannelDescription = string.Empty;
            IsSupported = false;
            SetMessages("Connect a vehicle to configure flight modes.");
            return;
        }

        FlightModeConfiguration configuration;
        try
        {
            configuration = modeService.GetConfiguration(vehicleId);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Loading flight-mode configuration failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
            return;
        }

        IsSupported = configuration.IsSupported;
        if (loadedVehicleId != vehicleId)
        {
            Slots.Clear();
        }
        loadedVehicleId = vehicleId;
        if (!configuration.IsSupported)
        {
            Slots.Clear();
            ModeChannelDescription = string.Empty;
            SetMessages($"{configuration.Family} does not expose a switch-based flight-mode channel.");
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

        SetMessages("Choose a mode, then Apply that slot. Listed modes are known family mappings, not confirmation of support in this firmware build. The highlighted slot is the RC switch position, not the active mode reported by telemetry.");
        RefreshSwitchEvidence();
    }

    private void RefreshSwitchEvidence()
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId is not { } id || loadedVehicleId != id)
        {
            SwitchEvidence = "Disconnected — RC selection and active mode unavailable";
            foreach (var row in Slots)
            {
                row.SetActive(false);
            }
            return;
        }
        FlightModeConfiguration config;
        try
        {
            config = modeService.GetConfiguration(id);
        }
        catch (InvalidOperationException)
        {
            // A disconnect/selection change can race a presentation timer tick.
            SwitchEvidence = "Vehicle changed — waiting for current configuration";
            foreach (var row in Slots)
            {
                row.SetActive(false);
            }
            return;
        }
        var selected = config.Slots.FirstOrDefault(slot => slot.IsActive);
        SwitchEvidence = $"RC{config.ModeChannel} raw: {config.RawPwm?.ToString() ?? "unavailable"} µs" +
            (config.IsRadioFresh ? "" : " (stale)") +
            $"\nSelected slot: {selected?.Slot.ToString() ?? "unknown"} · Assigned mode: {selected?.SelectedModeName ?? "unknown"}" +
            $"\nFC active mode: {config.ActiveMode}";
        foreach (var row in Slots)
        {
            row.SetActive(row.Slot == selected?.Slot);
        }
    }
}

/// <summary>Presents one flight-mode slot with a mode picker and live active indicator.</summary>
public sealed partial class FlightModeSlotViewModel : ObservableObject
{
    private readonly FlightModesSetupViewModel parent;
    private float? receivedModeNumber;

    /// <summary>Initializes a slot row.</summary>
    /// <param name="slot">The slot projection.</param>
    /// <param name="options">The available modes.</param>
    /// <param name="parent">The owning flight-mode workflow.</param>
    public FlightModeSlotViewModel(FlightModeSlot slot, IReadOnlyList<VehicleModeOption> options, FlightModesSetupViewModel parent)
    {
        this.parent = parent;
        Slot = slot.Slot;
        Update(slot, options);
    }

    /// <summary>Gets the one-based slot number.</summary>
    public int Slot
    {
        get;
    }

    /// <summary>Gets the available modes.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FlightModeChoice> Options
    {
        get;
        private set;
    } = [];

    /// <summary>Gets the original parameter value independently of the displayed selection.</summary>
    public float? ReceivedModeNumber => receivedModeNumber;

    /// <summary>Gets whether the slot parameter has loaded.</summary>
    public bool IsLoaded => receivedModeNumber.HasValue;

    /// <summary>Gets whether the user has selected a different known mode.</summary>
    public bool CanApply => IsLoaded && SelectedMode is { IsKnown: true, ModeNumber: { } number } && number != receivedModeNumber;

    /// <summary>Gets the PWM band description.</summary>
    [ObservableProperty]
    public partial string BandDescription
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>Gets whether the mode channel currently selects this slot.</summary>
    [ObservableProperty]
    public partial bool IsActive
    {
        get;
        private set;
    }

    /// <summary>Gets or sets the selected mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial FlightModeChoice? SelectedMode
    {
        get;
        set;
    }

    /// <summary>Updates the slot from a new projection.</summary>
    /// <param name="slot">The slot projection.</param>
    /// <param name="options">The available modes.</param>
    public void Update(FlightModeSlot slot, IReadOnlyList<VehicleModeOption> options)
    {
        var pending = CanApply && receivedModeNumber == slot.SelectedModeNumber ? SelectedMode : null;
        receivedModeNumber = slot.SelectedModeNumber;
        var choices = options.Select(option => new FlightModeChoice(option.Name, option.CustomMode, true)).ToList();
        var stored = choices.FirstOrDefault(option => option.ModeNumber == receivedModeNumber);
        if (stored is null)
        {
            stored = new FlightModeChoice(slot.SelectedModeName, receivedModeNumber, false);
            choices.Add(stored);
        }
        Options = choices;
        BandDescription = slot.PwmLow == 0 ? $"Slot {slot.Slot}: PWM ≤ {slot.PwmHigh}" : $"Slot {slot.Slot}: PWM {slot.PwmLow}-{slot.PwmHigh}";
        IsActive = slot.IsActive;
        SelectedMode = pending is not null ? choices.FirstOrDefault(option => option == pending) ?? stored : stored;
        OnPropertyChanged(nameof(ReceivedModeNumber));
        OnPropertyChanged(nameof(IsLoaded));
        OnPropertyChanged(nameof(CanApply));
        ApplyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplyAsync()
    {
        return parent.ApplySlotAsync(this);
    }

    internal void SetActive(bool active) => IsActive = active;
}

/// <summary>A row-specific display choice retaining its numeric value without implying firmware support.</summary>
/// <param name="Name">The catalogue label or received-value placeholder.</param>
/// <param name="ModeNumber">The mode value, or null for a parameter that has not loaded.</param>
/// <param name="IsKnown">Whether this is a choice from the shared family catalogue.</param>
public sealed record FlightModeChoice(string Name, float? ModeNumber, bool IsKnown);

