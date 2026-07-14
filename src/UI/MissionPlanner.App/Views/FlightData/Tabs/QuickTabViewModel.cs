using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Services.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class QuickTabViewModel : ObservableObject, IDisposable
{
    private readonly IVehicleHudDataService hudDataService;

    private readonly IDispatcher dispatcher;
    private readonly ILogger<QuickTabViewModel> logger;
    private IDisposable? hudDataSubscription;

    /// <summary>
    /// Yaw angle in degrees. Positive = nose right.
    /// </summary>
    [ObservableProperty]
    public partial double Yaw { get; set; }

    /// <summary>Pitch angle in degrees. Positive = nose up.</summary>
    [ObservableProperty]
    public partial double Pitch { get; set; }

    /// <summary>Roll angle in degrees. Positive = right wing down.</summary>
    [ObservableProperty]
    public partial double Roll { get; set; }

    /// <summary>
    /// Distance to the MAV (Micro Air Vehicle) in meters.  
    /// </summary>
    [ObservableProperty]
    public partial double DistanceToMav { get; set; }

    /// <summary>
    /// Distance to the next waypoint in meters.
    /// </summary>
    [ObservableProperty]
    public partial double DistanceToWp { get; set; }

    /// <summary>
    /// Heading in degrees, 0-360.
    /// </summary>
    [ObservableProperty]
    public partial double Heading { get; set; }

    /// <summary>Indicated airspeed in m/s.</summary>
    [ObservableProperty]
    public partial double AirSpeed { get; set; }

    /// <summary>Ground speed in m/s.</summary>
    [ObservableProperty]
    public partial double GroundSpeed { get; set; }

    /// <summary>Altitude above home/sea level in meters.</summary>
    [ObservableProperty]
    public partial double Altitude { get; set; }

    /// <summary>Vertical climb/descent rate in m/s.</summary>
    [ObservableProperty]
    public partial double VerticalSpeed { get; set; }

    /// <summary>Battery voltage in volts.</summary>
    [ObservableProperty]
    public partial double BatteryVoltage { get; set; }

    /// <summary>Battery remaining percentage, 0-100.</summary>
    [ObservableProperty]
    public partial double BatteryRemaining { get; set; } = 100;

    /// <summary>Number of GPS satellites in view.</summary>
    [ObservableProperty]
    public partial int GpsSatellites { get; set; }

    /// <summary>Whether the vehicle is armed.</summary>
    [ObservableProperty]
    public partial bool IsArmed { get; set; }

    /// <summary>Current flight mode.</summary>
    [ObservableProperty]
    public partial string FlightMode { get; set; }

    /// <inheritdoc />
    public QuickTabViewModel(IVehicleHudDataService hudDataService, IDispatcher dispatcher, ILogger<QuickTabViewModel> logger)
    {
        this.hudDataService = hudDataService;
        this.dispatcher = dispatcher;
        this.logger = logger;
        FlightMode = "Unknown";
        SubscribeToVehicleData();
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
                Roll = hudData.Roll;
                Yaw = hudData.Yaw;
                Heading = hudData.Heading;
                AirSpeed = hudData.AirSpeed;
                GroundSpeed = hudData.GroundSpeed;
                Altitude = hudData.Altitude;
                VerticalSpeed = hudData.VerticalSpeed;
                BatteryVoltage = hudData.BatteryVoltage;
                BatteryRemaining = hudData.BatteryRemaining;
                GpsSatellites = hudData.GpsSatellites;
                IsArmed = hudData.IsArmed;
                FlightMode = hudData.Mode.ToString();
                DistanceToMav = hudData.DistanceToMav;
                DistanceToWp = hudData.DistanceToWp;
            }));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        hudDataSubscription?.Dispose();
    }
}
