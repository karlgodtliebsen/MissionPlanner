using Microsoft.Extensions.Logging;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Tests.Configuration;

namespace MissionPlanner.Tests;

/// <summary>
/// Tests for the domain layer implementations.
/// </summary>
public class ConfigurationTests
{
    private readonly ITestOutputHelper output;
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public ConfigurationTests(ITestOutputHelper output)
    {
        this.output = output;

        var services = TestConfigurator
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);

        serviceProvider = services.BuildServiceProvider();
    }


    /// <summary>
    /// Tests the configuration setup.
    /// </summary>
    [Fact]
    public void TestConfiguration_Should_Be_Configured()
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ConfigurationTests>>();
        logger.LogInformation("Running TestConfiguration_Should_Be_Configured");


        serviceProvider.GetRequiredService<IDomainFactory>();
        serviceProvider.GetRequiredService<IDomainEventHub>();
        serviceProvider.GetRequiredService<IDateTimeProvider>();
        serviceProvider.GetRequiredService<IServiceFactory>();
        serviceProvider.GetRequiredService<IFactory>();
        Assert.True(true);
    }
}
