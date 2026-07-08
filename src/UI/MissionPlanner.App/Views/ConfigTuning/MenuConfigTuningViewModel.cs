using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.ConfigTuning;

public partial class MenuConfigTuningViewModel : ObservableObject
{
    /// <inheritdoc />
    public MenuConfigTuningViewModel(IDomainEventHub domainEventHub)
    {
        // domainEventHub.SubscribeDomainEventAsync<VehicleConnected>((VehicleConnected evt, CancellationToken ct)
        //domainEventHub.SubscribeDomainEventAsync<VehicleConnected>((VehicleConnected evt, CancellationToken ct)
    }
}
