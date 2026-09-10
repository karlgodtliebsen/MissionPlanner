using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Tests;

/// <summary>
/// Task 05 R8: the Betaflight MSP probe and the isolated MAVLink runtime probe are strictly
/// sequential for one serial endpoint; they must never run concurrently against the same COM port.
/// </summary>
public sealed class FirmwareProbeSerializationTests
{
    [Fact]
    public async Task MspAndMavLinkProbesNeverRunConcurrentlyForOneEndpoint()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        var monitor = new ConcurrencyMonitor();
        var probe = new OrderedProbe(monitor);
        var verifier = new OrderedVerifier(monitor);
        var service = new FirmwareDeviceIdentityService(probe, new Connection(),
            new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance), TimeProvider.System,
            runtimeVerifier: verifier);

        await service.EnrichAsync([new SerialDeviceDescriptor("COM10")], cancellationToken: TestContext.Current.CancellationToken);

        // At no point did more than one probe touch the endpoint.
        Assert.Equal(1, monitor.MaxConcurrent);
        // MAVLink releases the endpoint before the MSP fallback starts.
        Assert.Equal(new[] { "mav-start", "mav-end", "msp-start", "msp-end" }, monitor.Events);
    }

    private sealed class ConcurrencyMonitor
    {
        private readonly object sync = new();
        private int active;
        public int MaxConcurrent { get; private set; }
        public List<string> Events { get; } = [];

        public void Enter(string name)
        {
            lock (sync)
            {
                active++;
                MaxConcurrent = Math.Max(MaxConcurrent, active);
                Events.Add($"{name}-start");
            }
        }

        public void Exit(string name)
        {
            lock (sync)
            {
                active--;
                Events.Add($"{name}-end");
            }
        }
    }

    private sealed class OrderedProbe(ConcurrencyMonitor monitor) : IBetaflightDeviceProbe
    {
        public async Task<BetaflightProbeResult> ProbeAsync(string portName, CancellationToken cancellationToken = default)
        {
            monitor.Enter("msp");
            await Task.Yield();
            monitor.Exit("msp");
            // Neither protocol identifies this endpoint.
            return new BetaflightProbeResult(BetaflightProbeOutcome.NotMsp);
        }
    }

    private sealed class OrderedVerifier(ConcurrencyMonitor monitor) : IArduPilotRuntimeVerifier
    {
        public async Task<ArduPilotRuntimeIdentity?> VerifyAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken = default)
        {
            monitor.Enter("mav");
            await Task.Yield();
            monitor.Exit("mav");
            return null;
        }
    }

    private sealed class Connection : IFirmwareConnectionGateway
    {
        public bool IsVehicleConnected => false;
        public ConnectionTransportKind? ActiveTransportKind => null;
        public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
