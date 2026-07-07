using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Test.Support.Configuration;

namespace MissionPlanner.Core.Tests.IntegrationTests;

/// <summary>
/// Tests for the domain layer implementations.
/// </summary>
public class VehiclesParametersTests
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="VehiclesParametersTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public VehiclesParametersTests(ITestOutputHelper output)
    {
        this.output = output;
        var services = TestConfigurator
            .AddTestConfiguration(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Should_Return_All_Vehicle_Parameters()
    {
        var registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        var vehicleService = serviceProvider.GetRequiredService<IVehicleService>();
        var parameterService = serviceProvider.GetRequiredService<IVehicleParameterService>();
        var parameterRegistry = serviceProvider.GetRequiredService<IVehicleParameterRegistry>();
    }
}
