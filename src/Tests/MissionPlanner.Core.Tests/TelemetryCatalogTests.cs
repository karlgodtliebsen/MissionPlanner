using FluentAssertions;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies shared telemetry descriptor projection.</summary>
public sealed class TelemetryCatalogTests
{
    /// <summary>Descriptor keys are stable and unique.</summary>
    [Fact] public void DescriptorKeysAreUnique() => new TelemetryFieldCatalog().Fields.Select(x => x.Key).Should().OnlyHaveUniqueItems();

    /// <summary>Imperial formatting preserves the raw SI value.</summary>
    [Fact]
    public void ProjectionPreservesRawValueDuringConversion()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Unknown, false, null, null, null, null, null, null, null, null)
        { Motion = VehicleMotionState.Empty with { GroundSpeedMetersPerSecond = 10, ObservedAt = now } };
        var descriptor = new TelemetryFieldCatalog().Fields.Single(x => x.Key == "ground-speed");

        var result = new TelemetrySnapshotProjector().Project(descriptor, state, UnitSystem.Imperial, now);

        result.RawValue.Should().Be(10d);
        result.Unit.Should().Be("mph");
        result.Freshness.Should().Be(TelemetryFreshness.Fresh);
    }
}
