using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class TransponderTabViewModel : ObservableObject, IDisposable
{
    private readonly IVehicleHudDataService hudDataService;

    private readonly IDispatcher dispatcher;
    private readonly ILogger<TransponderTabViewModel> logger;
    private readonly IDisposable? hudDataSubscription;


    /// <inheritdoc />
    public TransponderTabViewModel(IVehicleHudDataService hudDataService, IDispatcher dispatcher, ILogger<TransponderTabViewModel> logger)
    {
        this.hudDataService = hudDataService;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }


    /// <inheritdoc/>
    public void Dispose()
    {
        hudDataSubscription?.Dispose();
    }
}
