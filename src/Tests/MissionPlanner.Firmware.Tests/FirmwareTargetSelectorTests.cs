using FluentAssertions;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareTargetSelectorTests
{
    [Fact]
    public void AmbiguousTargetsAreNotAutomaticallySelected()
    {
        var recommendations = FirmwareTargetSelector.Query([Entry(50, "CubeOrange"), Entry(51, "MatekH743")], new());
        FirmwareTargetSelector.UnambiguousHighConfidence(recommendations).Should().BeNull();
    }

    [Fact]
    public void ExactUsbEvidenceProducesOneHighConfidenceRecommendation()
    {
        var usb = new UsbIdentifier(0x2dae, 0x1016);
        var recommendations = FirmwareTargetSelector.Query(
            [Entry(50, "CubeOrange", usb), Entry(51, "MatekH743")], new(),
            [new SerialDeviceDescriptor("COM8", usbIdentifier: usb)]);
        FirmwareTargetSelector.UnambiguousHighConfidence(recommendations)!.Entry.Target.BoardId.Should().Be(50);
    }

    [Theory]
    [InlineData("Cube", 50)]
    [InlineData("Hex Aero", 50)]
    [InlineData("51", 51)]
    public void SearchMatchesPlatformManufacturerAndBoardId(string search, int expectedBoardId)
    {
        var results = FirmwareTargetSelector.Query(
            [Entry(50, "CubeOrange", metadata: new Dictionary<string, string> { ["manufacturer"] = "Hex Aero" }), Entry(51, "MatekH743")],
            new(SearchText: search));
        results.Should().ContainSingle().Which.Entry.Target.BoardId.Should().Be(expectedBoardId);
    }

    [Theory]
    [InlineData(FirmwareReleaseChannel.Stable)]
    [InlineData(FirmwareReleaseChannel.Beta)]
    [InlineData(FirmwareReleaseChannel.Latest)]
    public void ReleaseChannelFilterIsExact(FirmwareReleaseChannel channel)
    {
        var entries = new[] { Entry(50, "Stable", channel: FirmwareReleaseChannel.Stable), Entry(51, "Beta", channel: FirmwareReleaseChannel.Beta), Entry(52, "Latest", channel: FirmwareReleaseChannel.Latest) };
        FirmwareTargetSelector.Query(entries, new(ReleaseChannel: channel)).Should().ContainSingle(item => item.Entry.Channel == channel);
    }

    private static FirmwareManifestEntry Entry(int boardId, string platform, UsbIdentifier? usb = null, FirmwareReleaseChannel channel = FirmwareReleaseChannel.Stable, IReadOnlyDictionary<string, string>? metadata = null) =>
        new(new FirmwareVersion("1.0.0"), channel, new FirmwareBoardTarget(boardId, platform, FirmwareVehicleType.Copter, usb is null ? null : [usb.Value]), new FirmwareArtifact(new Uri($"https://example.test/{platform}.apj"), FirmwareImageFormat.Apj, 100), rawMetadata: metadata);
}
