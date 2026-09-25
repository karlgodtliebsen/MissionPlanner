using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Setup.Arming;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Deterministic arming reports keep current, historical, pending and heartbeat evidence separate.</summary>
public sealed class ArmingDocumentTests
{
    /// <summary>Every diagnostic stage is rendered without promoting ACK to heartbeat truth.</summary>
    [Theory]
    [InlineData(ArmingDiagnosticStage.Unknown)]
    [InlineData(ArmingDiagnosticStage.DisarmedReady)]
    [InlineData(ArmingDiagnosticStage.DisarmedNotReady)]
    [InlineData(ArmingDiagnosticStage.ArmRequested)]
    [InlineData(ArmingDiagnosticStage.ArmRejected)]
    [InlineData(ArmingDiagnosticStage.Armed)]
    [InlineData(ArmingDiagnosticStage.DisarmRequested)]
    public void Stages(ArmingDiagnosticStage stage)
    {
        var factory = new ArmingSetupDocumentFactory();
        var diagnostic = new VehicleArmingDiagnostic("Status", stage == ArmingDiagnosticStage.Armed,
            stage == ArmingDiagnosticStage.DisarmedReady, stage == ArmingDiagnosticStage.DisarmedNotReady ? ["Compass", "Battery"] : [],
            "Old failure", DateTimeOffset.UnixEpoch, "ACK accepted")
        { Stage = stage, LastArmAck = 0, LastArmCommandSource = "RC switch (inferred)", LastPreArmReason = "Historical compass warning", LastPreArmReasonAt = DateTimeOffset.UnixEpoch };
        var context = new ArmingDocumentContext(true, null, diagnostic, false, "RC8: 999 → 2000 → 999", "", "GCS policy");
        var status = factory.CreateStatus(context).Markdown;
        var detail = factory.CreateDiagnostics(context).Markdown;
        Assert.Contains("Historical compass warning", detail);
        Assert.DoesNotContain("Historical compass warning", status);
        Assert.Contains("Historical evidence", detail);
        Assert.Contains(stage.ToString(), detail);
        Assert.Equal(status, factory.CreateStatus(context).Markdown);
        if (stage == ArmingDiagnosticStage.ArmRequested)
        {
            Assert.Contains("Arm acknowledged; waiting for armed heartbeat", status);
            Assert.DoesNotContain("Armed heartbeat observed", status);
        }
        if (stage == ArmingDiagnosticStage.DisarmedNotReady)
        {
            Assert.Contains("Compass", status); Assert.Contains("Battery", status);
        }
    }

    /// <summary>Disconnected, no-request and pending configurations are explained distinctly.</summary>
    [Fact]
    public void DisconnectedUnknownAndNoRequest()
    {
        var factory = new ArmingSetupDocumentFactory();
        var context = new ArmingDocumentContext(false, null, null, false, "", "", "");
        Assert.Contains("Disconnected", factory.CreateStatus(context).Markdown);
        context = context with { Online = true, Pending = true, Diagnostic = new("Unknown", false, null, [], null, null, null) };
        var text = factory.CreateStatus(context).Markdown;
        Assert.Contains("No recent arm request was observed", text);
        Assert.Contains("pending and are not active FC values", text);
        Assert.DoesNotContain("rejected", text);
        Assert.Contains("// Parameters are not fully loaded", factory.CreateDiagnostics(context).Markdown);
    }

    /// <summary>Only confirmed numeric values are copyable assignments; hostile prose cannot inject Markdown.</summary>
    [Fact]
    public void CopyableEvidenceAndMultipleAssignments()
    {
        var state = new ArmingSetupState(new VehicleId(1, 1), new(PreArmCheckMode.All), [], [8, 10], true, true, true,
            new Dictionary<string, double> { ["ARMING_CHECK"] = 1, ["RC8_OPTION"] = 153 }, "<script> [inject](https://example.com)");
        var context = new ArmingDocumentContext(true, state, null, true, "", "", "");
        var factory = new ArmingSetupDocumentFactory();
        Assert.Contains("ARMING_CHECK = 1", factory.CreateDiagnostics(context).Markdown);
        Assert.Contains("RC8_OPTION = 153", factory.CreateDiagnostics(context).Markdown);
        var status = factory.CreateStatus(context).Markdown;
        Assert.Contains("Multiple Arm/Disarm assignments", status);
        Assert.Contains(UserDocumentBuilder.Escape(state.Status), status);
        Assert.DoesNotContain("ARMING_RUDDER = 0", factory.CreateDiagnostics(context).Markdown);
    }
}
