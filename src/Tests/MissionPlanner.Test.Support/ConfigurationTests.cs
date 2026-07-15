using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Decoding.Utils;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Test.Support.Configuration;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Test.Support;

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
            .AddTestConfiguration(output);

        serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();
    }


    /// <summary>
    /// Tests the configuration setup.
    /// </summary>
    [Fact]
    public void TestConfigurationSetup()
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ConfigurationTests>>();
        logger.LogInformation("Running TestConfigurationSetupAsync");
        serviceProvider.GetRequiredService<IMavLinkFrameParser>();
        serviceProvider.GetRequiredService<IMavLinkTransport>();
        serviceProvider.GetRequiredService<IMavLinkClient>();

        serviceProvider.GetRequiredService<IMavLinkFrameParser>();
        serviceProvider.GetRequiredService<IMavLinkCommandEncoder>();
        serviceProvider.GetRequiredService<IMavLinkCrcExtraProvider>();
        serviceProvider.GetRequiredService<IMavLinkMessageDecodeHandler>();
        serviceProvider.GetRequiredService<MavLinkMessageDecoders>();

        serviceProvider.GetRequiredService<IMavLinkConnection>();
        serviceProvider.GetRequiredService<IVehicleMessagePump>();
        serviceProvider.GetRequiredService<ICommandAckTracker>();
        serviceProvider.GetRequiredService<IVehicleCommandService>();
        serviceProvider.GetRequiredService<IVehicleRegistry>();
        serviceProvider.GetRequiredService<IVehicleService>();
        serviceProvider.GetRequiredService<IVehicleConnectionMonitor>();

        serviceProvider.GetRequiredService<IStatusTextHandler>();
        serviceProvider.GetRequiredService<ICommandAckTracker>();
        serviceProvider.GetRequiredService<IParamValueVehicleHandler>();

        serviceProvider.GetRequiredService<IVehicleMessageDispatcher>();

        serviceProvider.GetRequiredService<IVehicleMessageHandler>();
        serviceProvider.GetRequiredService<IVehicleCommandPolicy>();

        serviceProvider.GetRequiredService<ISerialPortDiscoveryService>();
        serviceProvider.GetRequiredService<IVehicleConnectionService>();
        serviceProvider.GetRequiredService<IVehicleHudDataService>();

        var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();

        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();
        var serviceFactory = serviceProvider.GetRequiredService<IServiceFactory>();

        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        var portName = "COM 10";
        var baudRate = 115200;

        // Create serial transport
        var transport = domainFactory.Create<ISerialMavLinkTransport, string, int>(portName, baudRate);
        Assert.NotNull(transport);
        // Create MAVLink client
        var client = domainFactory.Create<IMavLinkClient, ISerialMavLinkTransport>(transport);
        Assert.NotNull(client);

        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        Assert.NotNull(connection);
    }
}
