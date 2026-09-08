using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Betaflight.Protocol;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class BetaflightRebootTests
{
    [Theory]
    [InlineData(false, false, true, BootloaderEntryOutcome.DfuRebootInitiated)]
    [InlineData(false, true, true, BootloaderEntryOutcome.DfuRebootInitiated)]
    [InlineData(false, true, false, BootloaderEntryOutcome.Failed)]
    [InlineData(true, false, true, BootloaderEntryOutcome.Failed)]
    public async Task RebootRequiresDisarmedProofAndDefiniteWrite(bool armed, bool disconnect, bool written, BootloaderEntryOutcome outcome)
    {
        var device = Device();
        var port = new Port();
        var client = new Client { Armed = armed, Disconnect = disconnect, Written = written };
        var catalog = new Catalog(device, client);
        var strategy = new BetaflightMspBootloaderEntryStrategy(new(new Ports(port), TimeProvider.System), client,
            catalog, new Connection(), TimeProvider.System);
        var result = await strategy.TryEnterAsync(new(new(device), device) { Target = BootloaderEntryTarget.Stm32RomDfu },
            TestContext.Current.CancellationToken);
        Assert.Equal(outcome, result.Outcome);
        Assert.False(port.IsOpen);
        Assert.Equal(!armed, client.RebootSent);
        if (client.RebootSent)
        {
            Assert.Equal(new byte[] { (byte)MspRebootMode.RomBootloader }, client.Payload);
        }
    }

    [Fact]
    public async Task UnprovenOrWrongTargetDoesNotOpenPort()
    {
        var device = Device() with { BetaflightIdentity = null };
        var ports = new Ports(new Port());
        var client = new Client();
        var strategy = new BetaflightMspBootloaderEntryStrategy(new(ports, TimeProvider.System), client,
            new Catalog(device, client), new Connection(), TimeProvider.System);
        Assert.Equal(BootloaderEntryOutcome.NotApplicable, (await strategy.TryEnterAsync(new(new(device), device)
            { Target = BootloaderEntryTarget.Stm32RomDfu }, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(BootloaderEntryOutcome.NotApplicable, (await strategy.TryEnterAsync(new(new(Device()), Device()),
            TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(0, ports.Opens);
    }

    private static SerialDeviceDescriptor Device() => new("COM11", "physical", arrivedAt: DateTimeOffset.UnixEpoch)
    {
        BetaflightIdentity = new("COM11", new Version(1, 46), "BTFL", McuType: "STM32F405", McuUniqueId: new string('0', 24))
    };

    [Fact]
    public async Task MissingAckWithPresentDeviceExpiresWithoutSuccess()
    {
        var clock = new MissionPlanner.Test.Support.ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var client = new Client { Disconnect = true };
        var device = Device();
        var strategy = new BetaflightMspBootloaderEntryStrategy(new(new Ports(new Port()), clock), client,
            new Catalog(device, client, true), new Connection(), clock);
        var pending = strategy.TryEnterAsync(new(new(device), device) { Target = BootloaderEntryTarget.Stm32RomDfu },
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(4));
        var result = await pending;
        Assert.Equal("betaflight.serial-did-not-disappear", result.Code);
    }

    [Fact]
    public async Task StaleUidAndCancellationNeverSendReboot()
    {
        var client = new Client();
        var device = Device() with { BetaflightIdentity = Device().BetaflightIdentity! with { McuUniqueId = new string('F', 24) } };
        var strategy = new BetaflightMspBootloaderEntryStrategy(new(new Ports(new Port()), TimeProvider.System), client,
            new Catalog(device, client), new Connection(), TimeProvider.System);
        var context = new BootloaderEntryContext(new(device), device) { Target = BootloaderEntryTarget.Stm32RomDfu };
        Assert.Equal("betaflight.identity-stale", (await strategy.TryEnterAsync(context, TestContext.Current.CancellationToken)).Code);
        Assert.False(client.RebootSent);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => strategy.TryEnterAsync(context, cancelled.Token));
    }

    private sealed class Client : IBetaflightMspClient
    {
        public bool Armed { get; init; }
        public bool Disconnect { get; init; }
        public bool Written { get; init; } = true;
        public bool RebootSent { get; private set; }
        public byte[]? Payload { get; private set; }
        public Task<MspResponse> RequestAsync(IFirmwareSerialPort port, ushort command, ReadOnlyMemory<byte> payload,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (command == MspCommand.Reboot)
            {
                RebootSent = true;
                Payload = payload.ToArray();
                if (Disconnect)
                {
                    return Task.FromResult(new MspResponse(MspFailure.Disconnected, RequestWritten: Written));
                }
            }
            byte[] data = command switch
            {
                MspCommand.FcVariant => "BTFL"u8.ToArray(),
                MspCommand.Uid => new byte[12],
                MspCommand.BoxIds => [1, 0],
                MspCommand.Status => [0, 0, 0, 0, 0, 0, (byte)(Armed ? 2 : 0), 0, 0, 0],
                _ => [1]
            };
            return Task.FromResult(new MspResponse(MspFailure.None, new(MspProtocolVersion.V1, command, data, false), true));
        }
    }

    private sealed class Catalog(SerialDeviceDescriptor device, Client client, bool keepPresent = false) : IFirmwareSerialDeviceCatalog
    {
        public Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SerialDeviceDescriptor>>(client.RebootSent && !keepPresent ? [] : [device]);
    }

    private sealed class Ports(Port port) : IFirmwareSerialPortFactory
    {
        public int Opens { get; private set; }
        public Task<IFirmwareSerialPort> OpenAsync(SerialPortOpenOptions options, CancellationToken cancellationToken = default)
        {
            Opens++;
            return Task.FromResult<IFirmwareSerialPort>(port);
        }
    }

    private sealed class Port : IFirmwareSerialPort
    {
        public string PortName => "COM11";
        public Stream Stream => Stream.Null;
        public bool IsOpen { get; private set; } = true;
        public ValueTask DisposeAsync()
        {
            IsOpen = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Connection : IFirmwareConnectionGateway
    {
        public bool IsVehicleConnected => false;
        public ConnectionTransportKind? ActiveTransportKind => null;
        public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
