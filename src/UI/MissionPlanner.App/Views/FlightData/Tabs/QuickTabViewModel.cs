using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class QuickTabViewModel : ObservableObject, IDisposable
{
    private readonly IDomainEventHub domainEventHub;
    private readonly IDispatcher dispatcher;
    private readonly CancellationTokenSource cts;
    private readonly ILogger<QuickTabViewModel> logger;

    private readonly List<IDisposable> eventSubscriptions = [];
    [ObservableProperty] public partial double Altitude { get; set; }
    [ObservableProperty] public partial double GroundSpeed { get; set; }
    [ObservableProperty] public partial double DistWP { get; set; }
    [ObservableProperty] public partial double Yaw { get; set; }
    [ObservableProperty] public partial double VerticalSpeed { get; set; }
    [ObservableProperty] public partial double DistToMav { get; set; }


    /// <inheritdoc />
    public QuickTabViewModel(
        // IEventHub eventHub,
        IDomainEventHub domainEventHub,
        IDispatcher dispatcher,
        CancellationTokenSource cts,
        ILogger<QuickTabViewModel> logger)
    {
        this.domainEventHub = domainEventHub;
        this.dispatcher = dispatcher;
        this.cts = cts;
        this.logger = logger;
        var eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(VehicleStatusUpdated);
        eventSubscriptions.Add(eventSubscription);
        //  await domainEventHub.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State), cancellationToken);
        //var eventSubscription = eventHub.SubscribeAsync<AttitudeMessage>(MavLinkEventTopics.ReceivedMessage, AttitudeMessageRegistered);
        //eventSubscriptions.Add(eventSubscription);
    }

    private Task VehicleStatusUpdated(VehicleStateUpdated message, CancellationToken cancellationToken)
    {
        dispatcher.Dispatch(() =>
        {
            Yaw = message.VehicleState.Yaw ?? 0.0;
            Altitude = message.VehicleState.Altitude ?? 0.0;
            //GroundSpeed = message.VehicleState.GroundSpeed ?? 0.0;
            //DistWP = message.VehicleState.DistWP ?? 0.0;
            //VerticalSpeed = message.VehicleState.VerticalSpeed ?? 0.0;
            //DistToMav = message.VehicleState.DistToMav ?? 0.0;
        });
        return Task.CompletedTask;
    }

    //private Task AttitudeMessageRegistered(AttitudeMessage message, CancellationToken cancellationToken)
    //{
    //    dispatcher.Dispatch(() => Yaw = message.Yaw);
    //    return Task.CompletedTask;
    //}


    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var subscription in eventSubscriptions)
        {
            subscription.Dispose();
        }
    }
}
