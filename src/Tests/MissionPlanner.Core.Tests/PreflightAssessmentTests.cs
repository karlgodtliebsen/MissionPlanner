using FluentAssertions;
using MissionPlanner.Core.FlightData.Preflight;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies conservative preflight assessment semantics.</summary>
public sealed class PreflightAssessmentTests
{
    /// <summary>Missing evidence remains explicit and cannot yield an overall pass.</summary>
    [Fact]
    public void MissingEvidenceIsNotAnImplicitPass()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3,
            VehicleConnectionState.Online, now, VehicleMode.Unknown, false,
            null, null, null, null, null, null, null, null);

        var assessment = new PreflightAssessmentService().Assess(state, now);

        assessment.Checks.Should().Contain(x => x.Status == PreflightCheckStatus.NotAvailable);
        assessment.OverallStatus.Should().NotBe(PreflightCheckStatus.Pass);
        assessment.Checks.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Evidence.Source));
    }

    /// <summary>An old heartbeat is surfaced as stale and dominates unavailable evidence.</summary>
    [Fact]
    public void StaleHeartbeatIsActionableOverallSeverity()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3,
            VehicleConnectionState.Online, now.AddSeconds(-20), VehicleMode.Unknown, false,
            null, null, null, null, null, null, null, null);

        var assessment = new PreflightAssessmentService().Assess(state, now);

        assessment.Checks.Single(x => x.Key == "connection").Status.Should().Be(PreflightCheckStatus.Stale);
        assessment.OverallStatus.Should().Be(PreflightCheckStatus.Stale);
    }
}
