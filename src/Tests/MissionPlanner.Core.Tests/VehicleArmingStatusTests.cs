using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises retained readiness through session and registry lifetimes.</summary>
public sealed class VehicleArmingStatusTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-16T12:00:00Z");
    private const uint PreArmCheck = 1u << 28;

    /// <summary>Readiness requires supported, enabled pre-arm health; armed overrides it.</summary>
    [Theory]
    [InlineData(false, true, false, VehicleArmingState.DisarmedNotReady)]
    [InlineData(false, true, true, VehicleArmingState.DisarmedReady)]
    [InlineData(true, true, false, VehicleArmingState.Armed)]
    [InlineData(false, false, false, VehicleArmingState.Unknown)]
    public async Task DerivesReadiness(bool armed, bool supported, bool healthy, VehicleArmingState expected)
    {
        var (session, _) = await CreateAsync(armed);
        session.ApplySystemHealth(Health(supported, healthy));
        Assert.Equal(expected, session.State.Arming.State);
    }

    /// <summary>Pre-arm text survives unrelated messages and heartbeats until health recovers.</summary>
    [Fact]
    public async Task RetainsPreArmUntilReady()
    {
        var (session, _) = await CreateAsync();
        Text(session, "PreArm: Compass 1 not healthy");
        Text(session, "Battery healthy");
        session.ApplyHeartbeat(0, 2, 3, 0, 4, 3, Now.AddSeconds(1));
        Assert.Equal("PreArm: Compass 1 not healthy", session.State.Arming.PreArmReason);
        Assert.Equal(VehicleArmingState.DisarmedNotReady, session.State.Arming.State);
        session.ApplySystemHealth(Health(true, true));
        Assert.Null(session.State.Arming.PreArmReason);
        Assert.Equal(VehicleArmingState.DisarmedReady, session.State.Arming.State);
    }

    /// <summary>Arming clears a pre-arm blocker but keeps the latest failed attempt visible.</summary>
    [Fact]
    public async Task ArmedOverridesAndRetainsLastFailure()
    {
        var (session, _) = await CreateAsync();
        Text(session, "PreArm: Compass 1 not healthy");
        Text(session, "Arm: Roll (RC1) is not neutral");
        session.ApplyHeartbeat(0, 2, 3, 128, 4, 3, Now.AddSeconds(1));
        Assert.Equal(VehicleArmingState.Armed, session.State.Arming.State);
        Assert.Null(session.State.Arming.PreArmReason);
        Assert.Equal("Arm: Roll (RC1) is not neutral", session.State.Arming.LastArmFailure);
    }

    /// <summary>Blank prefixes and other components do not replace meaningful arming reasons.</summary>
    [Fact]
    public async Task IgnoresEmptyAndForeignReasons()
    {
        var (session, _) = await CreateAsync();
        Text(session, "PreArm: Compass 1 not healthy");
        Text(session, "PreArm: ");
        session.ApplyStatusText(new VehicleStatusText(session.Id, 1, 42, MavSeverity.Warning, "PreArm: Foreign", Now));
        Assert.Equal("PreArm: Compass 1 not healthy", session.State.Arming.PreArmReason);
    }

    /// <summary>Registry reset clears retained state even for consumers holding the old session.</summary>
    [Fact]
    public async Task ResetClearsRetainedState()
    {
        var (session, registry) = await CreateAsync();
        Text(session, "PreArm: Logging failed");
        Text(session, "Arm: Roll (RC1) is not neutral");
        await registry.Reset(TestContext.Current.CancellationToken);
        Assert.Equal(VehicleArmingStatus.Empty, session.State.Arming);
        Assert.Empty(registry.Vehicles);
    }

    /// <summary>Loss of heartbeat clears status so the HUD cannot claim stale readiness.</summary>
    [Fact]
    public async Task OfflineClearsRetainedState()
    {
        var (session, _) = await CreateAsync();
        Text(session, "Arm: Roll (RC1) is not neutral");
        session.UpdateConnectionState(Now.AddMinutes(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        Assert.Equal(VehicleArmingStatus.Empty, session.State.Arming);
    }

    /// <summary>Generic logging failures retain detailed storage evidence and disabled configuration is explicit.</summary>
    [Fact]
    public async Task OnboardLoggingRetainsStorageCause()
    {
        var (session, _) = await CreateAsync();
        Text(session, "Failed to create log directory /APM/LOGS : ENOSPC");
        Text(session, "PreArm: Logging failed");
        Assert.False(session.State.OnboardLogging.Healthy);
        Assert.True(session.State.OnboardLogging.AffectsArming);
        Assert.Contains("ENOSPC", session.State.OnboardLogging.StorageDetail);
        Assert.Equal("PreArm: Logging failed", session.State.OnboardLogging.LatestMessage);
        var disabled = session.State.OnboardLogging.WithBackend(0);
        Assert.False(disabled.Enabled);
        Assert.False(disabled.AffectsArming);
        Assert.Equal("Disabled", disabled.DisplayState);
        Assert.Equal("Error", session.State.OnboardLogging.WithBackend(1).DisplayState);
    }

    /// <summary>Reported logger recovery clears the active storage error, and disconnect clears evidence.</summary>
    [Fact]
    public async Task LoggerHealthRecoveryAndReset()
    {
        var (session, registry) = await CreateAsync();
        Text(session, "PreArm: Logging failed ENOSPC");
        const uint logging = 1u << 24;
        session.ApplySystemHealth(new(logging, logging, logging, 0, 0, 0, Now));
        Assert.True(session.State.OnboardLogging.Healthy);
        Assert.False(session.State.OnboardLogging.AffectsArming);
        Assert.Null(session.State.OnboardLogging.StorageDetail);
        await registry.Reset(TestContext.Current.CancellationToken);
        Assert.Equal(VehicleOnboardLoggingStatus.Empty, session.State.OnboardLogging);
    }

    /// <summary>Vehicle logging rejection cannot stop an independent PC recording.</summary>
    [Fact]
    public async Task OnboardFailureDoesNotAffectPcRecording()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MissionPlannerIndependentLogs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = Substitute.For<MissionPlanner.Core.ConfigTuning.Planner.IPlannerSettingsService>();
            settings.Current.Returns(new MissionPlanner.Core.ConfigTuning.Planner.PlannerSettings
            {
                Logging = new MissionPlanner.Core.ConfigTuning.Planner.PlannerLoggingSettings { LogDirectory = directory }
            });
            var recorder = new MissionPlanner.Core.Replay.TelemetryRecordingService(settings,
                Substitute.For<IDomainEventHub>(), NullLogger<MissionPlanner.Core.Replay.TelemetryRecordingService>.Instance);
            await using var recording = recorder.Start(new MissionPlanner.MavLink.Services.MavLinkInspectionTap());
            var (session, _) = await CreateAsync();
            Text(session, "PreArm: Logging failed ENOSPC");
            Assert.Equal("Error", session.State.OnboardLogging.DisplayState);
            Assert.Equal("Recording", recorder.Current.State);
            Assert.True(File.Exists(recorder.Current.FilePath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
    private static VehicleSystemHealthObservation Health(bool supported, bool healthy)
    {
        return new(supported ? PreArmCheck : 0, supported ? PreArmCheck : 0, healthy ? PreArmCheck : 0, 0, 0, 0, Now);
    }

    private static void Text(VehicleSession session, string text)
    {
        session.ApplyStatusText(new VehicleStatusText(session.Id, 1, 1, MavSeverity.Warning, text, Now));
    }

    private static async Task<(VehicleSession, VehicleRegistry)> CreateAsync(bool armed = false)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var registry = new VehicleRegistry(Substitute.For<IDomainEventHub>(), clock, NullLogger<VehicleRegistry>.Instance);
        var id = new VehicleId(1, 1);
        await registry.RegisterOrUpdateHeartbeatAsync(id, new TransportEndPoint("test", "arming"), 0, 2, 3,
            armed ? (byte)128 : (byte)0, 4, 3, Now, TestContext.Current.CancellationToken);
        return (registry.GetRequired(id)!, registry);
    }
}
