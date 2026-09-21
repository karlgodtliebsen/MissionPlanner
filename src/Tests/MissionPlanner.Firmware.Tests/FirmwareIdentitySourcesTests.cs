using MissionPlanner.Firmware.Model;
namespace MissionPlanner.Firmware.Tests;

/// <summary>Protects identity provenance against misleading installed firmware and filenames.</summary>
public sealed class FirmwareIdentitySourcesTests
{
    /// <summary>Real APJ container 0.1 metadata cannot hide the decoded application's release.</summary>
    [Fact]
    public void ReadsEmbeddedApplicationVersionAndVehicle()
    {
        var image = new byte[40];
        new byte[] { 0xFB, 0x72, 0x65, 0x76, 0x77, 0x66, 0x70, 0x61 }.CopyTo(image, 0);
        image[9] = 2;
        image[10] = 4;
        image[12] = 2;
        image[16] = 4;
        image[17] = 7;
        image[18] = 1;
        var package = new ApjFirmwarePackage(1002, image, 1024, summary: "omnibusf4", version: "0.1");
        Assert.Equal("4.7.1", package.Identity.Version);
        Assert.Equal(FirmwareVehicleType.Copter, package.Identity.VehicleType);
        Assert.True(package.Identity.HasVerifiableRelease);
    }

    /// <summary>Conflicting startup evidence is visible and cannot resolve as compatible.</summary>
    [Fact]
    public void ConflictingTargetsRemainAmbiguous()
    {
        var telemetry = new VehicleFirmwareIdentity(FirmwareFamily.ArduCopter, 2, 3, null, null, 0, 134u << 16, 0, 0, null, null);
        var running = RunningFirmwareIdentity.FromTelemetry(telemetry,
            ["speedybeef4 003D0052 32355116 38393232", "omnibusf4 003D0052 32355116 38393232"]);
        Assert.True(running.IsAmbiguous);
        Assert.Null(running.Target);
    }

    /// <summary>Application board claims remain separate from physical and bootloader evidence.</summary>
    [Fact]
    public void KeepsWrongRunningTargetSeparate()
    {
        var telemetry = new VehicleFirmwareIdentity(FirmwareFamily.ArduCopter, 2, 3,
            new(4, 7, 1, FirmwareReleaseType.Official), "abcd1234", 0, 134u << 16, 0x1209, 0x5741, null, "uid");
        var running = RunningFirmwareIdentity.FromTelemetry(telemetry,
            ["ChibiOS: abcd1234", "speedybeef4 003D0052 32355116 38393232"]);
        var snapshot = new FirmwareIdentitySnapshot(new(null, new(0x1209, 0x5741), "uid"), null, running,
            new("omnibusf4", 1002, FirmwareVehicleType.Copter, "4.7.1", null, null, FirmwareIdentitySource.UserSelection));
        Assert.Equal(134, snapshot.Running!.BoardId);
        Assert.Equal("speedybeef4", snapshot.Running.Target);
        Assert.Equal(FirmwareIdentitySource.AutopilotVersion, snapshot.Running.BoardIdSource);
        Assert.Null(snapshot.Bootloader);
        Assert.Null(snapshot.Physical!.McuFamily);
        Assert.Equal(1002, snapshot.Selected!.BoardId);
    }

    /// <summary>APJ container version is not a firmware version, and absent vehicle metadata is unknown.</summary>
    [Fact]
    public void EmbeddedIdentityDoesNotInventMissingFields()
    {
        var package = new ApjFirmwarePackage(134, new byte[] { 1 }, 1024, summary: "speedybeef4",
            version: "0.1", rawMetadata: new Dictionary<string, string> { ["version"] = "\"0.1\"" });
        Assert.Equal(134, package.Identity.BoardId);
        Assert.Equal("speedybeef4", package.Identity.Target);
        Assert.Equal(FirmwareVehicleType.Unknown, package.Identity.VehicleType);
        Assert.Null(package.Identity.Version);
    }
}
