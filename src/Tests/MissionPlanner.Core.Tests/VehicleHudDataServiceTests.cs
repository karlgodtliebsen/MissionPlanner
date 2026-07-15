using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Unit tests for VehicleHudDataService verifying basic transformation logic.
/// For end-to-end tests with real registry, see VehicleHudDataIntegrationTests.
/// </summary>
public class VehicleHudDataServiceTests
{
    /// <summary>
    /// Verifies that the service returns null when the specified vehicle is not found.
    /// </summary>
    [Fact]
    public void Should_Return_Null_When_Vehicle_Not_Found()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<VehicleHudDataService>>();
        var mockRegistry = Substitute.For<IVehicleRegistry>();
        var mockEventHub = Substitute.For<IDomainEventHub>();
        mockEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(Arg.Any<Func<VehicleStateUpdated, CancellationToken, Task>>()).Returns(Substitute.For<IDisposable>());

        var service = new VehicleHudDataService(mockRegistry, mockEventHub, mockLogger);
        var vehicleId = new VehicleId(99, 99);
        mockRegistry.GetRequired(vehicleId).Returns((VehicleSession?)null);

        // Act
        var hudData = service.GetHudData(vehicleId);

        // Assert
        hudData.Should().BeNull();
    }

    /// <summary>
    /// Verifies that the service returns null for the primary vehicle when no vehicles exist.
    /// </summary>
    [Fact]
    public void Should_Return_Null_For_Primary_Vehicle_When_No_Vehicles_Exist()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<VehicleHudDataService>>();
        var mockRegistry = Substitute.For<IVehicleRegistry>();
        var mockEventHub = Substitute.For<IDomainEventHub>();
        mockEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(Arg.Any<Func<VehicleStateUpdated, CancellationToken, Task>>()).Returns(Substitute.For<IDisposable>());

        var service = new VehicleHudDataService(mockRegistry, mockEventHub, mockLogger);
        mockRegistry.Vehicles.Returns(Array.Empty<VehicleSession>());

        // Act
        var hudData = service.GetPrimaryVehicleHudData();

        // Assert
        hudData.Should().BeNull();
    }
}
