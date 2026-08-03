using FluentAssertions;
using MissionPlanner.Firmware.Diagnostics;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareDiagnosticReportTests
{
    [Fact]
    public void CreateReportIncludesBoundedOperationEvidence()
    {
        var report = new FirmwareDiagnosticReport(
            Guid.Parse("2fc00000-0000-0000-0000-000000000001"),
            FirmwareOperationState.Failed,
            "Copter 4.7.0 stable",
            9,
            9,
            5,
            "COM7",
            "COM9",
            BytesProgrammed: 1234,
            VerificationResult: "Failed",
            FailureCode: "installation.verification-failed");

        var text = report.CreateReport();

        text.Should().Contain("Operation: 2fc00000-0000-0000-0000-000000000001")
            .And.Contain("Firmware board ID: 9")
            .And.Contain("Bootloader device: COM9")
            .And.Contain("Verification: Failed")
            .And.NotContain("System.Byte");
    }
}
