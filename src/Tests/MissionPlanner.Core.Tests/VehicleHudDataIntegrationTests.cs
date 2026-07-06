using FluentAssertions;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.Core.Tests.Configuration;
using MissionPlanner.Simulator;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Integration tests for VehicleHudDataService verifying end-to-end data flow
/// from vehicle state updates through to HUD data.
/// </summary>
public class VehicleHudDataIntegrationTests
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;

    public VehicleHudDataIntegrationTests(ITestOutputHelper output)
    {
        this.output = output;
        var services = TestConfigurator
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }

    [Fact]
    public void Should_Get_Current_HudData_From_Registry()
    {
        // Arrange
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var hudDataService = serviceProvider.GetRequiredService<IVehicleHudDataService>();

        var vehicleId = new VehicleId(1, 1);

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
        }.ApplyTo(registry);

        // Act
        var hudData = hudDataService.GetHudData(vehicleId);

        // Assert
        hudData.Should().NotBeNull();
        hudData!.VehicleId.Should().Be(vehicleId);
        hudData.Pitch.Should().Be(-3.1);
        hudData.Roll.Should().Be(5.2);
        hudData.Heading.Should().Be(90.0); // East
        hudData.Altitude.Should().Be(125.5);
        hudData.BatteryVoltage.Should().BeApproximately(11.4, 0.1);
        hudData.BatteryRemaining.Should().Be(87);
    }

    [Fact]
    public void Should_Get_Primary_Vehicle_HudData()
    {
        // Arrange
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var hudDataService = serviceProvider.GetRequiredService<IVehicleHudDataService>();

        var vehicleId = new VehicleId(1, 1);

        // Add vehicle
        new SimulatedVehicleState
        {
            VehicleId = vehicleId,
            Latitude = 56.1629,
            Longitude = 10.2039,
            Altitude = 100.0,
            BatteryRemaining = 90
        }.ApplyTo(registry);

        // Act
        var primaryHudData = hudDataService.GetPrimaryVehicleHudData();

        // Assert
        primaryHudData.Should().NotBeNull();
        primaryHudData!.VehicleId.Should().Be(vehicleId);
        primaryHudData.Altitude.Should().Be(100.0);
    }
}
