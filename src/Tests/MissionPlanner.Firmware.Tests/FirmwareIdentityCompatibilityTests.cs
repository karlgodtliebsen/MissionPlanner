using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

/// <summary>Regression matrix for wrong-target recovery and exact artifact identity.</summary>
public sealed class FirmwareIdentityCompatibilityTests
{
    /// <summary>Normal upgrades remain strict; recovery requires matching bootloader evidence.</summary>
    [Theory]
    [InlineData(1002, 0, FirmwareInstallMode.NormalUpgrade, FirmwareCompatibilityStatus.Compatible)]
    [InlineData(134, 0, FirmwareInstallMode.NormalUpgrade, FirmwareCompatibilityStatus.RunningFirmwareMismatch)]
    [InlineData(134, 1002, FirmwareInstallMode.Recovery, FirmwareCompatibilityStatus.CompatibleForRecovery)]
    [InlineData(134, 134, FirmwareInstallMode.Recovery, FirmwareCompatibilityStatus.BootloaderMismatch)]
    [InlineData(134, 0, FirmwareInstallMode.Recovery, FirmwareCompatibilityStatus.IdentityInsufficient)]
    public void EvaluatesIdentityAndIntent(int runningBoard, int bootBoard, FirmwareInstallMode mode, FirmwareCompatibilityStatus expected)
    {
        var snapshot = Snapshot(runningBoard, bootBoard);
        var result = FirmwareIdentityCompatibility.Evaluate(snapshot, mode);
        Assert.Equal(expected, result.Status);
        Assert.Equal(expected is FirmwareCompatibilityStatus.Compatible or FirmwareCompatibilityStatus.CompatibleForRecovery, result.CanProceed);
        Assert.Equal(expected == FirmwareCompatibilityStatus.CompatibleForRecovery, result.RequiresExplicitConfirmation);
        Assert.NotEmpty(result.Evidence);
    }

    /// <summary>Sharing a board ID never makes different target variants interchangeable.</summary>
    [Fact]
    public void RejectsDifferentVariantWithSameBoardId()
    {
        var snapshot = Snapshot(1002, 1002);
        snapshot = snapshot with { Selected = snapshot.Selected! with { Target = "omnibusf4-heli" } };
        Assert.Equal(FirmwareCompatibilityStatus.RunningFirmwareMismatch,
            FirmwareIdentityCompatibility.Evaluate(snapshot, FirmwareInstallMode.NormalUpgrade).Status);
    }

    /// <summary>Embedded APJ metadata defeats misleading user/file names.</summary>
    [Fact]
    public void RejectsMisnamedApj()
    {
        var package = new ApjFirmwarePackage(134, new byte[] { 1 }, 1024, summary: "speedybeef4");
        Assert.Equal(FirmwareCompatibilityStatus.TargetMismatch,
            FirmwareIdentityCompatibility.Evaluate(Snapshot(134, 1002), FirmwareInstallMode.Recovery, package).Status);
    }

    /// <summary>Recovery still blocks incompatible chips and explicit vehicle variants.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RejectsMcuAndVehicleMismatch(bool mcu)
    {
        var snapshot = Snapshot(134, 1002) with { Physical = new("STM32H743", null, null) };
        var package = new ApjFirmwarePackage(1002, new byte[] { 1 }, 1024, summary: "omnibusf4",
            rawMetadata: mcu ? new Dictionary<string, string> { ["mcu"] = "\"STM32F405\"" }
                : new Dictionary<string, string> { ["vehicle_type"] = "\"Plane\"" });
        Assert.Equal(mcu ? FirmwareCompatibilityStatus.PhysicalDeviceMismatch : FirmwareCompatibilityStatus.VehicleTypeMismatch,
            FirmwareIdentityCompatibility.Evaluate(snapshot, FirmwareInstallMode.Recovery, package).Status);
    }

    /// <summary>Actual COM12 bootloader evidence blocks recovery by board ID, not a broad MCU description.</summary>
    [Fact]
    public void ObservedCom12ConflictHasBootloaderProvenance()
    {
        var snapshot = Snapshot(134, 134) with { Bootloader = new(134, 5, 983040, chipDescription: "STM32F40x,?") };
        var package = new ApjFirmwarePackage(1002, new byte[] { 1 }, 1024,
            summary: "omnibusf4", description: "Firmware for a STM32F405xx board");
        Assert.Equal(FirmwareCompatibilityStatus.BootloaderMismatch,
            FirmwareIdentityCompatibility.Evaluate(snapshot, FirmwareInstallMode.Recovery, package).Status);
        snapshot = snapshot with { Bootloader = new(1002, 5, 983040, chipDescription: "STM32F40x,?") };
        Assert.Equal(FirmwareCompatibilityStatus.CompatibleForRecovery,
            FirmwareIdentityCompatibility.Evaluate(snapshot, FirmwareInstallMode.Recovery, package).Status);
    }

    /// <summary>Unknown values are omitted instead of being displayed as physical zeroes.</summary>
    [Fact]
    public void PresentationOmitsUnknownFields()
    {
        var text = FirmwareIdentityPresentation.Format(new(new(null, null, null), null, null, null));
        Assert.DoesNotContain("Board ID:", text);
        Assert.DoesNotContain("MCU:", text);
        Assert.DoesNotContain("Detected", text);
    }

    private static FirmwareIdentitySnapshot Snapshot(int runningBoard, int bootBoard)
    {
        var telemetry = new VehicleFirmwareIdentity(FirmwareFamily.ArduCopter, 2, 3,
            new(4, 7, 1, FirmwareReleaseType.Official), null, 0, (uint)runningBoard << 16, 0, 0, null, null);
        return new(null, bootBoard > 0 ? new(bootBoard, 5, 1024) : null,
            RunningFirmwareIdentity.FromTelemetry(telemetry, [$"{(runningBoard == 134 ? "speedybeef4" : "omnibusf4")} 003D0052 32355116 38393232"]),
            new("omnibusf4", 1002, FirmwareVehicleType.Copter, "4.7.1", null, null, FirmwareIdentitySource.OfficialCatalogue));
    }
}
