using FluentAssertions;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using NSubstitute;

namespace MissionPlanner.Test;

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
        var mockRegistry = Substitute.For<IVehicleRegistry>();
        var mockEventHub = Substitute.For<IDomainEventHub>();
        mockEventHub.Subscribe(Arg.Any<Action<VehicleStateUpdated>>()).Returns(Substitute.For<IDisposable>());

        var service = new VehicleHudDataService(mockRegistry, mockEventHub);
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
        var mockRegistry = Substitute.For<IVehicleRegistry>();
        var mockEventHub = Substitute.For<IDomainEventHub>();
        mockEventHub.Subscribe(Arg.Any<Action<VehicleStateUpdated>>()).Returns(Substitute.For<IDisposable>());

        var service = new VehicleHudDataService(mockRegistry, mockEventHub);
        mockRegistry.Vehicles.Returns(Array.Empty<VehicleSession>());

        // Act
        var hudData = service.GetPrimaryVehicleHudData();

        // Assert
        hudData.Should().BeNull();
    }

    /// <summary>
    /// Verifies that the service is created and subscribes to events.
    /// </summary>
    [Fact]
    public void Should_Create_Service_And_Subscribe_To_Events()
    {
        // Arrange
        var mockRegistry = Substitute.For<IVehicleRegistry>();
        var mockEventHub = Substitute.For<IDomainEventHub>();
        mockEventHub.Subscribe(Arg.Any<Action<VehicleStateUpdated>>()).Returns(Substitute.For<IDisposable>());

        // Act
        var service = new VehicleHudDataService(mockRegistry, mockEventHub);

        // Assert
        service.Should().NotBeNull();
        mockEventHub.Received(1).Subscribe(Arg.Any<Action<VehicleStateUpdated>>());
    }

    /// <summary>
    /// Verifies that the service disposes of its resources when disposed.
    /// </summary>
    [Fact]
    public void Should_Dispose_Resources_On_Dispose()
    {
        // Arrange
        var mockRegistry = Substitute.For<IVehicleRegistry>();
        var mockEventHub = Substitute.For<IDomainEventHub>();
        var mockSubscription = Substitute.For<IDisposable>();
        mockEventHub.Subscribe(Arg.Any<Action<VehicleStateUpdated>>()).Returns(mockSubscription);

        var service = new VehicleHudDataService(mockRegistry, mockEventHub);

        // Act
        service.Dispose();

        // Assert
        mockSubscription.Received(1).Dispose();
    }
}
