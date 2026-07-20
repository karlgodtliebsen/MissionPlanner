using FluentAssertions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Simulator;
using MissionPlanner.Test.Support.Configuration;

namespace MissionPlanner.Core.Tests.IntegrationTests;

/// <summary>
/// Integration tests for VehicleHudDataService verifying end-to-end data flow
/// from vehicle state updates through to HUD data.
/// </summary>
public class VehicleHudDataIntegrationTests
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleHudDataIntegrationTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public VehicleHudDataIntegrationTests(ITestOutputHelper output)
    {
        this.output = output;
        var services = TestConfigurator
            .AddTestConfiguration(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }

    /// <summary>
    /// Verifies that the service returns the current HUD data for a specified vehicle.
    /// </summary>
    [Fact]
    public async Task Should_Get_Current_HudData_From_RegistryAsync()
    {
        // Arrange
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var hudDataService = serviceProvider.GetRequiredService<IVehicleHudDataService>();

        var vehicleId = new VehicleId(1, 1);
        var result =
            new SimulatedVehicleState
            {
                VehicleId = vehicleId,
                Latitude = 56.1629,
                Longitude = 10.2039,
                Altitude = 125.5,
                Pitch = -3.1,
                Roll = 5.2,
                Yaw = 90.0,
                BatteryRemaining = 87,
                BatteryVoltage = 11.4f
            };
        await result.ApplyToAsync(registry, TestContext.Current.CancellationToken);

        // Act
        var hudData = hudDataService.GetHudData(vehicleId);

        // Assert
        hudData.Should().NotBeNull();
        hudData!.VehicleId.Should().Be(vehicleId);
        hudData.Pitch.Should().Be(-177.6);
        hudData.Roll.Should().Be(5.2);
        hudData.Heading.Should().Be(90.0); // East
        hudData.Altitude.Should().Be(125.5);
        hudData.BatteryVoltage.Should().BeApproximately(11.4, 0.1);
        hudData.BatteryRemaining.Should().Be(87);
    }

    /// <summary>
    /// Provides the public API for Should_Get_Primary_Vehicle_HudDataAsync.
    /// </summary>
    [Fact]
    public async Task Should_Get_Primary_Vehicle_HudDataAsync()
    {
        // Arrange
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var hudDataService = serviceProvider.GetRequiredService<IVehicleHudDataService>();

        var vehicleId = new VehicleId(1, 1);

        // Add vehicle
        var result = new SimulatedVehicleState
        {
            VehicleId = vehicleId,
            Latitude = 56.1629,
            Longitude = 10.2039,
            Altitude = 100.0,
            BatteryRemaining = 90
        };
        await result.ApplyToAsync(registry, TestContext.Current.CancellationToken);

        // Act
        var primaryHudData = hudDataService.GetPrimaryVehicleHudData();

        // Assert
        primaryHudData.Should().NotBeNull();
        primaryHudData!.VehicleId.Should().Be(vehicleId);
        primaryHudData.Altitude.Should().Be(100.0);
    }
}
