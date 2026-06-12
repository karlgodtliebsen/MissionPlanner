using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.Views.FlightData.Hud;

public partial class HudViewModel : ObservableObject
{
    /// <summary>Pitch angle in degrees. Positive = nose up.</summary>
    [ObservableProperty] public partial double Pitch { get; set; }

    /// <summary>Roll angle in degrees. Positive = right wing down.</summary>
    [ObservableProperty] public partial double Roll { get; set; }

    /// <summary>Heading in degrees, 0-360.</summary>
    [ObservableProperty] public partial double Heading { get; set; }

    /// <summary>Indicated airspeed.</summary>
    [ObservableProperty] public partial double AirSpeed { get; set; }

    /// <summary>Ground speed.</summary>
    [ObservableProperty] public partial double GroundSpeed { get; set; }

    /// <summary>Altitude above home/sea level.</summary>
    [ObservableProperty] public partial double Altitude { get; set; }

    /// <summary>Vertical climb/descent rate.</summary>
    [ObservableProperty] public partial double VerticalSpeed { get; set; }

    /// <summary>Battery voltage.</summary>
    [ObservableProperty] public partial double BatteryVoltage { get; set; }

    /// <summary>Battery remaining percentage, 0-100.</summary>
    [ObservableProperty] public partial double BatteryRemaining { get; set; } = 100;

    /// <summary>Number of GPS satellites in view.</summary>
    [ObservableProperty] public partial int GpsSatellites { get; set; }
}
