using MissionPlanner.Core.Vehicles;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Core.Tests;

public sealed class VehicleSerialConnectionPolicyTests
{
    [Theory]
    [InlineData("STM32 Bootloader")]
    [InlineData("ArduPilot Bootloader")]
    [InlineData("STM32 DFU")]
    [InlineData("Betaflight STM32F405")]
    [InlineData("INAV")]
    public void KnownNonTelemetryDevicesAreBlocked(string product)
        => Assert.NotNull(VehicleSerialConnectionPolicy.GetBlockReason(new("COM4", productName: product)));

    [Theory]
    [InlineData(null)]
    [InlineData("STMicroelectronics Virtual COM Port (COM4)")]
    [InlineData("USB Serial Port")]
    [InlineData("PX4 FMU")]
    [InlineData("ArduPilot")]
    public void GenericPortsRemainCandidatesForHeartbeatVerification(string? product)
        => Assert.Null(VehicleSerialConnectionPolicy.GetBlockReason(new("COM4", productName: product)));

    [Fact]
    public void DfuUsbIdentityIsBlockedWithoutProductName()
        => Assert.NotNull(VehicleSerialConnectionPolicy.GetBlockReason(new("COM4", usbIdentifier: new(0x0483, 0xdf11))));

    [Fact]
    public void ObservedBetaflightRuntimeIsBlockedOnGenericPort()
        => Assert.NotNull(VehicleSerialConnectionPolicy.GetBlockReason(new("COM4")
        {
            RuntimeProbe = new(FirmwareRuntimeKind.Betaflight, FirmwareBootEnvironment.None, "msp")
        }));
}
