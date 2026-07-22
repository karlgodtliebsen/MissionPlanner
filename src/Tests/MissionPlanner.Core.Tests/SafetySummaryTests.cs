using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies evidence-based safety assessment and the consolidated setup summary export.</summary>
public sealed class SafetySummaryTests
{
    private static readonly VehicleId vehicleId = new(1, 1);

    /// <summary>Verifies disabled arming checks and a report-only fence raise contradiction warnings.</summary>
    [Fact]
    public void SafetyAssessmentFlagsContradictions()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "ARMING_CHECK", 0);
        Store(registry, "FENCE_ENABLE", 1);
        Store(registry, "FENCE_ACTION", 0);
        Store(registry, "FS_THR_ENABLE", 1);
        var service = new SafetyAssessmentService(Context(FirmwareFamily.ArduCopter), registry, Substitute.For<ILogger<SafetyAssessmentService>>());

        var assessment = service.BuildAssessment(vehicleId);

        assessment.Items.Should().Contain(item => item.Name == "Pre-arm checks" && item.Status == SetupAssessmentStatus.Warning);
        assessment.Warnings.Should().Contain(warning => warning.Contains("Pre-arm checks are disabled"));
        assessment.Warnings.Should().Contain(warning => warning.Contains("report-only"));
    }

    /// <summary>Verifies EKF failsafe is marked unsupported for non-Copter families rather than passed.</summary>
    [Fact]
    public void UnsupportedChecksAreNotPassed()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "ARMING_CHECK", 1);
        var service = new SafetyAssessmentService(Context(FirmwareFamily.ArduPlane), registry, Substitute.For<ILogger<SafetyAssessmentService>>());

        var assessment = service.BuildAssessment(vehicleId);

        assessment.Items.Should().Contain(item => item.Name == "EKF failsafe" && item.Status == SetupAssessmentStatus.Unsupported);
        assessment.Items.Should().NotContain(item => item.Name == "EKF failsafe" && item.Status == SetupAssessmentStatus.Pass);
    }

    /// <summary>Verifies the summary aggregates sections and exports identifiable JSON and Markdown.</summary>
    [Fact]
    public void SummaryAggregatesAndExports()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "ARMING_CHECK", 1);
        Store(registry, "FRAME_CLASS", 1);
        var context = Context(FirmwareFamily.ArduCopter);
        var safety = new SafetyAssessmentService(context, registry, Substitute.For<ILogger<SafetyAssessmentService>>());
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        var summaryService = new SetupSummaryService(context, registry, new SetupWorkflowCatalog(),
            new MemoryCompletionStore(), safety, clock, Substitute.For<ILogger<SetupSummaryService>>());

        var summary = summaryService.BuildSummary(vehicleId);
        summary.Sections.Select(section => section.Title).Should().Contain(["Identity", "Frame", "Workflows", "Safety"]);

        var json = summaryService.ExportJson(summary);
        json.Should().Contain("ArduCopter").And.Contain("Sections");

        var markdown = summaryService.ExportMarkdown(summary);
        markdown.Should().Contain("# Setup summary").And.Contain("not a certification").And.Contain("## Safety");
    }

    private static void Store(VehicleParameterRegistry registry, string name, float value) =>
        registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavParamType.Int32, 0, 1), CancellationToken.None);

    private static IActiveVehicleContext Context(FirmwareFamily family)
    {
        var active = Substitute.For<IActiveVehicleContext>();
        var state = State(family);
        active.VehicleId.Returns(vehicleId);
        active.IsOnline.Returns(true);
        active.State.Returns(state);
        active.Current.Returns(new ActiveVehicleSnapshot(vehicleId, state));
        return active;
    }

    private static VehicleState State(FirmwareFamily family)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
        var firmware = new VehicleFirmwareIdentity(
            family, state.VehicleType, state.Autopilot,
            new FirmwareSemanticVersion(4, 5, 0, FirmwareReleaseType.Official),
            "abcdef01", 0, 1, 2, 3, 42, "vehicle-1");
        return state with { Identity = state.Identity with { Firmware = firmware } };
    }

    private sealed class MemoryCompletionStore : ISetupCompletionStore
    {
        private readonly List<SetupCompletionEvidence> values = [];

        public IReadOnlyList<SetupCompletionEvidence> GetAll() => values;

        public void Save(SetupCompletionEvidence evidence) => values.Add(evidence);

        public void Remove(string vehicleKey, SetupWorkflowKey workflow) =>
            values.RemoveAll(item => item.VehicleKey == vehicleKey && item.Workflow == workflow);
    }
}
