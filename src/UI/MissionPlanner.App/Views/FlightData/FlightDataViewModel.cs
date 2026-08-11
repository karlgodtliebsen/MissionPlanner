using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Coordinates the Flight Data page, its active tab, and active-vehicle status presentation.
/// </summary>
public partial class FlightDataViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<FlightDataViewModel> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataViewModel"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public FlightDataViewModel(ILogger<FlightDataViewModel> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
