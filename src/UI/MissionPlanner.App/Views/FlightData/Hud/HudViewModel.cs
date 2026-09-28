using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.App.Utilities;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Shared.Models.Vehicles.Models;
using Microsoft.Extensions.Logging;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Vehicles.Abstractions;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="HudViewModel"/> class.
    /// </summary>
    /// <param name="hudDataService">The service providing HUD-specific vehicle data.</param>
    /// <param name="dispatcher">The Dispatcher for UI thread operations.</param>
    /// <param name="activeVehicle">Selected vehicle/session boundary.</param>
    /// <param name="clock">Freshness clock.</param>
    /// <param name="logger">Presentation logger.</param>
    /// <param name="events">Application event hub.</param>
    public HudViewModel(IVehicleHudDataService hudDataService, IUiDispatcher dispatcher, IActiveVehicleContext activeVehicle, TimeProvider clock,
        ILogger<HudViewModel> logger, IDomainEventHub events) : base(logger, dispatcher, events)
    {
        this.hudDataService = hudDataService ?? throw new ArgumentNullException(nameof(hudDataService));
        this.dispatcher = dispatcher;
        this.activeVehicle = activeVehicle;
        this.clock = clock;
        referenceTimer = new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(100), Avalonia.Threading.DispatcherPriority.Background,
            (_, _) => RefreshHeadingReference());
        referenceTimer.Start();
        FlightMode = "Unknown";
        SubscribeToVehicleData();
    }


    /// <summary>Pitch angle in degrees. Positive = nose up.</summary>
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
        hudDataSubscription?.Dispose();
        referenceTimer.Stop();
        base.Dispose();
    }

    /// <summary>Angle of the separate display-only heading arrow.</summary>
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
        if (referenceVehicle != activeVehicle.VehicleId || referenceSession != activeVehicle.ConnectionCancellationToken || !activeVehicle.IsOnline)
        {
            headingReference.Restore();
            referenceVehicle = activeVehicle.VehicleId;
            referenceSession = activeVehicle.ConnectionCancellationToken;
        }
        var motion = activeVehicle.State?.Motion;
        CanResetModelHeading = activeVehicle.IsOnline && activeVehicle.State?.VehicleId == activeVehicle.VehicleId && motion?.AttitudeObservedAt is { } at &&
            clock.GetUtcNow() - at >= TimeSpan.Zero && clock.GetUtcNow() - at <= TimeSpan.FromSeconds(2) &&
            motion.YawRadians is { } yaw && double.IsFinite(yaw);
        ModelHeading = motion?.YawRadians is { } radians && double.IsFinite(radians) ? headingReference.Project(radians * 180 / Math.PI) : 0;
        HeadingReferenceStatus = (CanResetModelHeading ? "" : "STALE / unavailable · ") +
            (headingReference.Offset is { } offset ? $"Display offset active: {offset:0.0}° · arrow zero = captured nose-away" : "Unadjusted yaw · arrow zero = north") +
            "\nCockpit horizon: roll/pitch unchanged. Compass heading unchanged.";
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
        // Subscribe to HUD data updates from the primary vehicle
        var observable = hudDataService.ObservePrimaryVehicleHudData();

        hudDataSubscription = observable.Subscribe(hudData =>
            // Update all properties with the new data
            dispatcher.Dispatch(() =>
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
                    MissionPlanner.Core.Vehicles.Models.VehicleArmingState.Armed => "ARMED",
                    MissionPlanner.Core.Vehicles.Models.VehicleArmingState.DisarmedReady => "DISARMED — Ready to Arm",
                    MissionPlanner.Core.Vehicles.Models.VehicleArmingState.DisarmedNotReady => "DISARMED — Not Ready to Arm",
                    _ => "Arming readiness unknown"
                };
                PreArmReason = hudData.Arming.PreArmReason;
                LastArmFailure = hudData.Arming.LastArmFailure;
                FlightMode = hudData.Mode.ToString();
            }));
    }
}
