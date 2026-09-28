using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.FlightData.Preflight;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Battery readiness never substitutes percentage for voltage or FC evidence.</summary>
public sealed class BatteryReadinessTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Dictionary<string, float> Parameters = new()
    {
        ["BATT_MONITOR"] = 4, ["BATT_ARM_VOLT"] = 11, ["BATT_LOW_VOLT"] = 11.5f,
        ["BATT_CRT_VOLT"] = 10, ["BATT2_MONITOR"] = 4, ["BATT2_ARM_VOLT"] = 11
    };

    /// <summary>Separates low, invalid, missing and healthy voltage regardless of reported percentage.</summary>
    [Theory]
    [InlineData(0.02, PreflightCheckStatus.Fail)]
    [InlineData(0d, PreflightCheckStatus.Fail)]
    [InlineData(-1d, PreflightCheckStatus.NotAvailable)]
    [InlineData(double.NaN, PreflightCheckStatus.NotAvailable)]
    [InlineData(double.PositiveInfinity, PreflightCheckStatus.NotAvailable)]
    [InlineData(null, PreflightCheckStatus.NotAvailable)]
    [InlineData(11.2, PreflightCheckStatus.Warning)]
    [InlineData(12.1, PreflightCheckStatus.Pass)]
    public void VoltageIsIndependentOfPercentage(double? voltage, PreflightCheckStatus expected)
    {
        var result = Assess(voltage);
        Assert.Equal(expected, result.Status);
        Assert.Contains("99% (not verified state of charge)", result.Summary);
    }

    /// <summary>Fresh blockers override telemetry; silence cannot establish recovery.</summary>
    [Fact]
    public void EvidenceRequiresPositiveRecoveryAndIsInstanceScoped()
    {
        var blocker = new BatteryArmingEvidence(1, "PreArm: Battery 1 below minimum arming voltage", Now.AddSeconds(-1));
        Assert.Equal(PreflightCheckStatus.Fail, Assess(12, [blocker]).Status);
        Assert.Equal(PreflightCheckStatus.Pass, Assess(12, [blocker], number: 2).Status);
        Assert.Equal(PreflightCheckStatus.NotAvailable, Assess(12, [blocker with { ObservedAt = Now.AddMinutes(-2) }]).Status);
        var recovered = Assess(12, [blocker with { ResolvedAt = Now }]);
        Assert.Equal(PreflightCheckStatus.Pass, recovered.Status);
        Assert.Contains("Previous FC blocker resolved", recovered.Summary);
    }

    /// <summary>No threshold, disabled monitoring, old samples and disconnects are different outcomes.</summary>
    [Fact]
    public void ConfigurationAndFreshnessAreExplicit()
    {
        Assert.Equal(PreflightCheckStatus.NotAvailable, Assess(12, settings: new() { ["BATT_MONITOR"] = 4 }).Status);
        Assert.Equal(PreflightCheckStatus.NotAvailable, Assess(12, settings: new()).Status);
        Assert.Equal(PreflightCheckStatus.NotApplicable, Assess(null, settings: new() { ["BATT_MONITOR"] = 0 }).Status);
        Assert.Equal(PreflightCheckStatus.Stale, Assess(12, observed: Now.AddSeconds(-11)).Status);
        Assert.Equal(PreflightCheckStatus.Stale, Assess(12, online: false).Status);
        Assert.Equal(PreflightCheckStatus.Stale, Assess(12, observed: Now.AddSeconds(1)).Status);
    }

    private static PreflightCheckResult Assess(double? voltage, BatteryArmingEvidence[]? evidence = null,
        int number = 1, Dictionary<string, float>? settings = null, DateTimeOffset? observed = null, bool online = true)
    {
        var parameters = settings ?? Parameters;
        return BatteryReadiness.Assess(number, new((byte)(number - 1), voltage, null, null, null, 99, observed ?? Now),
            name => parameters.TryGetValue(name, out var value) ? value : null, evidence ?? [], online, Now);
    }
}
