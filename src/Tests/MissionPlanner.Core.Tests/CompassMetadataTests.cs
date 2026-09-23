using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Parameters.Metadata;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks the existing metadata parser and composition root support semantic Compass setup.</summary>
public sealed class CompassMetadataTests
{
    /// <summary>Only explicit finite defaults are retained.</summary>
    [Theory]
    [InlineData("<field name=\"Default\">1</field>", 1d)]
    [InlineData("<field name=\"Default\">NaN</field>", null)]
    [InlineData("", null)]
    public async Task ParsesExplicitDefaults(string field, double? expected)
    {
        var xml = "<paramfile><libraries><parameters name=\"Compass\"><param name=\"COMPASS_ENABLE\">" + field + "</param></parameters></libraries></paramfile>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var parser = new ParameterMetadataXmlParser(NullLogger<ParameterMetadataXmlParser>.Instance);
        var values = await parser.ParseAsync(stream, VehicleType.ArduCopter, TestContext.Current.CancellationToken);
        Assert.Equal(expected, values["COMPASS_ENABLE"].DefaultValue);
    }

    /// <summary>The existing Compass service remains the feature boundary in DI.</summary>
    [Fact]
    public void UsesExistingServiceRegistration()
    {
        var services = new ServiceCollection();
        services.AddDomainServices(new ConfigurationBuilder().AddInMemoryCollection().Build());
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ICompassConfigurationService));
        Assert.Equal(typeof(CompassConfigurationService), descriptor.ImplementationType);
    }
}
