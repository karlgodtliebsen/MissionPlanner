using FluentAssertions;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Tests;

public sealed class OptionalHardwareTabCatalogTests
{
    [Fact]
    public void CatalogHasUniqueDeterministicKeys()
    {
        var catalog = new OptionalHardwareTabCatalog();
        catalog.Tabs.Select(item => item.Key).Should().OnlyHaveUniqueItems();
        catalog.Tabs.Select(item => item.Order).Should().BeInAscendingOrder();
        catalog.Tabs.Should().HaveCount(19);
    }

    [Fact]
    public void DisconnectedCatalogExposesOnlyStandaloneTools()
    {
        var states = new OptionalHardwareTabCatalog().Evaluate(false, null, new Dictionary<string, VehicleParameter>());
        states.Where(item => item.IsAvailable).Should().OnlyContain(item => item.Descriptor.SupportsOffline);
        states.Count(item => item.IsAvailable).Should().Be(5);
    }
}
