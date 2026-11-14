using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.App.Utilities;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Shared.Models.Vehicles.Models;
using Microsoft.Extensions.Logging;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.App.Models;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.Views.FlightData.Hud;

/// <summary>
/// View model for the HUD (Heads-Up Display) showing real-time vehicle telemetry.
/// </summary>
public partial class HudViewModel : ViewModelBase
{
    private readonly IVehicleHudDataService hudDataService;
    private IDisposable? hudDataSubscription;
    private readonly IUiDispatcher dispatcher;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly TimeProvider clock;
    private readonly HudHeadingReference headingReference = new();
    private readonly Avalonia.Threading.DispatcherTimer referenceTimer;
    private VehicleId? referenceVehicle;
    private CancellationToken referenceSession;
    private bool isDisposed;
    private bool isObserving = true;
    private readonly IPlannerSettingsService settings;
    private AttitudeDisplayMode? appliedDefaultDisplay;

    /// <summary>The available attitude presentations.</summary>
    public static IReadOnlyList<HudDisplayChoice> DisplayChoices => HudDisplayChoice.All;

    /// <summary>The user's presentation choice, retained when moving the HUD to another window.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHud), nameof(ShowFrame))]
    public partial HudDisplayChoice SelectedDisplay { get; set; } = DisplayChoices[2];

    /// <summary>Whether to show the cockpit HUD.</summary>
    public bool ShowHud => SelectedDisplay?.ShowHud ?? true;

    /// <summary>Whether to show the illustrative frame.</summary>
    public bool ShowFrame => SelectedDisplay?.ShowFrame ?? true;

    /// <summary>A complete attitude sample from the selected vehicle; yaw includes the local display offset.</summary>
    [ObservableProperty]
    public partial FrameAttitude FrameAttitude { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HudViewModel"/> class.
    /// </summary>
    /// <param name="hudDataService">The service providing HUD-specific vehicle data.</param>
    /// <param name="dispatcher">The Dispatcher for UI thread operations.</param>
    /// <param name="activeVehicle">Selected vehicle/session boundary.</param>
    /// <param name="clock">Freshness clock.</param>
    /// <param name="logger">Presentation logger.</param>
    /// <param name="events">Application event hub.</param>
    /// <param name="settings">Saved local presentation preferences.</param>
    public HudViewModel(IVehicleHudDataService hudDataService, IUiDispatcher dispatcher, IActiveVehicleContext activeVehicle, TimeProvider clock,
        ILogger<HudViewModel> logger, IDomainEventHub events, IPlannerSettingsService settings) : base(logger, dispatcher, events)
    {
        this.hudDataService = hudDataService ?? throw new ArgumentNullException(nameof(hudDataService));
        this.dispatcher = dispatcher;
        this.activeVehicle = activeVehicle;
        this.clock = clock;
        this.settings = settings;
        ApplyDisplayPreference();
        settings.SettingsChanged += OnSettingsChanged;
        referenceTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(100), Avalonia.Threading.DispatcherPriority.Background,
            (_, _) => RefreshHeadingReference());
        referenceTimer.Start();
        FlightMode = "Unknown";
        activeVehicle.Changed += OnActiveVehicleChanged;
        SubscribeToVehicleData();
        RefreshHeadingReference();
    }


    /// <summary>Yaw angle in degrees, clockwise from north.</summary>
    [ObservableProperty]
    public partial double Yaw
    {
        get; set;
    }

    /// <summary>Pitch angle in degrees. Positive = nose up.</summary>
    [ObservableProperty]
    public partial double Pitch
    {
        get; set;
    }

    /// <summary>Roll angle in degrees. Positive = right wing down.</summary>
    [ObservableProperty]
    public partial double Roll
    {
        get; set;
    }

    /// <summary>
    /// Distance to the MAV (Micro Air Vehicle) in meters.  
    /// </summary>
    [ObservableProperty]
    public partial double DistanceToMav
    {
        get; set;
    }

    /// <summary>
    /// Distance to the next waypoint in meters.
    /// </summary>
    [ObservableProperty]
    public partial double DistanceToWp
    {
        get; set;
    }

    /// <summary>Heading in degrees, 0-360.</summary>
    [ObservableProperty]
    public partial double Heading
    {
        get; set;
    }

    /// <summary>Indicated airspeed in m/s.</summary>
    [ObservableProperty]
    public partial double AirSpeed
    {
        get; set;
    }

    /// <summary>Ground speed in m/s.</summary>
    [ObservableProperty]
    public partial double GroundSpeed
    {
        get; set;
    }

    /// <summary>Altitude above home/sea level in meters.</summary>
    [ObservableProperty]
    public partial double Altitude
    {
        get; set;
    }

    /// <summary>Vertical climb/descent rate in m/s.</summary>
    [ObservableProperty]
    public partial double VerticalSpeed
    {
        get; set;
    }

    /// <summary>Battery voltage in volts.</summary>
    [ObservableProperty]
    public partial double BatteryVoltage
    {
        get; set;
    }

    /// <summary>Battery remaining percentage, 0-100.</summary>
    [ObservableProperty]
    public partial double BatteryRemaining { get; set; } = 100;

    /// <summary>Number of GPS satellites in view.</summary>
    [ObservableProperty]
    public partial int GpsSatellites
    {
        get; set;
    }

    /// <summary>Whether the vehicle is armed.</summary>
    [ObservableProperty]
    public partial bool IsArmed
    {
        get; set;
    }

    /// <summary>Gets the flight-controller arming and readiness label.</summary>
    [ObservableProperty]
    public partial string ArmingLabel { get; set; } = "Arming status unknown";

    /// <summary>Gets the retained pre-arm blocker.</summary>
    [ObservableProperty]
    public partial string? PreArmReason { get; set; }

    /// <summary>Gets the latest rejected arming attempt.</summary>
    [ObservableProperty]
    public partial string? LastArmFailure { get; set; }

    /// <summary>Current flight mode.</summary>
    [ObservableProperty]
    public partial string FlightMode
    {
        get; set;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        isDisposed = true;
        StopObserving();
        base.Dispose();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (!isDisposed && !isObserving)
        {
            isObserving = true;
            activeVehicle.Changed += OnActiveVehicleChanged;
            settings.SettingsChanged += OnSettingsChanged;
            ApplyDisplayPreference();
            SubscribeToVehicleData();
            RefreshHeadingReference();
            referenceTimer.Start();
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        StopObserving();
        return Task.CompletedTask;
    }

    private void StopObserving()
    {
        isObserving = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        settings.SettingsChanged -= OnSettingsChanged;
        hudDataSubscription?.Dispose();
        hudDataSubscription = null;
        referenceTimer.Stop();
    }

    private void OnSettingsChanged(PlannerSettingsChangedEventArgs args) => dispatcher.Dispatch(() =>
    {
        if (!isDisposed && isObserving) ApplyDisplayPreference();
    });

    private void ApplyDisplayPreference()
    {
        var preference = settings.Current.Telemetry.DefaultAttitudeDisplay;
        // Preserve a temporary dropdown selection across pop-outs and unrelated settings saves.
        if (appliedDefaultDisplay == preference) return;
        appliedDefaultDisplay = preference;
        SelectedDisplay = HudDisplayChoice.Resolve(preference);
    }

    /// <summary>Display-only yaw of the illustrative frame.</summary>
    [ObservableProperty]
    public partial double ModelHeading { get; private set; }

    /// <summary>Freshness and captured offset status.</summary>
    [ObservableProperty]
    public partial string HeadingReferenceStatus { get; private set; } = "Heading reference unavailable";

    /// <summary>Whether fresh attitude permits capturing a reference.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetModelHeadingCommand))]
    public partial bool CanResetModelHeading { get; private set; }

    /// <summary>Refreshes display-only reference and freshness from the selected vehicle.</summary>
    public void RefreshHeadingReference()
    {
        if (isDisposed) return;
        if (referenceVehicle != activeVehicle.VehicleId || referenceSession != activeVehicle.ConnectionCancellationToken || !activeVehicle.IsOnline)
        {
            headingReference.Restore();
            referenceVehicle = activeVehicle.VehicleId;
            referenceSession = activeVehicle.ConnectionCancellationToken;
        }
        var state = activeVehicle.State;
        var motion = state?.VehicleId == activeVehicle.VehicleId ? state?.Motion : null;
        var now = clock.GetUtcNow();
        CanResetModelHeading = activeVehicle.IsOnline && motion?.AttitudeObservedAt is { } at &&
            now - at >= TimeSpan.Zero && now - at <= TimeSpan.FromSeconds(2) &&
            motion.YawRadians is { } yaw && double.IsFinite(yaw);
        ModelHeading = motion?.YawRadians is { } radians && double.IsFinite(radians) ? headingReference.Project(radians * 180 / Math.PI) : 0;
        FrameAttitude = motion?.RollRadians is { } roll && double.IsFinite(roll) &&
            motion.PitchRadians is { } pitch && double.IsFinite(pitch) &&
            motion.YawRadians is { } modelYaw && double.IsFinite(modelYaw)
            ? new(roll * 180 / Math.PI, pitch * 180 / Math.PI, ModelHeading, true, CanResetModelHeading)
            : default;
        HeadingReferenceStatus = (CanResetModelHeading ? "" : "STALE / unavailable · ") +
            (headingReference.Offset is { } offset ? $"Display offset active: {offset:0.0}° · nose-away reference" : "Unadjusted yaw · north points away");
    }

    [RelayCommand(CanExecute = nameof(CanResetModelHeading))]
    private void ResetModelHeading()
    {
        RefreshHeadingReference();
        if (CanResetModelHeading && activeVehicle.State?.Motion.YawRadians is { } yaw)
        {
            headingReference.Reset(yaw * 180 / Math.PI, true);
            RefreshHeadingReference();
        }
    }

    [RelayCommand]
    private void RestoreModelHeading()
    {
        headingReference.Restore();
        RefreshHeadingReference();
    }

    private void SubscribeToVehicleData()
    {
        hudDataSubscription?.Dispose();
        hudDataSubscription = null;
        var vehicle = activeVehicle.VehicleId;
        var session = activeVehicle.ConnectionCancellationToken;
        ApplyHudData(VehicleHudData.CreateDefault(vehicle ?? default));
        if (vehicle is not { } id) return;
        // Both presentations follow the selected vehicle, including after a selection/session change.
        hudDataSubscription = hudDataService.ObserveHudData(id).Subscribe(hudData =>
            dispatcher.Dispatch(() =>
            {
                if (isDisposed || !isObserving || activeVehicle.VehicleId != id || activeVehicle.ConnectionCancellationToken != session) return;
                ApplyHudData(hudData);
                RefreshHeadingReference();
            }));
        if (activeVehicle.State?.VehicleId == id && hudDataService.GetHudData(id) is { } initial)
            ApplyHudData(initial);
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args) => dispatcher.Dispatch(() =>
    {
        if (isDisposed || !isObserving) return;
        SubscribeToVehicleData();
        RefreshHeadingReference();
    });

    private void ApplyHudData(VehicleHudData hudData)
    {
        Pitch = hudData.Pitch;
        Yaw = hudData.Yaw;
        Roll = hudData.Roll;
        Heading = hudData.Heading;
        AirSpeed = hudData.AirSpeed;
        GroundSpeed = hudData.GroundSpeed;
        Altitude = hudData.Altitude;
        VerticalSpeed = hudData.VerticalSpeed;
        BatteryVoltage = hudData.BatteryVoltage;
        BatteryRemaining = hudData.BatteryRemaining;
        DistanceToMav = hudData.DistanceToMav;
        DistanceToWp = hudData.DistanceToWp;
        GpsSatellites = hudData.GpsSatellites;
        IsArmed = hudData.IsArmed;
        ArmingLabel = hudData.Arming.State switch
        {
            VehicleArmingState.Armed => "ARMED",
            VehicleArmingState.DisarmedReady => "DISARMED — Ready to Arm",
            VehicleArmingState.DisarmedNotReady => "DISARMED — Not Ready to Arm",
            _ => "Arming readiness unknown"
        };
        PreArmReason = hudData.Arming.PreArmReason;
        LastArmFailure = hudData.Arming.LastArmFailure;
        FlightMode = hudData.Mode.ToString();
    }
}
