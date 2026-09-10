using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Workflow;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Tests;

/// <summary>Explicitly opted-in passive hardware checks; never reboots or flashes the controller.</summary>
public sealed class FirmwareRuntimeHardwareTests
{
    /// <summary>Gets whether a hardware endpoint was explicitly provided by the operator.</summary>
    public static bool Enabled => OperatingSystem.IsWindows()
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MP_FIRMWARE_RUNTIME_PORT"));

    [Fact(Skip = "Set MP_FIRMWARE_RUNTIME_PORT to explicitly enable passive hardware probing.", SkipUnless = nameof(Enabled))]
    [Trait("Category", "ManualHardware")]
    public async Task PassiveProbeIdentifiesArduPilotAndReleasesPortForSecondProbe()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMavLinkServices(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var parser = new CountingParser(provider.GetRequiredService<IMavLinkFrameParser>());
        var gateway = new TemporaryMavLinkBootloaderGateway(new SystemFirmwareSerialPortFactory(),
            parser, provider.GetRequiredService<IMavLinkMessageDecodeHandler>(),
            provider.GetRequiredService<IMavLinkCommandEncoder>(), Options.Create(new FirmwareOptions()),
            NullLogger<TemporaryMavLinkBootloaderGateway>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var device = new SerialDeviceDescriptor(Environment.GetEnvironmentVariable("MP_FIRMWARE_RUNTIME_PORT")!);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = await gateway.ProbeAsync(device, deadline.Token);
            Assert.True(result.Runtime == FirmwareRuntimeKind.ArduPilot, $"{device.PortName}: {result.Code} ({result.Outcome}); bytes={parser.Bytes}, frames={string.Join(',', parser.Messages)}");
            Assert.Equal(FirmwareRuntimeVerification.Verified, result.Verification);
            Assert.Equal(FirmwareRuntimeEvidence.MavLinkProbe, result.Evidence);
            Assert.Equal("Application", result.OperatingMode);
        }
        var discovery = new FirmwareDeviceIdentityService(new UnexpectedMspProbe(), new DisconnectedGateway(),
            new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance), TimeProvider.System,
            runtimeVerifier: gateway);
        var enriched = await discovery.EnrichAsync([device], forceRefresh: true, cancellationToken: deadline.Token);
        Assert.Equal(FirmwareRuntimeKind.ArduPilot, enriched[0].RuntimeProbe!.Runtime);
        Assert.Equal(FirmwareRuntimeVerification.Verified, enriched[0].RuntimeProbe!.Verification);
        Assert.Null(enriched[0].BootloaderIdentity);
    }

    private sealed class UnexpectedMspProbe : IBetaflightDeviceProbe
    {
        public Task<BetaflightProbeResult> ProbeAsync(string portName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Verified ArduPilot must not fall through to MSP.");
        }
    }

    private sealed class DisconnectedGateway : IFirmwareConnectionGateway
    {
        public bool IsVehicleConnected => false;
        public ConnectionTransportKind? ActiveTransportKind => null;
        public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CountingParser(IMavLinkFrameParser inner) : IMavLinkFrameParser
    {
        public int Bytes { get; private set; }
        public List<uint> Messages { get; } = [];
        public void Reset() => inner.Reset();
        public IReadOnlyList<MissionPlanner.MavLink.MavLinkFrame> Parse(ReadOnlySpan<byte> data,
            MissionPlanner.Transport.TransportEndPoint endpoint, DateTimeOffset receivedAt)
        {
            Bytes += data.Length;
            var frames = inner.Parse(data, endpoint, receivedAt);
            Messages.AddRange(frames.Select(frame => frame.MessageId));
            return frames;
        }
    }
}
