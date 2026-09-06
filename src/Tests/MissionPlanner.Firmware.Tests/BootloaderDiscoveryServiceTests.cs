using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Protocol;

namespace MissionPlanner.Firmware.Tests;

public sealed class BootloaderDiscoveryServiceTests
{
    [Fact]
    public async Task IgnoresWrongDeviceAndHandlesBootloaderPortChange()
    {
        var application = Device("COM7", "application");
        var unrelated = Device("COM1", "unrelated");
        var bootloader = Device("COM9", "bootloader", "Cube-BL");
        var ports = new FakePortFactory();
        var clients = new FakeClientFactory(new Dictionary<string, BootloaderIdentity> { ["COM9"] = new(50, 4, 1024) });
        var service = CreateService(new FakeCatalog(application, unrelated), new FakeMonitor(bootloader, bootloader), ports, clients);

        await using var found = await service.FindAsync(
            new BootloaderDiscoveryRequest(ExpectedUsbIdentifiers: [new UsbIdentifier(0x2dae, 0x1016)], BootloaderHints: ["-BL"]),
            cancellationToken: TestContext.Current.CancellationToken);

        found.Device.PortName.Should().Be("COM9");
        found.Identity.BoardId.Should().Be(50);
        ports.Opened.Should().Equal("COM1", "COM7", "COM9");
        ports.Ports.Where(pair => pair.Key != "COM9").Should().OnlyContain(pair => pair.Value.Disposed);
        clients.DestructiveCalls.Should().Be(0);
    }

    [Fact]
    public async Task CorrectPortIsReturnedOnlyAfterProtocolIdentification()
    {
        var candidate = Device("COM4", "selected", "Bootloader");
        var ports = new FakePortFactory();
        var clients = new FakeClientFactory(new Dictionary<string, BootloaderIdentity> { ["COM4"] = new(9, 4, 2048) });
        var service = CreateService(new FakeCatalog(candidate), new FakeMonitor(), ports, clients);

        var found = await service.FindAsync(new BootloaderDiscoveryRequest(candidate), cancellationToken: TestContext.Current.CancellationToken);

        found.Identity.BoardId.Should().Be(9);
        clients.IdentifyCalls.Should().Be(1);
        ports.Ports["COM4"].Disposed.Should().BeFalse();
        await found.DisposeAsync();
        ports.Ports["COM4"].Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task ExplicitSelectionIsProbedBeforeUnrelatedCandidates()
    {
        var unrelated = Device("COM1", "unrelated");
        var selected = Device("COM8", "selected");
        var ports = new FakePortFactory();
        var clients = new FakeClientFactory(new Dictionary<string, BootloaderIdentity> { ["COM8"] = new(50, 4, 1024) });
        var service = CreateService(new FakeCatalog(unrelated, selected), new FakeMonitor(), ports, clients);

        await using var found = await service.FindAsync(new BootloaderDiscoveryRequest(selected), cancellationToken: TestContext.Current.CancellationToken);

        found.Device.Should().Be(selected);
        ports.Opened.Should().Equal("COM8");
    }

    [Fact]
    public async Task ReprobesNewBootloaderGenerationOnSameStablePort()
    {
        var application = Device("COM7", "device-42", "Cube");
        var bootloader = Device("COM7", "device-42", "Cube-BL");
        var ports = new FakePortFactory();
        var clients = new SequencedClientFactory(null, new BootloaderIdentity(50, 4, 1024));
        var service = CreateService(new FakeCatalog(application), new FakeMonitor(bootloader), ports, clients);

        await using var found = await service.FindAsync(
            new BootloaderDiscoveryRequest(application),
            cancellationToken: TestContext.Current.CancellationToken);

        found.Device.PortName.Should().Be("COM7");
        found.Identity.BoardId.Should().Be(50);
        clients.IdentifyCalls.Should().Be(2);
        ports.Opened.Should().Equal("COM7", "COM7");
    }

    [Fact]
    public async Task ProtocolTimeoutRejectsApplicationPortAndContinuesMonitoring()
    {
        var application = Device("COM7", "device-42", "Cube");
        var bootloader = Device("COM9", "device-42", "Cube-BL");
        var ports = new FakePortFactory();
        var clients = new TimeoutThenIdentityClientFactory(new BootloaderIdentity(50, 4, 1024));
        var service = CreateService(new FakeCatalog(application), new FakeMonitor(bootloader), ports, clients);

        await using var found = await service.FindAsync(
            new BootloaderDiscoveryRequest(application),
            cancellationToken: TestContext.Current.CancellationToken);

        found.Device.PortName.Should().Be("COM9");
        clients.IdentifyCalls.Should().Be(2);
    }

    [Fact]
    public async Task ReprobesUnchangedPortWithoutDeviceArrivalEvent()
    {
        var device = Device("COM7", "device-42", "Cube");
        var ports = new FakePortFactory();
        var clients = new SequencedClientFactory(null, new BootloaderIdentity(50, 4, 1024));
        var service = CreateService(new FakeCatalog(device), new WaitingMonitor(), ports, clients);

        await using var found = await service.FindAsync(
            new BootloaderDiscoveryRequest(device),
            cancellationToken: TestContext.Current.CancellationToken);

        found.Device.PortName.Should().Be("COM7");
        clients.IdentifyCalls.Should().Be(2);
        ports.Opened.Should().Equal("COM7", "COM7");
    }

    [Fact]
    public async Task TimesOutWhenNoCandidateIdentifies()
    {
        var service = CreateService(new FakeCatalog(), new WaitingMonitor(), new FakePortFactory(),
            new FakeClientFactory(new Dictionary<string, BootloaderIdentity>()));

        var act = async () => await service.FindAsync(
            new BootloaderDiscoveryRequest(Timeout: TimeSpan.FromMilliseconds(20)),
            cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareDeviceNotFoundException>();
    }

    [Fact]
    public async Task SelectedUsbSerialSurvivesPidAndComChangeAndRejectsUnrelatedBootloader()
    {
        var selected = new SerialDeviceDescriptor("COM11", "application-instance", new UsbIdentifier(1, 1), "FC-123");
        var unrelated = new SerialDeviceDescriptor("COM11", "unrelated-instance", new UsbIdentifier(1, 2), "OTHER-FC");
        var bootloader = new SerialDeviceDescriptor("COM14", "bootloader-instance", new UsbIdentifier(1, 2), "FC-123");
        var ports = new FakePortFactory();
        var clients = new FakeClientFactory(new Dictionary<string, BootloaderIdentity>
        {
            ["COM11"] = new(50, 4, 1024),
            ["COM14"] = new(50, 4, 1024)
        });
        var service = CreateService(new FakeCatalog(unrelated), new FakeMonitor(unrelated, bootloader), ports, clients);
        await using var found = await service.FindAsync(new BootloaderDiscoveryRequest(selected), cancellationToken: TestContext.Current.CancellationToken);
        found.Device.Should().Be(bootloader);
        ports.Opened.Should().Equal("COM14");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdentificationDeadlineAndCancellationReleaseOwnedPort(bool cancel)
    {
        var device = Device("COM11", "selected");
        var ports = new FakePortFactory();
        using var cancellation = new CancellationTokenSource();
        var clients = new CancelledClientFactory(cancel ? cancellation : null);
        var service = CreateService(new FakeCatalog(device), new WaitingMonitor(), ports, clients);
        var find = () => service.FindAsync(new BootloaderDiscoveryRequest(device, Timeout: TimeSpan.FromMilliseconds(20)), cancellationToken: cancellation.Token);
        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(find);
        }
        else
        {
            await Assert.ThrowsAsync<FirmwareDeviceNotFoundException>(find);
        }
        ports.Ports["COM11"].Disposed.Should().BeTrue();
    }

    private sealed class CancelledClientFactory(CancellationTokenSource? cancellation) : IArduPilotBootloaderClientFactory
    {
        public IArduPilotBootloaderClient Create(IFirmwareSerialPort port) => new Client(port, cancellation);
        private sealed class Client(IFirmwareSerialPort port, CancellationTokenSource? cancellation) : IArduPilotBootloaderClient
        {
            public async Task<BootloaderIdentity> IdentifyAsync(CancellationToken cancellationToken = default)
            {
                cancellation?.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Should have been cancelled");
            }
            public Task EraseAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task ProgramAsync(ApjFirmwarePackage package, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<FirmwareVerificationResult> VerifyAsync(ApjFirmwarePackage package, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task RebootAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask DisposeAsync() => port.DisposeAsync();
        }
    }

    private static BootloaderDiscoveryService CreateService(
        IFirmwareSerialDeviceCatalog catalog,
        IFirmwareDeviceMonitor monitor,
        IFirmwareSerialPortFactory ports,
        IArduPilotBootloaderClientFactory clients)
    {
        return new BootloaderDiscoveryService(catalog, monitor, ports, clients, Options.Create(new FirmwareOptions
        {
            BootloaderDiscoveryTimeout = TimeSpan.FromSeconds(1),
            BootloaderDiscoveryPollInterval = TimeSpan.FromMilliseconds(5),
            BootloaderPortOpenTimeout = TimeSpan.FromMilliseconds(50)
        }), NullLogger<BootloaderDiscoveryService>.Instance);
    }

    private static SerialDeviceDescriptor Device(string port, string id, string? product = null)
    {
        return new SerialDeviceDescriptor(port, id, new UsbIdentifier(0x2dae, 0x1016), id, product);
    }

    private sealed class FakeCatalog(params SerialDeviceDescriptor[] devices) : IFirmwareSerialDeviceCatalog
    {
        public Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SerialDeviceDescriptor>>(devices);
        }
    }

    private sealed class FakeMonitor(params SerialDeviceDescriptor[] devices) : IFirmwareDeviceMonitor
    {
        public async IAsyncEnumerable<FirmwareDeviceChange> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var device in devices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new FirmwareDeviceChange(FirmwareDeviceChangeKind.Arrived, device, DateTimeOffset.UtcNow);
                await Task.Yield();
            }
        }
    }

    private sealed class WaitingMonitor : IFirmwareDeviceMonitor
    {
        public async IAsyncEnumerable<FirmwareDeviceChange> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }
    }

    private sealed class FakePortFactory : IFirmwareSerialPortFactory
    {
        public List<string> Opened { get; } = [];
        public Dictionary<string, FakePort> Ports { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IFirmwareSerialPort> OpenAsync(SerialPortOpenOptions options, CancellationToken cancellationToken = default)
        {
            Opened.Add(options.PortName);
            var port = new FakePort(options.PortName);
            Ports[options.PortName] = port;
            return Task.FromResult<IFirmwareSerialPort>(port);
        }
    }

    private sealed class FakePort(string name) : IFirmwareSerialPort
    {
        public string PortName => name;
        public Stream Stream { get; } = new MemoryStream();
        public bool IsOpen => !Disposed;
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            Stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeClientFactory(IReadOnlyDictionary<string, BootloaderIdentity> identities) : IArduPilotBootloaderClientFactory
    {
        public int IdentifyCalls { get; private set; }
        public int DestructiveCalls { get; private set; }

        public IArduPilotBootloaderClient Create(IFirmwareSerialPort port)
        {
            return new FakeClient(this, port, identities.GetValueOrDefault(port.PortName));
        }

        private sealed class FakeClient(FakeClientFactory owner, IFirmwareSerialPort port, BootloaderIdentity? identity) : IArduPilotBootloaderClient
        {
            public Task<BootloaderIdentity> IdentifyAsync(CancellationToken cancellationToken = default)
            {
                owner.IdentifyCalls++;
                return identity is null ? Task.FromException<BootloaderIdentity>(new FirmwareBootloaderException("not a bootloader")) : Task.FromResult(identity);
            }

            public Task EraseAsync(CancellationToken cancellationToken = default)
            {
                owner.DestructiveCalls++;
                return Task.CompletedTask;
            }

            public Task ProgramAsync(ApjFirmwarePackage package, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default)
            {
                owner.DestructiveCalls++;
                return Task.CompletedTask;
            }

            public Task<FirmwareVerificationResult> VerifyAsync(ApjFirmwarePackage package, CancellationToken cancellationToken = default)
            {
                owner.DestructiveCalls++;
                return Task.FromResult(new FirmwareVerificationResult(true, 0, 0));
            }

            public Task RebootAsync(CancellationToken cancellationToken = default)
            {
                owner.DestructiveCalls++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                return port.DisposeAsync();
            }
        }
    }

    private sealed class SequencedClientFactory(params BootloaderIdentity?[] identities) : IArduPilotBootloaderClientFactory
    {
        private int index;
        public int IdentifyCalls { get; private set; }

        public IArduPilotBootloaderClient Create(IFirmwareSerialPort port)
        {
            return new FakeClient(this, port, identities[Math.Min(index++, identities.Length - 1)]);
        }

        private sealed class FakeClient(SequencedClientFactory owner, IFirmwareSerialPort port, BootloaderIdentity? identity) : IArduPilotBootloaderClient
        {
            public Task<BootloaderIdentity> IdentifyAsync(CancellationToken cancellationToken = default)
            {
                owner.IdentifyCalls++;
                return identity is null
                    ? Task.FromException<BootloaderIdentity>(new FirmwareBootloaderException("not a bootloader"))
                    : Task.FromResult(identity);
            }

            public Task EraseAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task ProgramAsync(ApjFirmwarePackage package, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<FirmwareVerificationResult> VerifyAsync(ApjFirmwarePackage package, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new FirmwareVerificationResult(true, 0, 0));
            }

            public Task RebootAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                return port.DisposeAsync();
            }
        }
    }

    private sealed class TimeoutThenIdentityClientFactory(BootloaderIdentity identity) : IArduPilotBootloaderClientFactory
    {
        public int IdentifyCalls { get; private set; }

        public IArduPilotBootloaderClient Create(IFirmwareSerialPort port) => new FakeClient(this, port, identity);

        private sealed class FakeClient(TimeoutThenIdentityClientFactory owner, IFirmwareSerialPort port, BootloaderIdentity identity) : IArduPilotBootloaderClient
        {
            public Task<BootloaderIdentity> IdentifyAsync(CancellationToken cancellationToken = default)
            {
                owner.IdentifyCalls++;
                return owner.IdentifyCalls == 1
                    ? Task.FromException<BootloaderIdentity>(new TimeoutException("application port did not answer bootloader protocol"))
                    : Task.FromResult(identity);
            }

            public Task EraseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task ProgramAsync(ApjFirmwarePackage package, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<FirmwareVerificationResult> VerifyAsync(ApjFirmwarePackage package, CancellationToken cancellationToken = default) => Task.FromResult(new FirmwareVerificationResult(true, 0, 0));
            public Task RebootAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => port.DisposeAsync();
        }
    }
}
