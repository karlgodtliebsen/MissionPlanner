using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies the closed scenario schema, exact-target executor, and audit reports.</summary>
[Trait("TestTier", "Unit")]
public sealed class SimulationScenarioTests
{
    /// <summary>Verifies current schema round-trip, typed variables, version rejection, and unknown-field rejection.</summary>
    [Fact]
    public void ParserUsesClosedVersionedSchemaAndTypedVariables()
    {
        var parser = CreateParser();
        var document = Document(
            TakeoffStep(new SimulationScenarioValue(SimulationScenarioValueKind.Number, Variable: "altitude"))) with
        {
            Variables = new Dictionary<string, SimulationScenarioValue>
            {
                ["altitude"] = new(SimulationScenarioValueKind.Number, NumberValue: 12)
            }
        };

        var json = parser.Serialize(document);
        parser.Parse(json).Should().BeEquivalentTo(document);

        var wrongVersion = parser.Serialize(document with { SchemaVersion = 2 });
        var versionAction = () => parser.Parse(wrongVersion);
        versionAction.Should().Throw<InvalidDataException>().WithMessage("*Unsupported schema version*");

        var unknownField = json.Replace("\"name\":", "\"arbitraryScript\": \"echo unsafe\", \"name\":", StringComparison.Ordinal);
        var unknownAction = () => parser.Parse(unknownField);
        unknownAction.Should().Throw<InvalidDataException>().WithMessage("*arbitraryScript*");
    }

    /// <summary>Verifies every step is bounded and malformed kind-specific fields are rejected.</summary>
    [Fact]
    public void ValidatorRequiresExplicitTimeoutsAndKindSpecificValues()
    {
        var parser = CreateParser();
        var invalid = Document(
            new SimulationScenarioStep("wait", SimulationScenarioStepKind.WaitCondition, "Wait", 0));

        var issues = parser.Validate(invalid);

        issues.Should().Contain(item => item.Path.EndsWith("timeoutSeconds", StringComparison.Ordinal));
        issues.Should().Contain(item => item.Path.EndsWith("condition", StringComparison.Ordinal));
    }

    /// <summary>Verifies a dry run reports all required live capabilities without sending commands.</summary>
    [Fact]
    public async Task DryRunReportsCapabilitiesWithoutVehicleChanges()
    {
        var fixture = CreateFixture();
        var document = Document(
            Step("mode", SimulationScenarioStepKind.SetMode) with { Mode = "Guided" },
            Step("fault", SimulationScenarioStepKind.InjectFault) with
            {
                ControlKey = "rc-failure",
                Value = Number(1),
                DurationSeconds = 5
            });

        var report = await fixture.Runner.RunAsync(
            Request(document, dryRun: true, confirmed: false),
            TestContext.Current.CancellationToken);

        report.Result.Should().Be(SimulationScenarioRunResult.DryRun);
        report.Steps.Should().OnlyContain(item => item.Result == SimulationScenarioStepResult.Planned);
        report.Validation.Capabilities.Should().Contain(item => item.Name == "mode:Guided" && item.Available);
        report.Validation.Capabilities.Should().Contain(item => item.Name == "control:rc-failure" && item.Available);
        fixture.Commands.ReceivedCalls().Should().BeEmpty();
        fixture.Controls.ReceivedCalls().Should().NotContain(call =>
            call.GetMethodInfo().Name == nameof(ISimulationControlService.ApplyAsync));
    }

    /// <summary>Verifies all declared action families execute in order and produce auditable evidence.</summary>
    [Fact]
    public async Task ExecutorCompletesCommandsMissionControlsConditionsAndReports()
    {
        var fixture = CreateFixture();
        var document = Document(
            Step("online", SimulationScenarioStepKind.WaitForState) with { State = SimulationVehicleStateRequirement.Online },
            Step("mode", SimulationScenarioStepKind.SetMode) with { Mode = "Guided" },
            Step("arm", SimulationScenarioStepKind.Arm),
            TakeoffStep(Number(10)),
            Step("upload", SimulationScenarioStepKind.UploadMission) with { MissionItems = [MissionItem()] },
            Step("start", SimulationScenarioStepKind.StartMission),
            Step("inject", SimulationScenarioStepKind.InjectFault) with
            {
                ControlKey = "rc-failure",
                Value = Number(1),
                DurationSeconds = 5
            },
            Step("clear", SimulationScenarioStepKind.ClearFault) with { ControlKey = "rc-failure" },
            Step("land", SimulationScenarioStepKind.Land),
            Step("assert", SimulationScenarioStepKind.AssertTelemetry) with
            {
                Condition = new SimulationTelemetryCondition(
                    SimulationTelemetryMetric.Online,
                    SimulationComparisonOperator.Equal,
                    new SimulationScenarioValue(SimulationScenarioValueKind.Boolean, BooleanValue: true))
            });

        var report = await fixture.Runner.RunAsync(
            Request(document, dryRun: false, confirmed: true),
            TestContext.Current.CancellationToken);

        report.Result.Should().Be(SimulationScenarioRunResult.Succeeded);
        report.Steps.Should().HaveCount(10).And.OnlyContain(item => item.Result == SimulationScenarioStepResult.Succeeded);
        report.Steps.Should().OnlyContain(item => item.Telemetry != null);
        await fixture.Commands.Received(1).ArmAsync(vehicleId, Arg.Any<CancellationToken>());
        await fixture.Commands.Received(1).TakeoffAsync(vehicleId, 10, true, Arg.Any<CancellationToken>());
        await fixture.Commands.Received(1).LandAsync(vehicleId, Arg.Any<CancellationToken>());
        await fixture.Missions.Received(1).UploadItemsAsync(
            vehicleId,
            Arg.Is<IReadOnlyList<MavLinkMissionItem>>(items => ContainsSingleFirstSequence(items)),
            MissionPlanner.Core.Missions.Models.MissionPlanType.FlightMission,
            null,
            Arg.Any<CancellationToken>());
        await fixture.Controls.Received(1).ApplyAsync("rc-failure", 1, TimeSpan.FromSeconds(5), true, Arg.Any<CancellationToken>());
        await fixture.Controls.Received(1).ResetAsync("rc-failure", Arg.Any<CancellationToken>());

        var exporter = new SimulationScenarioReportExporter();
        exporter.ToJson(report).Should().Contain("\"reportVersion\": 1");
        exporter.ToText(report).Should().Contain("[Succeeded] online");
    }

    /// <summary>Verifies a rejected acknowledged command fails its step and stops later actions.</summary>
    [Fact]
    public async Task ExecutorRecordsCommandFailureAndStops()
    {
        var fixture = CreateFixture();
        var denied = Response(VehicleCommandResult.Denied, "Pre-arm checks failed.");
        fixture.Commands.ArmAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(denied);
        var document = Document(
            Step("arm", SimulationScenarioStepKind.Arm),
            Step("land", SimulationScenarioStepKind.Land));

        var report = await fixture.Runner.RunAsync(
            Request(document, dryRun: false, confirmed: true),
            TestContext.Current.CancellationToken);

        report.Result.Should().Be(SimulationScenarioRunResult.Failed);
        report.Steps.Should().ContainSingle().Which.Should().Match<SimulationScenarioStepReport>(item =>
            item.StepId == "arm" && item.Result == SimulationScenarioStepResult.Failed && item.Evidence.Contains("Pre-arm"));
        fixture.Commands.ReceivedCalls().Should().NotContain(call =>
            call.GetMethodInfo().Name == nameof(IVehicleCommandService.LandAsync));
    }

    /// <summary>Verifies an unsatisfied wait stops at its explicit timeout and records telemetry.</summary>
    [Fact]
    public async Task ExecutorRecordsExplicitWaitTimeout()
    {
        var fixture = CreateFixture();
        var document = Document(
            Step("armed", SimulationScenarioStepKind.WaitForState, timeoutSeconds: 1) with
            {
                State = SimulationVehicleStateRequirement.Armed
            });

        var report = await fixture.Runner.RunAsync(
            Request(document, dryRun: false, confirmed: false),
            TestContext.Current.CancellationToken);

        report.Result.Should().Be(SimulationScenarioRunResult.Failed);
        report.Steps.Should().ContainSingle().Which.Evidence.Should().Contain("timed out after 1 seconds");
    }

    /// <summary>Verifies caller cancellation returns a canceled report and stops a pending wait.</summary>
    [Fact]
    public async Task ExecutorCancelsPendingWait()
    {
        var fixture = CreateFixture();
        var document = Document(
            Step("armed", SimulationScenarioStepKind.WaitForState, timeoutSeconds: 10) with
            {
                State = SimulationVehicleStateRequirement.Armed
            });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        var report = await fixture.Runner.RunAsync(Request(document, dryRun: false, confirmed: false), cancellation.Token);

        report.Result.Should().Be(SimulationScenarioRunResult.Canceled);
        report.Steps.Should().ContainSingle().Which.Result.Should().Be(SimulationScenarioStepResult.Canceled);
    }

    /// <summary>Verifies cancellation after arming performs a confirmed ground disarm on the original target.</summary>
    [Fact]
    public async Task CancellationAfterArmAttemptsDefinedGroundSafeState()
    {
        var fixture = CreateFixture();
        var accepted = Response();
        fixture.Commands.ArmAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var now = DateTimeOffset.UtcNow;
            fixture.Session.ApplyHeartbeat(new VehicleHeartbeatObservation(0, 2, 3, 128, 4, 3, now));
            fixture.Session.ApplyExtendedFlightState(new VehicleExtendedFlightStateObservation(
                VehicleVtolState.Undefined,
                VehicleLandedState.OnGround,
                now));
            return accepted;
        });
        var document = Document(
            Step("arm", SimulationScenarioStepKind.Arm),
            Step("altitude", SimulationScenarioStepKind.WaitCondition, timeoutSeconds: 10) with
            {
                Condition = new SimulationTelemetryCondition(
                    SimulationTelemetryMetric.RelativeAltitudeMeters,
                    SimulationComparisonOperator.GreaterThan,
                    Number(100))
            });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(80));

        var report = await fixture.Runner.RunAsync(Request(document, dryRun: false, confirmed: true), cancellation.Token);

        report.Result.Should().Be(SimulationScenarioRunResult.Canceled);
        await fixture.Commands.Received(1).DisarmAsync(vehicleId, true, CancellationToken.None);
    }

    /// <summary>Verifies pause occurs only after the active step and resume starts the next step.</summary>
    [Fact]
    public async Task PauseAndResumeOperateAtSafeStepBoundary()
    {
        var fixture = CreateFixture();
        var modeCompletion = new TaskCompletionSource<VehicleCommandResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Commands.SetModeAsync(vehicleId, Arg.Any<VehicleModeOption>(), Arg.Any<CancellationToken>())
            .Returns(modeCompletion.Task);
        var document = Document(
            Step("mode", SimulationScenarioStepKind.SetMode) with { Mode = "Guided" },
            Step("arm", SimulationScenarioStepKind.Arm));

        var running = fixture.Runner.RunAsync(
            Request(document, dryRun: false, confirmed: true),
            TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => fixture.Runner.Current.StepId == "mode", TimeSpan.FromSeconds(2));
        fixture.Runner.Pause().Should().BeTrue();
        modeCompletion.SetResult(Response());
        await WaitUntilAsync(() => fixture.Runner.Current.State == SimulationScenarioRunnerState.Paused, TimeSpan.FromSeconds(2));
        fixture.Commands.ReceivedCalls().Should().NotContain(call =>
            call.GetMethodInfo().Name == nameof(IVehicleCommandService.ArmAsync));

        fixture.Runner.Resume().Should().BeTrue();
        (await running).Result.Should().Be(SimulationScenarioRunResult.Succeeded);
        await fixture.Commands.Received(1).ArmAsync(vehicleId, Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies validation rejects a different vehicle and an unavailable documented control.</summary>
    [Fact]
    public async Task ValidationRejectsWrongVehicleAndMissingCapability()
    {
        var fixture = CreateFixture(controlAvailable: false);
        var document = Document(
            Step("fault", SimulationScenarioStepKind.InjectFault) with
            {
                ControlKey = "gps-failure",
                Value = Number(1),
                DurationSeconds = 5
            });

        var wrongTarget = await fixture.Runner.ValidateAsync(
            document,
            sessionId,
            new VehicleId(99, 1),
            TestContext.Current.CancellationToken);
        wrongTarget.IsValid.Should().BeFalse();
        wrongTarget.Issues.Should().Contain(item => item.Path == "target");

        var missingControl = await fixture.Runner.ValidateAsync(
            document,
            sessionId,
            vehicleId,
            TestContext.Current.CancellationToken);
        missingControl.IsValid.Should().BeFalse();
        missingControl.Capabilities.Should().Contain(item => item.Name == "control:gps-failure" && !item.Available);
    }

    private static readonly VehicleId vehicleId = new(1, 1);
    private static readonly Guid sessionId = Guid.Parse("7d1180e5-c227-433b-a395-e243366780fb");

    private static ISimulationScenarioParser CreateParser() =>
        new SimulationScenarioParser(Options.Create(new SimulationScenarioOptions()));

    private static ScenarioFixture CreateFixture(bool controlAvailable = true)
    {
        var now = DateTimeOffset.UtcNow;
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => DateTimeOffset.UtcNow);
        var state = new VehicleState(
            vehicleId,
            0,
            2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            now,
            VehicleMode.Stabilize,
            false,
            55,
            12,
            10,
            0,
            0,
            0,
            90,
            12) with
        {
            Flight = new VehicleFlightState(0, 0, 4, VehicleMode.Stabilize, false,
                LandedState: VehicleLandedState.OnGround, ObservedAt: now),
            Position = VehiclePositionState.Empty with
            {
                LatitudeDegrees = 55,
                LongitudeDegrees = 12,
                AltitudeMslMeters = 10,
                RelativeAltitudeMeters = 0,
                ObservedAt = now
            },
            Motion = VehicleMotionState.Empty with { GroundSpeedMetersPerSecond = 0, ObservedAt = now }
        };
        state = state with
        {
            Identity = state.Identity with
            {
                Firmware = state.Identity.Firmware with { Family = FirmwareFamily.ArduCopter }
            }
        };
        var session = new VehicleSession(state, new TransportEndPoint("scenario-test"), clock);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(vehicleId).Returns(session);
        var manager = Substitute.For<ISimulationSessionManager>();
        manager.Current.Returns(new SimulationSessionSnapshot(
            sessionId,
            SimulatorProfile.CreateDefault(),
            SimulationSessionState.Running,
            new SimulatorRuntimeIdentity("scenario", "test", null),
            [],
            now,
            null,
            "Running",
            null,
            [],
            vehicleId));

        var accepted = Response();
        var commands = Substitute.For<IVehicleCommandService>();
        commands.ArmAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(accepted);
        commands.DisarmAsync(vehicleId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(accepted);
        commands.TakeoffAsync(vehicleId, Arg.Any<double>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(accepted);
        commands.SetModeAsync(vehicleId, Arg.Any<VehicleModeOption>(), Arg.Any<CancellationToken>()).Returns(accepted);
        commands.LandAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(accepted);
        commands.ExecuteExpertAsync(Arg.Any<ExpertVehicleCommand>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(accepted);
        var modes = Substitute.For<IArduPilotModeCatalog>();
        modes.GetModes(FirmwareFamily.ArduCopter).Returns([new VehicleModeOption("Guided", 4, VehicleMode.Guided)]);
        modes.Find(FirmwareFamily.ArduCopter, VehicleMode.Land).Returns(new VehicleModeOption("Land", 9, VehicleMode.Land));
        var missions = Substitute.For<IMissionTransferService>();
        var upload = new MissionUploadResult(true, 0, null);
        missions.UploadItemsAsync(
                vehicleId,
                Arg.Any<IReadOnlyList<MavLinkMissionItem>>(),
                Arg.Any<MissionPlanner.Core.Missions.Models.MissionPlanType>(),
                Arg.Any<IProgress<MissionUploadProgress>?>(),
                Arg.Any<CancellationToken>())
            .Returns(upload);
        var controls = Substitute.For<ISimulationControlService>();
        var descriptor = new SimulationControlCatalog().Controls.Single(item => item.Key == "rc-failure") with
        {
            Key = controlAvailable ? "rc-failure" : "gps-failure"
        };
        IReadOnlyList<SimulationControlCapability> capabilities =
        [new(descriptor, controlAvailable, controlAvailable ? "SIM_RC_FAIL" : null,
            controlAvailable ? MissionPlanner.MavLink.Parameters.MavParamType.Real32 : null,
            controlAvailable ? 0 : null,
            controlAvailable ? "Available on exact target." : "Required parameter is absent.",
            null)];
        controls.DiscoverAsync(Arg.Any<CancellationToken>()).Returns(capabilities);
        controls.ApplyAsync(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        controls.ResetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var runner = new SimulationScenarioRunner(
            CreateParser(),
            manager,
            registry,
            commands,
            modes,
            missions,
            controls,
            new SimulationScenarioDelay(),
            clock,
            Options.Create(new SimulationScenarioOptions { PollIntervalMilliseconds = 10 }),
            Substitute.For<ILogger<SimulationScenarioRunner>>());
        return new ScenarioFixture(runner, commands, missions, controls, session);
    }

    private static SimulationScenarioRunRequest Request(
        SimulationScenarioDocument document,
        bool dryRun,
        bool confirmed) =>
        new(document, sessionId, vehicleId, dryRun, confirmed);

    private static SimulationScenarioDocument Document(params SimulationScenarioStep[] steps) =>
        new(1, Guid.Parse("011bb91e-a8b5-4430-aa4a-3a0711dc8163"), "Scenario test",
            new Dictionary<string, SimulationScenarioValue>(), steps);

    private static SimulationScenarioStep Step(
        string id,
        SimulationScenarioStepKind kind,
        int timeoutSeconds = 2) =>
        new(id, kind, id, timeoutSeconds);

    private static SimulationScenarioStep TakeoffStep(SimulationScenarioValue value) =>
        Step("takeoff", SimulationScenarioStepKind.Takeoff) with { Value = value };

    private static SimulationScenarioValue Number(double value) =>
        new(SimulationScenarioValueKind.Number, NumberValue: value);

    private static SimulationScenarioMissionItem MissionItem() =>
        new(3, 16, false, true, 0, 0, 0, 0, 550000000, 120000000, 10);

    private static bool ContainsSingleFirstSequence(IReadOnlyList<MavLinkMissionItem>? items) =>
        items?.Count == 1 && items[0].Sequence == 0;

    private static VehicleCommandResponse Response(
        VehicleCommandResult result = VehicleCommandResult.Accepted,
        string message = "MAVLink ACK accepted.") =>
        new(vehicleId, result, DateTimeOffset.UtcNow, message);

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!condition())
        {
            await Task.Delay(10, cancellation.Token);
        }
    }

    private sealed record ScenarioFixture(
        SimulationScenarioRunner Runner,
        IVehicleCommandService Commands,
        IMissionTransferService Missions,
        ISimulationControlService Controls,
        VehicleSession Session);
}
