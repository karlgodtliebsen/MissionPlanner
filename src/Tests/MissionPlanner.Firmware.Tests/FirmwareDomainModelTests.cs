using FluentAssertions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareDomainModelTests
{
    [Fact]
    public void ValueObjectsUseValueEquality()
    {
        var first = new UsbIdentifier(0x1209, 0x5740);
        var second = new UsbIdentifier(0x1209, 0x5740);

        first.Should().Be(second);
        new FirmwareVersion("4.6.1", new Version(4, 6, 1))
            .Should().Be(new FirmwareVersion("4.6.1", new Version(4, 6, 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void UsbIdentifierRejectsInvalidValues(int value)
    {
        var act = () => new UsbIdentifier(value, 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ArtifactRejectsInvalidUrlsSizesAndHashes()
    {
        var relativeUrl = () => new FirmwareArtifact(new Uri("firmware.apj", UriKind.Relative), FirmwareImageFormat.Apj, 10);
        var fileUrl = () => new FirmwareArtifact(new Uri("file:///firmware.apj"), FirmwareImageFormat.Apj, 10);
        var empty = () => new FirmwareArtifact(new Uri("https://firmware.example/fw.apj"), FirmwareImageFormat.Apj, 0);
        var emptyImage = () => new FirmwareArtifact(new Uri("https://firmware.example/fw.apj"), FirmwareImageFormat.Apj, imageSize: 0);
        var badHash = () => new FirmwareArtifact(new Uri("https://firmware.example/fw.apj"), FirmwareImageFormat.Apj, 10, "xyz");

        relativeUrl.Should().Throw<ArgumentException>();
        fileUrl.Should().Throw<ArgumentException>();
        empty.Should().Throw<ArgumentOutOfRangeException>();
        emptyImage.Should().Throw<ArgumentOutOfRangeException>();
        badHash.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BoardTargetRejectsInvalidIdentity()
    {
        var invalidBoard = () => new FirmwareBoardTarget(0, "CubeOrange", FirmwareVehicleType.Copter);
        var missingPlatform = () => new FirmwareBoardTarget(50, " ", FirmwareVehicleType.Copter);

        invalidBoard.Should().Throw<ArgumentOutOfRangeException>();
        missingPlatform.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ManifestEntryRejectsInvalidReleaseData()
    {
        var version = new FirmwareVersion("4.6.1");
        var target = new FirmwareBoardTarget(50, "CubeOrange", FirmwareVehicleType.Copter);
        var artifact = new FirmwareArtifact(new Uri("https://firmware.example/fw.apj"), FirmwareImageFormat.Apj, 10);

        var customRelease = () => new FirmwareManifestEntry(version, FirmwareReleaseChannel.Custom, target, artifact);
        var invalidSha = () => new FirmwareManifestEntry(version, FirmwareReleaseChannel.Stable, target, artifact, "not-a-sha");

        customRelease.Should().Throw<ArgumentException>();
        invalidSha.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProgressRejectsInconsistentMeasurements()
    {
        var invalidPercentage = () => new FirmwareProgress(FirmwareOperationState.Downloading, 101, "download.progress");
        var invalidBytes = () => new FirmwareProgress(FirmwareOperationState.Downloading, 50, "download.progress", 11, 10);

        invalidPercentage.Should().Throw<ArgumentOutOfRangeException>();
        invalidBytes.Should().Throw<ArgumentException>();
    }
}
