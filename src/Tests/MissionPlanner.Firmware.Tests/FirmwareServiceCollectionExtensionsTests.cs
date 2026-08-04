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

    [Fact]
    public void FirmwareHttpClientHasProductIdentityAndBoundedTimeout()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFirmwareServices(new ConfigurationBuilder().Build(), options =>
        {
            options.HttpRequestTimeout = TimeSpan.FromSeconds(37);
            options.HttpUserAgent = "MissionPlanner.Tests/1.0";
        });
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(FirmwareHttpClient.Name);
        client.Timeout.Should().Be(TimeSpan.FromSeconds(37));
        client.DefaultRequestHeaders.UserAgent.ToString().Should().Be("MissionPlanner.Tests/1.0");
    }
}
