using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMissionPlannerFirmwareRegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddMissionPlannerFirmware();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<FirmwareOptions>>().Value.Should().NotBeNull();
    }
}
