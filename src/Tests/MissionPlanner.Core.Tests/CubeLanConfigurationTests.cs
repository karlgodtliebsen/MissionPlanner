using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.Core.ConfigTuning.VendorDevices;
using MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Generated;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies the repository-documented CubeLAN configuration workflow.</summary>
public sealed class CubeLanConfigurationTests
{
    /// <summary>Verifies the codec projects eight ports and preserves unknown registers.</summary>
    [Fact]
    public void CodecRoundTripsVerifiedFieldsAndUnknownRegisters()
    {
        var codec = new CubeLanConfigurationCodec();
        byte[] document =
        [
            0xaa, 0x55,
            21, 0, 0x0c, 0x00,
            22, 0, 0x00, 0xfe,
            23, 13, 0x00, 0x01,
            23, 15, 0xff, 0xfe,
            99, 7, 0x12, 0x34,
            0x55, 0xaa, 0xff, 0xff, 0xff, 0xff
        ];

        var decoded = codec.Decode(document);
        var encoded = codec.Encode(decoded.Configuration!);
        var roundTrip = codec.Decode(encoded);

        decoded.Success.Should().BeTrue();
        decoded.Configuration!.Ports.Should().HaveCount(8);
        decoded.Configuration.Ports[0].ClassOfServiceEnabled.Should().BeTrue();
        decoded.Configuration.Ports[0].ClassOfServiceHighPriority.Should().BeTrue();
        decoded.Configuration.Ports[0].EnergyEfficientEthernetEnabled.Should().BeFalse();
        decoded.Configuration.Ports[0].VlanTagged.Should().BeTrue();
        decoded.Configuration.VlanMembership.Single(item => item.SourcePort == 0 && item.DestinationPort == 0)
            .IsMember.Should().BeFalse();
        roundTrip.Configuration!.Registers.Should().Contain(new CubeLanRegisterValue(99, 7, 0x1234));
    }

    /// <summary>Verifies fake-device discovery, read, apply, and full readback.</summary>
    [Fact]
    public async Task AdapterDiscoversAppliesAndReadsBackEightPorts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var codec = new CubeLanConfigurationCodec();
        var device = new MemoryDeviceOperationClient(DefaultDocument(codec));
        var adapter = Adapter(device, codec);
        var vehicleId = new VehicleId(1, 1);
        var discovery = await adapter.DiscoverAsync(vehicleId, null, cancellationToken);
        var original = discovery.Snapshot!;
        var ports = original.Configuration.Ports
            .Select(port => port.PortIndex == 3 ? port with { VlanTagged = true, ClassOfServiceEnabled = true } : port)
            .ToArray();
        var memberships = original.Configuration.VlanMembership
            .Select(item => item.SourcePort == 3 && item.DestinationPort == 7 ? item with { IsMember = false } : item)
            .ToArray();
        var desired = original.Configuration with { Ports = ports, VlanMembership = memberships };

        var applied = await adapter.ApplyAsync(vehicleId, original, desired, null, cancellationToken);

        discovery.Status.Should().Be(VendorDeviceStatus.Available);
        original.Configuration.Ports.Should().HaveCount(8);
        applied.Success.Should().BeTrue();
        applied.ConfirmedSnapshot!.Configuration.Ports[3].VlanTagged.Should().BeTrue();
        applied.ConfirmedSnapshot.Configuration.VlanMembership
            .Single(item => item.SourcePort == 3 && item.DestinationPort == 7).IsMember.Should().BeFalse();
        device.WriteCount.Should().BeGreaterThan(0);
    }

    /// <summary>Verifies malformed discovery is reported as unsupported without writes.</summary>
    [Fact]
    public async Task AdapterReportsUnsupportedForUnknownConfigurationEnvelope()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var codec = new CubeLanConfigurationCodec();
        var device = new MemoryDeviceOperationClient(new byte[100]);
        var adapter = Adapter(device, codec);

        var result = await adapter.DiscoverAsync(new VehicleId(1, 1), null, cancellationToken);

        result.Status.Should().Be(VendorDeviceStatus.Unsupported);
        result.Snapshot.Should().BeNull();
        device.WriteCount.Should().Be(0);
    }

    /// <summary>Verifies incomplete eight-port models are rejected before device I/O.</summary>
    [Fact]
    public async Task AdapterValidationBlocksIncompletePortMatrix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var codec = new CubeLanConfigurationCodec();
        var device = new MemoryDeviceOperationClient(DefaultDocument(codec));
        var adapter = Adapter(device, codec);
        var original = await adapter.ReadAsync(new VehicleId(1, 1), null, cancellationToken);
        var invalid = original.Configuration with { Ports = original.Configuration.Ports.Take(7).ToArray() };

        var result = await adapter.ApplyAsync(original.VehicleId, original, invalid, null, cancellationToken);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("exactly one configuration");
        device.WriteCount.Should().Be(0);
    }

    /// <summary>Verifies a failed confirmed write triggers and confirms rollback.</summary>
    [Fact]
    public async Task AdapterRollsBackWhenWriteCannotBeConfirmed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var codec = new CubeLanConfigurationCodec();
        var originalDocument = DefaultDocument(codec);
        var device = new MemoryDeviceOperationClient(originalDocument);
        var adapter = Adapter(device, codec);
        var vehicleId = new VehicleId(1, 1);
        var original = await adapter.ReadAsync(vehicleId, null, cancellationToken);
        var desired = original.Configuration with
        {
            Ports = original.Configuration.Ports
                .Select(port => port.PortIndex == 0
                    ? port with { ClassOfServiceEnabled = true, EnergyEfficientEthernetEnabled = false }
                    : port)
                .ToArray()
        };
        var desiredDocument = codec.Encode(desired);
        device.FailWriteAtOffset = Enumerable.Range(0, desiredDocument.Length)
            .Last(index => desiredDocument[index] != originalDocument[index]);

        var result = await adapter.ApplyAsync(vehicleId, original, desired, null, cancellationToken);

        result.Success.Should().BeFalse();
        result.RollbackAttempted.Should().BeTrue();
        result.RollbackSucceeded.Should().BeTrue();
        device.Memory[..originalDocument.Length].Should().Equal(originalDocument);
    }

    /// <summary>Verifies authentication objects redact and do not serialize their secret.</summary>
    [Fact]
    public void AuthenticationRedactsSecret()
    {
        var authentication = new VendorDeviceAuthentication("operator", "highly-sensitive");

        authentication.ToString().Should().Contain("operator").And.NotContain("highly-sensitive");
        System.Text.Json.JsonSerializer.Serialize(authentication).Should().NotContain("highly-sensitive");
    }

    /// <summary>Verifies exports omit preserved raw registers.</summary>
    [Fact]
    public void ExportContainsOnlyTheVerifiedPublicConfiguration()
    {
        var codec = new CubeLanConfigurationCodec();
        var configuration = codec.Decode([
            0xaa, 0x55,
            99, 7, 0x12, 0x34,
            0x55, 0xaa, 0xff, 0xff, 0xff, 0xff
        ]).Configuration!;
        var adapter = Adapter(new MemoryDeviceOperationClient([]), codec);

        var exported = adapter.Export(configuration);

        exported.Should().Contain("vlanMembership");
        exported.ToLowerInvariant().Should().NotContain("registers");
        exported.Should().NotContain("4660");
    }

    /// <summary>Verifies the UI keeps an unsupported discovery state explicit and non-editable.</summary>
    [Fact]
    public async Task ViewModelShowsUnsupportedDeviceState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var state = OnlineState();
        var context = new TestActiveVehicleContext(state);
        var adapter = Substitute.For<IVendorDeviceAdapter<CubeLanConfiguration>>();
        adapter.DiscoverAsync(state.VehicleId, null, Arg.Any<CancellationToken>()).Returns(
            new VendorDeviceDiscoveryResult<CubeLanConfiguration>(
                VendorDeviceStatus.Unsupported,
                "The response does not match the verified CubeLAN envelope.",
                null));
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!();
            return true;
        });
        using var viewModel = new CubeLan8PortSwitchTabViewModel(
            context,
            adapter,
            new ParametersFileHandler(Substitute.For<IFileSaver>()),
            Substitute.For<IUserConfirmationService>(),
            dispatcher,
            NullLogger<CubeLan8PortSwitchTabViewModel>.Instance);

        await viewModel.RefreshAsync().WaitAsync(cancellationToken);

        viewModel.Status.Should().Be(VendorDeviceStatus.Unsupported);
        viewModel.CanEdit.Should().BeFalse();
        viewModel.Ports.Should().BeEmpty();
        viewModel.StatusMessage.Should().Contain("verified CubeLAN envelope");
    }

    private static CubeLanDeviceAdapter Adapter(
        IDeviceOperationClient client,
        ICubeLanConfigurationCodec codec)
    {
        return new CubeLanDeviceAdapter(client, codec, NullLogger<CubeLanDeviceAdapter>.Instance);
    }

    private static byte[] DefaultDocument(ICubeLanConfigurationCodec codec)
    {
        var decoded = codec.Decode([0xaa, 0x55, 0x55, 0xaa, 0xff, 0xff, 0xff, 0xff]);
        return codec.Encode(decoded.Configuration!);
    }

    private static VehicleState OnlineState()
    {
        var now = DateTimeOffset.UtcNow;
        return new VehicleState(
            new VehicleId(1, 1),
            0,
            2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            now,
            VehicleMode.Stabilize,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private sealed class MemoryDeviceOperationClient(byte[] initial) : IDeviceOperationClient
    {
        public byte[] Memory { get; } = CreateMemory(initial);

        public int WriteCount { get; private set; }

        public int? FailWriteAtOffset { get; set; }

        public Task<DeviceOperationResult> ReadAsync(
            VehicleId vehicleId,
            DeviceOpBustype busType,
            byte bus,
            byte address,
            byte registerStart,
            byte count,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DeviceOperationResult(0, registerStart, Memory.AsSpan(registerStart, count).ToArray()));
        }

        public Task<DeviceOperationResult> WriteAsync(
            VehicleId vehicleId,
            DeviceOpBustype busType,
            byte bus,
            byte address,
            byte registerStart,
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCount++;
            if (FailWriteAtOffset == registerStart)
            {
                return Task.FromResult(new DeviceOperationResult(1, registerStart, []));
            }

            data.Span.CopyTo(Memory.AsSpan(registerStart));
            return Task.FromResult(new DeviceOperationResult(0, registerStart, []));
        }

        private static byte[] CreateMemory(byte[] initial)
        {
            var memory = new byte[128];
            initial.CopyTo(memory, 0);
            return memory;
        }
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        public ActiveVehicleSnapshot Current { get; private set; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => CancellationToken.None;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed
        {
            add { }
            remove { }
        }
    }
}
