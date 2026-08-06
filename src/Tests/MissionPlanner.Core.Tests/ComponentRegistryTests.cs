using FluentAssertions;
using MissionPlanner.Core.FlightData.Components;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies component-scoped transponder and traffic storage.</summary>
public sealed class ComponentRegistryTests
{
    /// <summary>Traffic is deduplicated by vehicle and ICAO identity.</summary>
    [Fact]
    public void TrafficUpdatesInPlaceByIcao()
    {
        var registry = new VehicleComponentRegistry(); var endpoint = new TransportEndPoint("test"); var now = DateTimeOffset.UtcNow;
        registry.Observe(new AdsbVehicleMessage(1, 100, endpoint, 42, 1, 2, 0, 1000, 0, 0, 0, "ONE", 0, 0, 1, 1200, now));
        registry.Observe(new AdsbVehicleMessage(1, 101, endpoint, 42, 3, 4, 0, 2000, 0, 0, 0, "TWO", 0, 0, 1, 1200, now.AddSeconds(1)));
        registry.GetTraffic(1, now.AddSeconds(1)).Should().ContainSingle().Which.Callsign.Should().Be("TWO");
    }

    /// <summary>Squawk validation requires exactly four octal digits.</summary>
    [Theory] [InlineData("1200", true)] [InlineData("7700", true)] [InlineData("8888", false)] [InlineData("120", false)]
    public void SquawkValidationIsOctal(string value, bool expected) => TransponderValidation.IsSquawk(value).Should().Be(expected);
}
