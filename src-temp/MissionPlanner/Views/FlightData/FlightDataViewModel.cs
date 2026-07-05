using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.Views.FlightData;

public partial class FlightDataViewModel : ObservableObject
{
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