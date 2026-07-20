using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Views.FlightData;

public partial class FlightDataViewModel : ObservableObject
{
    /// <summary>
    /// Provides the public API for MapStyles.
    /// </summary>
    public List<string> MapStyles { get; } =
    [
        "GEO",
        "UTM",
        "MGRS"
    ];

    [ObservableProperty] public partial string? SelectedMapStyle { get; set; }

    /// <inheritdoc />
    public FlightDataViewModel()
    {
        SelectedMapStyle = "GEO";
    }
}
