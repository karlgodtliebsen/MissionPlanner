namespace MissionPlanner.Firmware.Tests;

/// <summary>Documents manual hardware smoke tests; these are intentionally excluded from CI.</summary>
[Trait("Category", "ManualHardware")]
public sealed class FirmwareHardwareTests
{
    [Theory(Skip = "Requires an explicitly selected physical controller and operator safety procedure.")]
    [InlineData("Supported F4 board")]
    [InlineData("Supported H7 board")]
    [InlineData("Port changes on reboot")]
    [InlineData("Repeated upload")]
    [InlineData("Board mismatch")]
    [InlineData("Manual unplug/replug")]
    [InlineData("Embedded bootloader update command")]
    public void RunDocumentedHardwareSmokeTest(string scenario) =>
        Assert.Fail($"Manual scenario was not executed: {scenario}");
}
