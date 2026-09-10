using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Firmware.Tests;

/// <summary>
/// Task 02 Step 1: an existing Mission Planner vehicle session that owns the selected serial
/// endpoint is reused for authoritative runtime identity, without reopening or stealing the port.
/// </summary>
public sealed class FirmwareExistingSessionReuseTests
{
    [Fact]
    public async Task ExistingArduPilotSessionIsReusedWithoutReopeningThePort()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var probe = new CountingProbe();
        var verifier = new CountingVerifier();
        var connection = new Connection
        {
            IsVehicleConnected = true,
            ActiveTransportKind = ConnectionTransportKind.Serial,
            ActiveSerialPort = "COM10",
            OwnedRuntime = FirmwareRuntimeKind.ArduPilot
        };
        var service = new FirmwareDeviceIdentityService(probe, connection,
            new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance), TimeProvider.System,
            runtimeVerifier: verifier);

        var result = await service.EnrichAsync([new SerialDeviceDescriptor("COM10")],
            cancellationToken: TestContext.Current.CancellationToken);

        // No reopen: neither the MSP probe nor the isolated MAVLink verifier touched the owned port.
        Assert.Equal(0, probe.Calls);
        Assert.Equal(0, verifier.Calls);
        var runtime = result[0].RuntimeProbe;
        Assert.NotNull(runtime);
        Assert.Equal(FirmwareRuntimeKind.ArduPilot, runtime!.Runtime);
        Assert.Equal(FirmwareRuntimeEvidence.ExistingVehicleSession, runtime.Evidence);
        Assert.Equal(FirmwareRuntimeVerification.Verified, runtime.Verification);
        Assert.Equal(FirmwareBootEnvironment.None, runtime.BootEnvironment);
        Assert.Equal(BetaflightProbeOutcome.PortBusy, result[0].BetaflightProbeOutcome);
        // The reused session never establishes an exact board.
        Assert.Null(result[0].BootloaderIdentity);
    }

    [Fact]
    public async Task OwnedNonArduPilotPortIsNotForciblyTakenAndStaysUnresolved()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var probe = new CountingProbe();
        var verifier = new CountingVerifier();
        var connection = new Connection
        {
            IsVehicleConnected = true,
            ActiveTransportKind = ConnectionTransportKind.Serial,
            ActiveSerialPort = "COM10",
            OwnedRuntime = null // e.g. a non-ArduPilot autopilot or an unrelated serial session
        };
        var service = new FirmwareDeviceIdentityService(probe, connection,
            new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance), TimeProvider.System,
            runtimeVerifier: verifier);

        var result = await service.EnrichAsync([new SerialDeviceDescriptor("COM10")],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, probe.Calls);
        Assert.Equal(0, verifier.Calls);
        Assert.Equal(BetaflightProbeOutcome.PortBusy, result[0].BetaflightProbeOutcome);
        // No runtime is fabricated from a busy port we may not open.
        Assert.Equal(FirmwareRuntimeKind.Unknown, result[0].RuntimeProbe!.Runtime);
        Assert.NotEqual(FirmwareRuntimeEvidence.ExistingVehicleSession, result[0].RuntimeProbe!.Evidence);
    }

    private sealed class CountingProbe : IBetaflightDeviceProbe
    {
        public int Calls { get; private set; }

        public Task<BetaflightProbeResult> ProbeAsync(string portName, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new BetaflightProbeResult(BetaflightProbeOutcome.NotMsp));
        }
    }

    private sealed class CountingVerifier : IArduPilotRuntimeVerifier
    {
        public int Calls { get; private set; }

        public Task<ArduPilotRuntimeIdentity?> VerifyAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<ArduPilotRuntimeIdentity?>(new ArduPilotRuntimeIdentity(1, 1, null));
        }
    }

    private sealed class Connection : IFirmwareConnectionGateway
    {
        public bool IsVehicleConnected { get; set; }
        public ConnectionTransportKind? ActiveTransportKind { get; set; }
        public string? ActiveSerialPort { get; set; }
        public FirmwareRuntimeKind? OwnedRuntime { get; set; }
        public FirmwareRuntimeKind? IdentifyOwnedSerialRuntime(string? portName) => OwnedRuntime;
        public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
