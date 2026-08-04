using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Dfu;

namespace MissionPlanner.Firmware.Tests;

public sealed class DfuTargetSafetyServiceTests
{
    [Fact]
    public void MissingPlatformOrArtifactBlocks()
    {
        var service = CreateService();

        service.Evaluate(new DfuTargetSafetyRequest(null, null, Artifact(), Device())).Decision.Should().Be(DfuTargetSafetyDecision.Blocked);
        service.Evaluate(new DfuTargetSafetyRequest("CubeOrange", 140, null, Device())).EvidenceCodes.Should().Contain("HexInvalid");
    }

    [Fact]
    public void KnownArtifactPlatformOrBoardMismatchBlocks()
    {
        var service = CreateService();

        service.Evaluate(Request(Artifact("OtherBoard", 140))).EvidenceCodes.Should().Contain("ArtifactPlatformMismatch");
        service.Evaluate(Request(Artifact("CubeOrange", 999))).EvidenceCodes.Should().Contain("ArtifactBoardMismatch");
    }

    [Fact]
    public void BootloaderOnlyImageBlocksNormalInstall()
    {
        var result = CreateService().Evaluate(Request(Artifact(application: false)));

        result.Decision.Should().Be(DfuTargetSafetyDecision.Blocked);
        result.EvidenceCodes.Should().Contain("BootloaderOnlyImage");
    }

    [Fact]
    public void ReportedFlashTooSmallBlocks()
    {
        var result = CreateService().Evaluate(Request(device: Device(flashBytes: 32 * 1024)));

        result.EvidenceCodes.Should().Contain("ArtifactExceedsReportedFlash");
    }

    [Fact]
    public void KnownIncompatibleMcuBlocks()
    {
        var policies = new[] { new DfuTargetPolicy("CubeOrange", 140, ["0x450"], 1024 * 1024) };

        var result = CreateService(policies).Evaluate(Request(device: Device("0x413")));

        result.EvidenceCodes.Should().Contain("KnownIncompatibleMcu");
    }

    [Fact]
    public void SharedMcuAcrossBoardsNeverProvesSelectedPcb()
    {
        var policies = new[]
        {
            new DfuTargetPolicy("BoardA", 1, ["0x413"]),
            new DfuTargetPolicy("BoardB", 2, ["0x413"])
        };
        var service = CreateService(policies);
        var request = new DfuTargetSafetyRequest("BoardA", 1, Artifact("BoardA", 1), Device("0x413"));

        var result = service.Evaluate(request);

        result.Decision.Should().Be(DfuTargetSafetyDecision.AllowedWithStrongWarning);
        result.EvidenceCodes.Should().Contain("McuIdentityIsNotBoardProof");
        result.RequiredConfirmationPhrase.Should().Be("FLASH BoardA");
    }

    [Fact]
    public void ExactTypedPhraseSatisfiesWarningButDoesNotTurnMcuIntoProof()
    {
        var result = CreateService().Evaluate(Request(confirmation: "FLASH CubeOrange"));

        result.Decision.Should().Be(DfuTargetSafetyDecision.AllowedWithStrongWarning);
        result.RequiredConfirmationPhrase.Should().BeNull();
        result.EvidenceCodes.Should().Contain("StrongConfirmationAccepted");
    }

    [Fact]
    public void RememberedAssociationRequiresApplicationAndDfuIdentityMatch()
    {
        var association = new DfuRememberedAssociation("CubeOrange", 140, "app-usb-123", "DFU123");
        var request = Request() with { PreviousApplicationIdentity = "app-usb-123", RememberedAssociation = association };

        var allowed = CreateService().Evaluate(request);
        var wrongApplication = CreateService().Evaluate(request with { PreviousApplicationIdentity = "other" });

        allowed.Decision.Should().Be(DfuTargetSafetyDecision.Allowed);
        allowed.EvidenceCodes.Should().Contain("RememberedDeviceAssociationMatches");
        wrongApplication.Decision.Should().Be(DfuTargetSafetyDecision.AllowedWithStrongWarning);
    }

    private static DfuTargetSafetyService CreateService(DfuTargetPolicy[]? policies = null) =>
        new(Options.Create(new DfuOptions { TargetPolicies = policies ?? [] }));

    private static DfuTargetSafetyRequest Request(
        DfuArtifact? artifact = null,
        DfuDeviceInformation? device = null,
        string? confirmation = null) =>
        new("CubeOrange", 140, artifact ?? Artifact(), device ?? Device(), ConfirmationPhrase: confirmation);

    private static DfuArtifact Artifact(
        string platform = "CubeOrange",
        int boardId = 140,
        bool application = true)
    {
        var ranges = application
            ? new[] { new DfuMemoryRange(0x08000000, new byte[] { 1 }), new DfuMemoryRange(0x08010000, new byte[] { 2 }) }
            : [new DfuMemoryRange(0x08000000, new byte[] { 1 })];
        var metadata = new DfuArtifactMetadata(100, ranges.Length, ranges[0].StartAddress, ranges[^1].EndAddress,
            new string('A', 64), ranges, [], AppearsToContainBootloader: true, AppearsToContainApplication: application);
        return new DfuArtifact("firmware_with_bl.hex", "firmware.hex", metadata, Platform: platform, BoardId: boardId);
    }

    private static DfuDeviceInformation Device(string mcu = "0x450", long flashBytes = 2 * 1024 * 1024) =>
        new(new DfuDeviceDescriptor("usb1", 0x0483, 0xDF11, DfuDriverState.PresentReady, SerialNumber: "DFU123"),
            mcu, "Rev V", flashBytes, [], []);
}
