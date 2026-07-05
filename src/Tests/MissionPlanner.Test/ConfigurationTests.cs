using Domain.Library.Factory.Domain.Abstractions;

using Microsoft.Extensions.Logging;

using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Services;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Test.Configuration;
using MissionPlanner.Transport;

namespace MissionPlanner.Test;

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

        IServiceCollection services = TestConfigurator
            .AddTestConfiguration()
            .AddDefaultTestLogging(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }


    [Fact]
    public async Task TestConfigurationSetupAsync()
    {
        ILogger<ConfigurationTests> logger = serviceProvider.GetRequiredService<ILogger<ConfigurationTests>>();
        IMavLinkFrameParser parser = serviceProvider.GetRequiredService<IMavLinkFrameParser>();
        IMavLinkTransport transport = serviceProvider.GetRequiredService<IMavLinkTransport>();
        IMavLinkClient mavLinkClient = serviceProvider.GetRequiredService<IMavLinkClient>();

        IMavLinkFrameParser frameParser = serviceProvider.GetRequiredService<IMavLinkFrameParser>();
        IMavLinkCommandEncoder commandEncoder = serviceProvider.GetRequiredService<IMavLinkCommandEncoder>();
        IMavLinkCrcExtraProvider crcExtraProvider = serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>();
        IMavLinkMessageDecoder messageDecoder = serviceProvider.GetRequiredService<IMavLinkMessageDecoder>();

        IMavLinkConnection connection = serviceProvider.GetRequiredService<IMavLinkConnection>();
        IVehicleMessagePump messagePump = serviceProvider.GetRequiredService<IVehicleMessagePump>();
        ICommandAckTracker commandAckTracker = serviceProvider.GetRequiredService<ICommandAckTracker>();
        IVehicleCommandService commandService = serviceProvider.GetRequiredService<IVehicleCommandService>();
        IVehicleRegistry registry = serviceProvider.GetRequiredService<IVehicleRegistry>();
        IVehicleService vehicleService = serviceProvider.GetRequiredService<IVehicleService>();

        IDomainFactory domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();

        // await using var simulator = new FakeMavLinkVehicle2(frameParser, crcExtraProvider, "127.0.0.1", 14550, 14551, TimeSpan.FromMilliseconds(100));

        logger.LogInformation("Running TestConfigurationSetupAsync");
    }
}