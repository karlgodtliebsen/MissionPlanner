using MissionPlanner.Core.Setup.Reporting;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class SetupReportTests
{
    [Fact]
    public void LoadingFailureAndDisconnectNeverExportPartialOrStaleValues()
    {
        var id = new VehicleId(1, 1);
        var active = Substitute.For<IActiveVehicleContext>();
        active.Current.Returns(new ActiveVehicleSnapshot(id, VehicleLiveDiagnosticsTests.State(id)));
        active.VehicleId.Returns(id);
        active.IsOnline.Returns(true);
        var registry = new VehicleParameterRegistry();
        registry.StoreParameter(id, new("FRAME_CLASS", 1, MavParamType.Real32, 0, 2), TestContext.Current.CancellationToken);
        registry.StoreParameter(id, new("FRAME_TYPE", 1, MavParamType.Real32, 1, 2), TestContext.Current.CancellationToken);
        var loads = new VehicleParameterLoadStatusContext();
        var service = new SetupReportService(active, registry, loads);
        foreach (var state in new[] { ParameterLoadState.Starting, ParameterLoadState.Downloading, ParameterLoadState.Failed, ParameterLoadState.Cancelled })
        {
            loads.Update(new(id, state, 1, 2, 50, "Loading", DateTimeOffset.UtcNow));
            Assert.Empty(service.Capture(SetupReportTopic.Frame).Parameters);
        }
        loads.Update(new(id, ParameterLoadState.Completed, 2, 2, 100, "Complete", DateTimeOffset.UtcNow));
        Assert.Equal(2, service.Capture(SetupReportTopic.Frame).Parameters.Count);
        registry.StoreParameter(id, new("FRAME_TYPE", 3, MavParamType.Real32, 1, 2), TestContext.Current.CancellationToken);
        Assert.Equal(3, service.Capture(SetupReportTopic.Frame).Parameters.Single(p => p.Name == "FRAME_TYPE").Value);
        active.Current.Returns(ActiveVehicleSnapshot.Empty);
        active.IsOnline.Returns(false);
        Assert.Empty(service.Capture(SetupReportTopic.Frame).Parameters);
        Assert.Null(service.Capture(SetupReportTopic.Frame).VehicleId);
    }

    [Fact]
    public void ReportsFilterUnrelatedParametersAndPreserveUnknownNumericEvidence()
    {
        var id = new VehicleId(1, 1);
        var active = Substitute.For<IActiveVehicleContext>();
        active.Current.Returns(new ActiveVehicleSnapshot(id, VehicleLiveDiagnosticsTests.State(id)));
        active.VehicleId.Returns(id);
        active.IsOnline.Returns(true);
        var registry = new VehicleParameterRegistry();
        registry.StoreParameter(id, new("FLOW_ENABLE", 0, MavParamType.Real32, 0, 3), TestContext.Current.CancellationToken);
        registry.StoreParameter(id, new("FLOW_FXSCALER", float.NaN, MavParamType.Real32, 1, 3), TestContext.Current.CancellationToken);
        registry.StoreParameter(id, new("MOT_SPIN_ARM", 0.1f, MavParamType.Real32, 2, 3), TestContext.Current.CancellationToken);
        var service = new SetupReportService(active, registry, new VehicleParameterLoadStatusContext());
        var report = service.Capture(SetupReportTopic.OpticalFlow);
        Assert.Equal(2, report.Parameters.Count);
        Assert.Null(report.Parameters.Single(p => p.Name == "FLOW_FXSCALER").Value);
        Assert.Contains(report.Facts, f => f.Value == "Disabled");
        Assert.DoesNotContain(report.Parameters, p => p.Name.StartsWith("MOT_"));
    }

    [Fact]
    public void EverySubsystemHasGuidanceWithoutInventingDisconnectedEvidence()
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.Current.Returns(ActiveVehicleSnapshot.Empty);
        var service = new SetupReportService(active, new VehicleParameterRegistry(), new VehicleParameterLoadStatusContext());
        foreach (var topic in Enum.GetValues<SetupReportTopic>())
        {
            var report = service.Capture(topic);
            Assert.NotEmpty(report.Title);
            Assert.NotEmpty(report.Overview);
            Assert.NotEmpty(report.Guidance);
            Assert.Empty(report.Parameters);
        }
    }

    [Theory]
    [InlineData(false, false, false, "unavailable")]
    [InlineData(true, true, false, "in progress")]
    [InlineData(true, false, true, "Discovery")]
    [InlineData(true, false, false, "DFU endpoint")]
    public void FirmwareGuidanceUsesDiscoveryAndOperationState(bool supported, bool installing, bool refreshing, string expected)
    {
        var report = FirmwareDiscoveryReporting.Create(new(false, ["COM7"], ["USB endpoint"], "Ready", "Ready",
            "Unknown", "Anonymous ROM", refreshing, installing, supported));
        Assert.Contains(expected, report.Guidance[0]);
        Assert.Contains(report.Guidance, g => g.Contains("does not establish firmware compatibility"));
        Assert.Empty(report.Parameters);
    }
}
