using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services;
using MissionPlanner.Simulator;
using MissionPlanner.Test.Configuration;

namespace MissionPlanner.Test;

/// <summary>
/// Tests for the domain layer implementations.
/// </summary>
public class DomainVehiclesTests
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainVehiclesTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public DomainVehiclesTests(ITestOutputHelper output)
    {
        this.output = output;
        IServiceCollection services = TestConfigurator
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }

    /// <summary>
    /// Maps a <see cref="VehicleMode"/> to the corresponding ArduCopter custom mode.
    /// </summary>
    /// <param name="mode">The vehicle mode to map.</param>
    /// <param name="expected">The expected ArduCopter custom mode value.</param>
    [Theory]
    [InlineData(VehicleMode.Stabilize, 0u)]
    [InlineData(VehicleMode.AltHold, 2u)]
    [InlineData(VehicleMode.Guided, 4u)]
    [InlineData(VehicleMode.Loiter, 5u)]
    [InlineData(VehicleMode.Rtl, 6u)]
    [InlineData(VehicleMode.Land, 9u)]
    public void Should_Map_VehicleMode_To_ArduCopter_CustomMode(VehicleMode mode, uint expected)
    {
        var actual = ArduCopterModeMapper.ToCustomMode(mode);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Should_Return_All_Simulated_VehiclesAsync()
    {
        IVehicleRegistry registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        new SimulatedVehicleState
        {
            VehicleId = new VehicleId(1, 1),
            Latitude = 56.1629,
            Longitude = 10.2039,
            Altitude = 12.5,
            BatteryRemaining = 87,
            BatteryVoltage = 11.4f
        }.ApplyTo(registry);

        IReadOnlyCollection<VehicleState> vehicles = vehicleService.GetVehicles();

        VehicleState vehicle = Assert.Single(vehicles);

        Assert.Equal(new VehicleId(1, 1), vehicle.VehicleId);
        Assert.Equal(56.1629, vehicle.Latitude);
        Assert.Equal(10.2039, vehicle.Longitude);
        Assert.Equal(12.5, vehicle.Altitude);
        Assert.Equal(87, vehicle.BatteryRemaining);
        Assert.Equal(11.4f, vehicle.BatteryVoltage);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Should_Return_Specific_Simulated_VehicleAsync()
    {
        IVehicleRegistry registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        new SimulatedVehicleState
        {
            VehicleId = new VehicleId(1, 1),
            Roll = 0.1,
            Pitch = -0.2,
            Yaw = 1.5
        }.ApplyTo(registry);

        VehicleState? vehicle = vehicleService.GetVehicleState(new VehicleId(1, 1));

        Assert.Equal(new VehicleId(1, 1), vehicle.VehicleId);
        Assert.Equal(0.1, vehicle.Roll);
        Assert.Equal(-0.2, vehicle.Pitch);
        Assert.Equal(1.5, vehicle.Yaw);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Should_Return_Null_When_Getting_Unknown_Vehicle()
    {
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        VehicleState? vehicle = vehicleService.GetVehicleState(new VehicleId(99, 1));
        Assert.Null(vehicle);
    }
}