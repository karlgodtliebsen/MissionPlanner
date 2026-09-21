using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareUpgradeVerificationTests
{
    private static FirmwareManifestEntry Release() => new(new FirmwareVersion("4.7.1", new Version(4, 7, 1)),
        FirmwareReleaseChannel.Stable, new(1002, "omnibusf4", FirmwareVehicleType.Copter, FirmwareVehicleType.Copter),
        new(new Uri("https://example.test/firmware.apj"), FirmwareImageFormat.Apj), "dbe79216");

    private static VehicleFirmwareIdentity Identity() => new(FirmwareFamily.ArduCopter, 2, 3,
        new(4, 7, 1, FirmwareReleaseType.Official), "6462653739323136", 0, 1002u << 16, 0x1209, 0x5741, 123, "UID");

    [Fact]
    public void AcceptsObservedAsciiGitEncodingAndMatchingIdentity()
    {
        FirmwareUpgradeVerification.Validate(Identity(), Release(), true, Identity());
    }

    [Theory]
    [InlineData("version")]
    [InlineData("family")]
    [InlineData("board")]
    [InlineData("uid")]
    [InlineData("git")]
    [InlineData("missing")]
    [InlineData("variant")]
    public void RejectsWrongOrMissingPostFlashIdentity(string mismatch)
    {
        var identity = mismatch switch
        {
            "variant" => Identity() with { MavType = 4 },
            "version" => Identity() with { FlightVersion = new(4, 7, 0, FirmwareReleaseType.Official) },
            "family" => Identity() with { Family = FirmwareFamily.ArduPlane },
            "board" => Identity() with { BoardVersion = 9u << 16 },
            "uid" => Identity() with { HardwareUid2 = "OTHER" },
            "git" => Identity() with { FlightGitHash = "deadbeef" },
            _ => Identity() with { FlightVersion = null }
        };
        Assert.ThrowsAny<Exception>(() => FirmwareUpgradeVerification.Validate(identity, Release(), true, Identity()));
    }

    [Fact]
    public void WrongTargetRecoveryCannotUseNormalHandoff()
    {
        Assert.Throws<FirmwareCompatibilityException>(() => FirmwareUpgradeVerification.Validate(
            Identity() with { BoardVersion = 9u << 16 }, Release(), false));
    }
}
