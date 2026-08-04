using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMissionPlannerFirmwareRegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddFirmwareServices(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<FirmwareOptions>>().Value.Should().NotBeNull();
    }
}
