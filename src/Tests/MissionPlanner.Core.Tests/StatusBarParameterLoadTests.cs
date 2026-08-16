using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies global parameter-load status projection.</summary>
public sealed class StatusBarParameterLoadTests
{
    /// <summary>Verifies a status bar created during a load immediately uses the retained snapshot.</summary>
    [Fact]
    public void RetainedParameterLoadStatusIsDisplayedOnCreation()
    {
        var vehicleId = new VehicleId(1, 1);
        var activeVehicle = Substitute.For<IActiveVehicleContext>();
        activeVehicle.Current.Returns(new ActiveVehicleSnapshot(vehicleId, CreateOnlineState(vehicleId)));
        var stateService = new ApplicationStateService(activeVehicle);
        var statusContext = new VehicleParameterLoadStatusContext();
        statusContext.Update(new ParameterLoadStatus(
            vehicleId,
            ParameterLoadState.Downloading,
            400,
            1000,
            40,
            "Downloading parameters… 400/1000 (40%)",
            DateTimeOffset.UtcNow));
        var dispatcher = Substitute.For<IDispatcher>();
        var eventHub = Substitute.For<IDomainEventHub>();
        eventHub.SubscribeDomainEventAsync<VehicleConnected>(Arg.Any<Func<VehicleConnected, CancellationToken, Task>>())
            .Returns(Substitute.For<IDisposable>());
        eventHub.SubscribeDomainEventAsync<VehicleDisconnected>(Arg.Any<Func<VehicleDisconnected, CancellationToken, Task>>())
            .Returns(Substitute.For<IDisposable>());
        eventHub.SubscribeDomainEventAsync<VehicleParameterLoadStatusChanged>(Arg.Any<Func<VehicleParameterLoadStatusChanged, CancellationToken, Task>>())
            .Returns(Substitute.For<IDisposable>());

        using var viewModel = new StatusBarViewModel(
            stateService,
            dispatcher,
            eventHub,
            statusContext,
            NullLogger<StatusBarViewModel>.Instance);

        viewModel.StatusMessage.Should().Be("Downloading parameters… 400/1000 (40%)");
        stateService.Dispose();
    }

    private static VehicleState CreateOnlineState(VehicleId vehicleId) => new(
        vehicleId,
        0,
        2,
        3,
        0,
        4,
        3,
        VehicleConnectionState.Online,
        DateTimeOffset.UtcNow,
        VehicleMode.Stabilize,
        false,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);
}
