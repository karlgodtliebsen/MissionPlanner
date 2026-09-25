using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies live RC projection and the radio endpoint-calibration state machine.</summary>
public sealed class RadioSetupTests
{
    private static readonly VehicleId vehicleId = new(1, 1);

    /// <summary>Verifies channel projection maps pilot functions, endpoints, and normalized travel.</summary>
    [Fact]
    public void LiveChannelsProjectFunctionsAndTravel()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "RC3_MIN", 1000);
        Store(registry, "RC3_MAX", 2000);
        Store(registry, "RC3_TRIM", 1500);
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1100, 1500], now));
        var service = CreateService(context, registry, now);

        var view = service.GetLiveChannels(vehicleId);

        view.IsStale.Should().BeFalse();
        view.Channels.Should().HaveCount(4);
        var throttle = view.Channels.Single(channel => channel.Number == 3);
        throttle.FunctionName.Should().Be("Throttle");
        throttle.Normalized.Should().BeApproximately(-0.8, 0.001);
    }

    /// <summary>Verifies default pilot assignments produce an honest AETR summary.</summary>
    [Fact]
    public void DefaultMapSummaryIsAetr()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var service = CreateService(context, new VehicleParameterRegistry(), now);

        service.GetLiveChannels(vehicleId).ChannelMapSummary.Should().Be("AETR");
    }

    /// <summary>Verifies RCMAP assignments, rather than channel-number assumptions, produce TAER.</summary>
    [Fact]
    public void RemappedPilotFunctionsProduceTaer()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "RCMAP_THROTTLE", 1);
        Store(registry, "RCMAP_ROLL", 2);
        Store(registry, "RCMAP_PITCH", 3);
        Store(registry, "RCMAP_YAW", 4);
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1000, 1500, 1500, 1500], now));
        var view = CreateService(context, registry, now).GetLiveChannels(vehicleId);

        view.ChannelMapSummary.Should().Be("TAER");
        view.Channels.Single(channel => channel.Number == 1).FunctionName.Should().Be("Throttle");
        view.Channels.Single(channel => channel.Number == 2).FunctionName.Should().Be("Roll");
    }

    /// <summary>Verifies a noncompact map falls back to explicit assignments.</summary>
    [Fact]
    public void NonstandardMapUsesExplicitSummary()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "RCMAP_ROLL", 6);
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500, 1500, 1500], now));

        var summary = CreateService(context, registry, now).GetLiveChannels(vehicleId).ChannelMapSummary;

        summary.Should().Contain("Roll CH6").And.Contain("Pitch CH2");
    }

    /// <summary>Verifies dead zone follows the mapped centered function.</summary>
    [Fact]
    public void DeadZoneFollowsMappedPilotChannel()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "RCMAP_ROLL", 6);
        Store(registry, "RC6_DZ", 35);
        Store(registry, "RC1_DZ", 99);
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500, 1500, 1500], now));
        var view = CreateService(context, registry, now).GetLiveChannels(vehicleId);

        view.Channels.Single(channel => channel.Number == 6).DeadZone.Should().Be(35);
        view.Channels.Single(channel => channel.Number == 1).DeadZone.Should().Be(99);
    }

    /// <summary>Verifies stale RC telemetry is reported and cannot be mistaken for live input.</summary>
    [Fact]
    public void StaleTelemetryIsReported()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now - TimeSpan.FromSeconds(5)));
        var service = CreateService(context, new VehicleParameterRegistry(), now);

        service.GetLiveChannels(vehicleId).IsStale.Should().BeTrue();
    }

    /// <summary>Verifies unavailable RC RSSI remains unknown instead of becoming a false zero.</summary>
    [Fact]
    public void UnknownReceiverRssiRemainsUnknown()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500], now));

        var view = CreateService(context, new VehicleParameterRegistry(), now).GetLiveChannels(vehicleId);

        view.RssiPercent.Should().BeNull();
        view.SignalState.Should().Be(RadioSignalState.Live);
    }

    /// <summary>Verifies capture, Review trim sampling, and confirmed writes are distinct.</summary>
    [Fact]
    public async Task CalibrationCapturesExtremesReviewsFreshTrimAndWritesAllValues()
    {
        var registry = new VehicleParameterRegistry();
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, registry, now, written);

        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        service.Current.State.Should().Be(RadioCalibrationState.Capturing);
        context.SetState(StateWithChannels([1000, 1000, 1000, 1000], now));
        context.SetState(StateWithChannels([2000, 2000, 2000, 2000], now));
        var review = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);

        review.State.Should().Be(RadioCalibrationState.Review);
        written.Should().BeEmpty("entering Review must not write parameters");
        context.SetState(StateWithChannels([1500, 1500, 1000, 1500], now));
        var result = await service.CompleteAsync(TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        service.Current.State.Should().Be(RadioCalibrationState.Success);
        written.Should().Contain("RC1_MIN").And.Contain("RC1_MAX").And.Contain("RC3_MIN").And.Contain("RC3_MAX");
        written.Should().Contain("RC1_TRIM", "centered sticks use the fresh Review sample");
        written.Should().Contain("RC3_TRIM", "conventional throttle records the fresh low-throttle Review sample");
        registry.GetParameter(vehicleId, "RC1_TRIM")!.Value.Should().Be(1500);
        registry.GetParameter(vehicleId, "RC3_TRIM")!.Value.Should().Be(1000);
    }

    /// <summary>Verifies insufficient stick movement blocks a confirmed write.</summary>
    [Fact]
    public async Task InsufficientMovementBlocksWrite()
    {
        var registry = new VehicleParameterRegistry();
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, registry, now, written);

        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetState(StateWithChannels([1520, 1520, 1520, 1520], now));
        var review = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);

        review.State.Should().Be(RadioCalibrationState.Capturing);
        service.Current.State.Should().Be(RadioCalibrationState.Capturing);
        service.Current.Issues.Should().Contain(issue => issue.Severity == RadioIssueSeverity.Hazard);
        written.Should().BeEmpty();
    }

    /// <summary>Validation feedback survives telemetry until a successful explicit recheck.</summary>
    [Fact]
    public async Task EndpointIssuesPersistUntilCaptureIsRechecked()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        using var service = CreateService(context, new VehicleParameterRegistry(), now, written);
        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetState(StateWithChannels([1520, 1520, 1520, 1520], now));
        var failed = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);

        failed.FailureReason.Should().Contain("4 channel issues").And.Contain("No parameters were written");
        failed.Instruction.Should().Contain("Capture is still running");
        var rollIssue = failed.Captures.Single(item => item.Number == 1).Issues.Single();
        rollIssue.Message.Should().Contain("Channel 1 (Roll)").And.Contain("at least 200 us");
        context.SetState(StateWithChannels([1000, 1000, 1000, 1000], now));
        context.SetState(StateWithChannels([2000, 2000, 2000, 2000], now));

        service.Current.Captures.Single(item => item.Number == 1).Issues.Should().Contain(rollIssue);
        service.Current.Issues.Should().Equal(failed.Issues);
        var review = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);
        review.State.Should().Be(RadioCalibrationState.Review);
        review.FailureReason.Should().BeNull();
        review.Issues.Should().BeEmpty();
        review.Captures.Should().OnlyContain(item => item.Issues.Count == 0);
        written.Should().BeEmpty();
    }

    /// <summary>Channel 10 feedback must not appear on channel 1.</summary>
    [Fact]
    public async Task StationaryAuxiliaryFeedbackBelongsToTheExactChannel()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500, 1500], now));
        var registry = new VehicleParameterRegistry();
        Store(registry, "RC10_OPTION", 153);
        using var service = CreateService(context, registry, now);
        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetState(StateWithChannels([1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1000, 1500], now));
        context.SetState(StateWithChannels([2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 1500], now));

        var result = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);

        result.Captures.Single(item => item.Number == 1).Issues.Should().BeEmpty();
        result.Captures.Single(item => item.Number == 10).Issues.Single().Message
            .Should().Contain("did not move").And.Contain("transmitter channel assignment");
    }

    /// <summary>Verifies endpoint extrema freeze while live candidate trims continue updating in Review.</summary>
    [Fact]
    public async Task ReviewFreezesEndpointsAndUpdatesLiveCandidateTrim()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var service = CreateService(context, new VehicleParameterRegistry(), now);
        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetState(StateWithChannels([1000, 1000, 1000, 1000], now));
        context.SetState(StateWithChannels([2000, 2000, 2000, 2000], now));
        await service.FinishCaptureAsync(TestContext.Current.CancellationToken);

        context.SetState(StateWithChannels([1500, 1500, 1000, 1500], now));

        var roll = service.Current.Captures.Single(capture => capture.Number == 1);
        roll.Minimum.Should().Be(1000);
        roll.Maximum.Should().Be(2000);
        roll.Current.Should().Be(1500);
        roll.CandidateTrim.Should().Be(1500);
    }

    /// <summary>Verifies writing rejects stale Review telemetry without sending parameters.</summary>
    [Fact]
    public async Task StaleReviewSampleCannotWrite()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, new VehicleParameterRegistry(), now, written);
        await CaptureValidEndpointsAsync(service, context, now);
        context.SetState(StateWithChannels([1500, 1500, 1000, 1500], now - TimeSpan.FromSeconds(5)));

        var result = await service.CompleteAsync(TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Fresh RC input");
        written.Should().BeEmpty();
    }

    /// <summary>Verifies a centered trim at an endpoint is rejected before any write.</summary>
    [Fact]
    public async Task CenteredAxisTrimNearEndpointCannotWrite()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, new VehicleParameterRegistry(), now, written);
        await CaptureValidEndpointsAsync(service, context, now);
        context.SetState(StateWithChannels([1000, 1500, 1000, 1500], now));

        var result = await service.CompleteAsync(TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        service.Current.State.Should().Be(RadioCalibrationState.Review);
        written.Should().BeEmpty();
    }

    /// <summary>Verifies armed state prevents entering the destructive stage.</summary>
    [Fact]
    public async Task ArmedVehicleCannotWriteReview()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, new VehicleParameterRegistry(), now, written);
        await CaptureValidEndpointsAsync(service, context, now);
        context.SetState(StateWithChannels([1500, 1500, 1000, 1500], now) with
        {
            Flight = context.State!.Flight with
            {
                IsArmed = true
            }
        });

        var result = await service.CompleteAsync(TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Disarm");
        written.Should().BeEmpty();
    }

    /// <summary>Verifies a fresh trim outside captured travel is rejected before writing.</summary>
    [Fact]
    public async Task CandidateTrimOutsideCapturedRangeCannotWrite()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, new VehicleParameterRegistry(), now, written);
        await CaptureValidEndpointsAsync(service, context, now);
        context.SetState(StateWithChannels([2200, 1500, 1000, 1500], now));

        var result = await service.CompleteAsync(TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Trim candidates");
        written.Should().BeEmpty();
    }

    /// <summary>Verifies cancellation from Review preserves the no-write guarantee.</summary>
    [Fact]
    public async Task CancellingReviewWritesNothing()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, new VehicleParameterRegistry(), now, written);
        await CaptureValidEndpointsAsync(service, context, now);

        await service.CancelAsync(TestContext.Current.CancellationToken);

        service.Current.State.Should().Be(RadioCalibrationState.Cancelled);
        written.Should().BeEmpty();
    }

    /// <summary>Verifies a disconnect in Review prevents the destructive stage.</summary>
    [Fact]
    public async Task DisconnectInReviewPreventsWrite()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        var service = CreateService(context, new VehicleParameterRegistry(), now, written);
        await CaptureValidEndpointsAsync(service, context, now);
        context.SetOnline(false);

        var action = () => service.CompleteAsync(TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
        service.Current.State.Should().Be(RadioCalibrationState.Disconnected);
        written.Should().BeEmpty();
    }

    /// <summary>Verifies failed parameter confirmation retains the exact failed parameter diagnostic.</summary>
    [Fact]
    public async Task FailedReadbackReportsParameter()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var service = CreateService(context, new VehicleParameterRegistry(), now, confirmWrites: false);
        await CaptureValidEndpointsAsync(service, context, now);
        context.SetState(StateWithChannels([1500, 1500, 1000, 1500], now));

        var result = await service.CompleteAsync(TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("RC1_MIN");
    }

    /// <summary>Verifies reverse-capable throttle uses centered trim instead of conventional low trim.</summary>
    [Fact]
    public async Task ReversibleThrottleUsesCenteredPolicy()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "THR_MIN", -100);
        var now = DateTimeOffset.UtcNow;
        var rover = StateWithChannels([1500, 1500, 1500, 1500], now);
        rover = rover with
        {
            Identity = rover.Identity with
            {
                Firmware = rover.Identity.Firmware with
                {
                    Family = MissionPlanner.Firmware.FirmwareFamily.Rover
                }
            }
        };
        var context = new TestActiveVehicleContext(rover);
        var service = CreateService(context, registry, now);
        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetState(rover with
        {
            Radio = rover.Radio with
            {
                ChannelsRaw = [1000, 1000, 1000, 1000]
            }
        });
        context.SetState(rover with
        {
            Radio = rover.Radio with
            {
                ChannelsRaw = [2000, 2000, 2000, 2000]
            }
        });

        var review = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);

        review.Captures.Single(capture => capture.Number == 3).TrimPolicy.Should().Be(RadioTrimPolicy.Centered);
        review.Instruction.Should().Contain("reversible throttle");
    }

    /// <summary>Verifies duplicate RCMAP assignments are detected before calibration.</summary>
    [Fact]
    public void DuplicatePilotMappingIsReportedFromSourceAssignments()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "RCMAP_ROLL", 1);
        Store(registry, "RCMAP_PITCH", 1);
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var service = CreateService(context, registry, now);

        service.GetLiveChannels(vehicleId).Issues.Should().Contain(issue => issue.Message.Contains("Multiple pilot functions"));
    }

    /// <summary>Verifies the ordinary default AETR assignment has no duplicate warning.</summary>
    [Fact]
    public void DefaultPilotMappingHasNoDuplicateIssue()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var service = CreateService(context, new VehicleParameterRegistry(), now);

        service.GetLiveChannels(vehicleId).Issues.Should().NotContain(issue => issue.Message.Contains("Multiple pilot functions"));
    }

    /// <summary>Verifies a disconnect during capture leaves a recoverable state.</summary>
    [Fact]
    public async Task DisconnectDuringCaptureIsRecoverable()
    {
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500], now));
        var service = CreateService(context, new VehicleParameterRegistry(), now);

        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetOnline(false);

        service.Current.State.Should().Be(RadioCalibrationState.Disconnected);
        service.Reset();
        service.Current.State.Should().Be(RadioCalibrationState.NotStarted);
    }

    /// <summary>Unused advertised channels do not block calibration and are never written.</summary>
    [Fact]
    public async Task SixteenChannelsOnlyWriteAssignedFive()
    {
        var now = DateTimeOffset.UtcNow;
        var registry = new VehicleParameterRegistry();
        Store(registry, "FLTMODE_CH", 5);
        var context = new TestActiveVehicleContext(StateWithChannels(Enumerable.Repeat((ushort)1500, 16).ToArray(), now));
        var written = new List<string>();
        using var service = CreateService(context, registry, now, written);
        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        foreach (var value in new ushort[] { 1000, 2000 })
        {
            context.SetState(StateWithChannels(Enumerable.Range(1, 16).Select(channel => channel <= 5 ? value : (ushort)1500).ToArray(), now));
        }
        var review = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RadioCalibrationState.Review, review.State);
        Assert.Empty(review.Captures.Single(capture => capture.Number == 12).Issues);
        var neutral = Enumerable.Repeat((ushort)1500, 16).ToArray();
        neutral[2] = 1000;
        context.SetState(StateWithChannels(neutral, now));
        Assert.True((await service.CompleteAsync(TestContext.Current.CancellationToken)).Success);
        Assert.Equal(15, written.Count);
        Assert.All(written, name => Assert.Contains(name, Enumerable.Range(1, 5).SelectMany(channel =>
            new[] { $"RC{channel}_MIN", $"RC{channel}_MAX", $"RC{channel}_TRIM" })));
    }

    /// <summary>Every required or used channel must move, including remapped flight-mode controls.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(8, true)]
    public async Task StaticAssignedChannelBlocksReview(int stationaryChannel, bool flightMode)
    {
        var now = DateTimeOffset.UtcNow;
        var registry = new VehicleParameterRegistry();
        if (flightMode)
        {
            Store(registry, "FLTMODE_CH", 8);
        }
        var context = new TestActiveVehicleContext(StateWithChannels(Enumerable.Repeat((ushort)1500, 16).ToArray(), now));
        using var service = CreateService(context, registry, now);
        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        foreach (var value in new ushort[] { 1000, 2000 })
        {
            context.SetState(StateWithChannels(Enumerable.Range(1, 16).Select(channel => channel == stationaryChannel ? (ushort)1500 : value).ToArray(), now));
        }
        var result = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RadioCalibrationState.Capturing, result.State);
        Assert.Contains(result.Issues, issue => issue.Message.Contains($"Channel {stationaryChannel} "));
    }

    /// <summary>A newly assigned static channel is revalidated immediately before writes.</summary>
    [Fact]
    public async Task ChangedAssignmentCannotBypassValidation()
    {
        var now = DateTimeOffset.UtcNow;
        var registry = new VehicleParameterRegistry();
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1500, 1500, 1500], now));
        var written = new List<string>();
        using var service = CreateService(context, registry, now, written);
        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetState(StateWithChannels([1000, 1000, 1000, 1000, 1500], now));
        context.SetState(StateWithChannels([2000, 2000, 2000, 2000, 1500], now));
        await service.FinishCaptureAsync(TestContext.Current.CancellationToken);
        Store(registry, "RC5_OPTION", 153);
        context.SetState(StateWithChannels([1500, 1500, 1000, 1500, 1500], now));
        Assert.False((await service.CompleteAsync(TestContext.Current.CancellationToken)).Success);
        Assert.Empty(written);
    }

    /// <summary>Neutral assessments use configured trim and dead-zone rather than nominal center.</summary>
    [Theory]
    [InlineData(1512, 1500, 20, true, 12)]
    [InlineData(1587, 1500, 20, false, 87)]
    [InlineData(1587, 1587, 20, true, 0)]
    public void NeutralUsesConfiguredValues(int center, int trim, int deadZone, bool neutral, int error)
    {
        var diagnostic = new RadioNeutralDiagnostic(center, center, trim, deadZone, 1076, 2011, false);
        Assert.Equal(neutral, diagnostic.NeutralAllowed);
        Assert.Equal(!neutral, diagnostic.CenterOutsideDeadZone);
        Assert.Equal(error, diagnostic.CenterError);
        Assert.Equal(2011 + 1076 - 2 * center, diagnostic.Asymmetry);
    }

    /// <summary>Fresh live input and parameter updates immediately change the diagnostic projection.</summary>
    [Fact]
    public void NeutralProjectionTracksLiveInputAndTrim()
    {
        var now = DateTimeOffset.UtcNow;
        var registry = new VehicleParameterRegistry();
        Store(registry, "RC4_TRIM", 1500);
        Store(registry, "RC4_DZ", 20);
        var context = new TestActiveVehicleContext(StateWithChannels([1500, 1500, 1000, 1512], now));
        using var service = CreateService(context, registry, now);
        RadioNeutralDiagnostic? Read() => service.GetLiveChannels(vehicleId).Channels.Single(channel => channel.Number == 4).NeutralDiagnostic;
        Assert.True(Read()!.NeutralAllowed);
        context.SetState(StateWithChannels([1500, 1500, 1000, 1587], now));
        Assert.False(Read()!.NeutralAllowed);
        Store(registry, "RC4_TRIM", 1587);
        Assert.True(Read()!.NeutralAllowed);
        context.SetState(StateWithChannels([1500, 1500, 1000, 1587], now.AddSeconds(-10)));
        Assert.Null(Read()!.NeutralAllowed);
    }

    /// <summary>Missing downloaded parameters cannot produce a false neutral assessment.</summary>
    [Fact]
    public void UnknownTrimOrDeadZoneRemainsUnknown()
    {
        Assert.Null(new RadioNeutralDiagnostic(1500, null, null, null, null, null, false).NeutralAllowed);
    }

    /// <summary>Earlier switch movement and assignment never satisfy a new endpoint capture.</summary>
    [Fact]
    public async Task ArmSwitchMovementMustOccurWithinCurrentCapture()
    {
        var now = DateTimeOffset.UtcNow;
        var registry = new VehicleParameterRegistry();
        Store(registry, "FLTMODE_CH", 8);
        Store(registry, "RC5_OPTION", 153);
        var context = new TestActiveVehicleContext(StateWithChannels(Enumerable.Repeat((ushort)1500, 8).ToArray(), now));
        using var service = CreateService(context, registry, now);
        void Sample(ushort rc5, ushort others) => context.SetState(StateWithChannels(
            Enumerable.Range(1, 8).Select(channel => channel == 5 ? rc5 : others).ToArray(), now));
        Sample(999, 1000);
        Sample(2000, 2000);
        Sample(999, 1500);
        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        Sample(999, 1000);
        Sample(999, 2000);
        var failed = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RadioCalibrationState.Capturing, failed.State);
        Assert.NotEmpty(failed.Captures.Single(channel => channel.Number == 5).Issues);
        Sample(2000, 1500);
        Sample(999, 1500);
        var review = await service.FinishCaptureAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RadioCalibrationState.Review, review.State);
        Assert.Empty(review.Captures.Single(channel => channel.Number == 5).Issues);
        Sample(2100, 1500);
        Assert.Equal(2000, service.Current.Captures.Single(channel => channel.Number == 5).Maximum);
        Assert.Equal(153, registry.GetParameter(vehicleId, "RC5_OPTION")!.Value);
    }

    private static RadioCalibrationService CreateService(
        TestActiveVehicleContext context,
        VehicleParameterRegistry registry,
        DateTimeOffset now,
        List<string>? written = null,
        bool confirmWrites = true)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var parameterService = Substitute.For<IVehicleParameterService>();
        parameterService.SetParameterAsync(vehicleId, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var name = call.ArgAt<string>(1);
                var value = call.ArgAt<float>(2);
                written?.Add(name);
                if (!confirmWrites)
                {
                    return Task.FromResult(false);
                }

                registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavParamType.Int16, 0, 1), CancellationToken.None);
                return Task.FromResult(true);
            });
        var eventHub = Substitute.For<IDomainEventHub>();

        eventHub.SubscribeDomainEventAsync(
                Arg.Do<Func<VehicleStateUpdated, CancellationToken, Task>>(handler =>
                    context.StateUpdated += state => handler(new VehicleStateUpdated(state), CancellationToken.None).GetAwaiter().GetResult()))

            .Returns(Substitute.For<IDisposable>());
        return new RadioCalibrationService(context, registry, parameterService, new VehicleOperationGate(), eventHub, clock,
            Substitute.For<ILogger<RadioCalibrationService>>());
    }

    private static async Task CaptureValidEndpointsAsync(RadioCalibrationService service, TestActiveVehicleContext context, DateTimeOffset now)
    {
        await service.StartAsync(vehicleId, TestContext.Current.CancellationToken);
        context.SetState(StateWithChannels([1000, 1000, 1000, 1000], now));
        context.SetState(StateWithChannels([2000, 2000, 2000, 2000], now));
        await service.FinishCaptureAsync(TestContext.Current.CancellationToken);
    }

    private static void Store(VehicleParameterRegistry registry, string name, float value)
    {
        registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavParamType.Int16, 0, 1), CancellationToken.None);
    }

    private static VehicleState StateWithChannels(ushort[] channels, DateTimeOffset observedAt)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
                VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null) with
        {
            Flight = new VehicleFlightState(0, 0, 4, VehicleMode.Stabilize, false,
                    LandedState: VehicleLandedState.OnGround, ObservedAt: now)
        };
        return state with
        {
            Radio = VehicleRadioState.Empty with
            {
                ChannelCount = channels.Length,
                ChannelsRaw = channels,
                ObservedAt = observedAt
            }
        };
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private readonly CancellationTokenSource lifetime = new();

        public ActiveVehicleSnapshot Current { get; private set; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => lifetime.Token;

        public event Action<ActiveVehicleChangedEventArgs>? Changed;

        public event Action<VehicleState>? StateUpdated;

        public void SetState(VehicleState next)
        {
            Current = new ActiveVehicleSnapshot(next.VehicleId, next);
            StateUpdated?.Invoke(next);
        }

        public void SetOnline(bool online)
        {
            var previous = Current;
            var nextState = Current.State! with
            {
                Connection = Current.State!.Connection with
                {
                    State = online ? VehicleConnectionState.Online : VehicleConnectionState.Offline
                }
            };
            Current = new ActiveVehicleSnapshot(nextState.VehicleId, nextState);
            if (!online)
            {
                lifetime.Cancel();
            }

            Changed?.Invoke(new ActiveVehicleChangedEventArgs(previous, Current));
        }
    }
}
