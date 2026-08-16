using FluentAssertions;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareCompatibilityServiceTests
{
    private readonly FirmwareCompatibilityService service = new();

    [Fact]
    public void CompatiblePackageIsAccepted()
    {
        var result = service.Check(Package(), Bootloader());

        result.IsCompatible.Should().BeTrue();
        result.Code.Should().Be("compatibility.compatible");
    }

    [Fact]
    public void BoardMismatchIsBlockedWithStructuredDetails()
    {
        var result = service.Check(Package(boardId: 50), Bootloader(boardId: 9));

        result.IsCompatible.Should().BeFalse();
        result.Code.Should().Be("compatibility.board-id-mismatch");
        result.TechnicalDetail.Should().Contain("Firmware board ID: 50").And.Contain("Detected board ID: 9");
    }

    [Fact]
    public void ExplicitPolicyAllowsOnlyBoardIdMismatch()
    {
        var policy = new FirmwareCompatibilityPolicy(AllowBoardIdMismatch: true);

        service.Check(Package(boardId: 50), Bootloader(boardId: 9), policy).IsCompatible.Should().BeTrue();
        service.Check(Package(boardId: 50, boardRevision: 3), Bootloader(boardId: 9, boardRevision: 2), policy)
            .Code.Should().Be("compatibility.board-revision-too-old");
        service.Check(Package(boardId: 50, imageSize: 17), Bootloader(boardId: 9, flashSize: 16), policy)
            .Code.Should().Be("compatibility.internal-image-too-large");
        service.Check(Package(boardId: 50, externalSize: 5), Bootloader(boardId: 9, externalSize: 4), policy)
            .Code.Should().Be("compatibility.external-flash-insufficient");
        service.Check(Package(boardId: 50, minimumBootloader: 5), Bootloader(boardId: 9, revision: 4), policy)
            .Code.Should().Be("compatibility.bootloader-too-old");
        service.Check(Package(boardId: 50, requiresSecure: true), Bootloader(boardId: 9, isSecure: false), policy)
            .Code.Should().Be("compatibility.secure-boot-required");
        service.Check(Package(boardId: 50, isSigned: false), Bootloader(boardId: 9, isSecure: true), policy)
            .Code.Should().Be("compatibility.signed-image-required");
    }

    [Fact]
    public void HistoricalBoard33Firmware9CompatibilityRemainsAccepted()
    {
        service.Check(Package(boardId: 9), Bootloader(boardId: 33)).IsCompatible.Should().BeTrue();
    }

    [Theory]
    [InlineData(5, 4, null, "compatibility.board-revision-too-old")]
    [InlineData(1, 4, 3, "compatibility.board-revision-too-new")]
    public void BoardRevisionConstraintsAreBlocked(int minimum, int detected, int? maximum, string code)
    {
        service.Check(Package(boardRevision: minimum, boardRevisionMaximum: maximum), Bootloader(boardRevision: detected))
            .Code.Should().Be(code);
    }

    [Fact]
    public void InternalAndExternalCapacityAreBlockedBeforeErase()
    {
        service.Check(Package(imageSize: 17), Bootloader(flashSize: 16)).Code.Should().Be("compatibility.internal-image-too-large");
        service.Check(Package(externalSize: 5), Bootloader(externalSize: 4)).Code.Should().Be("compatibility.external-flash-insufficient");
    }

    [Fact]
    public void BootloaderRevisionAndSecureMetadataAreFailClosed()
    {
        service.Check(Package(minimumBootloader: 5), Bootloader(revision: 4)).Code.Should().Be("compatibility.bootloader-too-old");
        service.Check(Package(requiresSecure: true), Bootloader(isSecure: null)).Code.Should().Be("compatibility.secure-boot-required");
        service.Check(Package(isSigned: false), Bootloader(isSecure: true)).Code.Should().Be("compatibility.signed-image-required");
    }

    private static ApjFirmwarePackage Package(
        int boardId = 50,
        int imageSize = 8,
        int externalSize = 0,
        int boardRevision = 1,
        int? boardRevisionMaximum = null,
        int minimumBootloader = 3,
        bool? requiresSecure = null,
        bool? isSigned = true) =>
        new(boardId, new byte[imageSize], 1024, new byte[externalSize], boardRevision, boardRevisionMaximum,
            minimumBootloader, requiresSecure, isSigned);

    private static BootloaderIdentity Bootloader(
        int boardId = 50,
        int revision = 4,
        long flashSize = 1024,
        int boardRevision = 2,
        long externalSize = 1024,
        bool? isSecure = false) =>
        new(boardId, revision, flashSize, boardRevision, externalSize, isSecure: isSecure);
}
