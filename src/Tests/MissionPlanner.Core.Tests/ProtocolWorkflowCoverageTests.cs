using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Validates dedicated protocol ownership and workflow resilience.</summary>
public sealed class ProtocolWorkflowCoverageTests
{
    private static readonly TransportEndPoint EndPoint = new("test");
    private static readonly DateTimeOffset ObservedAt = DateTimeOffset.UnixEpoch;

    /// <summary>Verifies command sender and inbound ACK handler share one correlation tracker.</summary>
    [Fact]
    public void CommandAckTrackerHasSingletonLifetime()
    {
        var services = new ServiceCollection();
        services.AddDomainServices(new ConfigurationBuilder().Build());
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(ICommandAckTracker));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>Verifies ACK correlation rejects wrong vehicles and ignores duplicate/out-of-order responses.</summary>
    [Fact]
    public async Task CommandAckTrackerCorrelatesVehicleAndCommand()
    {
        var tracker = new CommandAckTracker();
        var expectedVehicle = new VehicleId(1, 1);
        var wait = tracker.WaitForAckAsync(expectedVehicle, 400, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        tracker.Handle(new CommandAckMessage(2, 1, EndPoint, 400, 0, ObservedAt));
        tracker.Handle(new CommandAckMessage(1, 1, EndPoint, 401, 0, ObservedAt));
        Assert.False(wait.IsCompleted);
        var expected = new CommandAckMessage(1, 1, EndPoint, 400, 0, ObservedAt);
        tracker.Handle(expected);
        tracker.Handle(expected);
        Assert.Same(expected, await wait);
    }

    /// <summary>Verifies command waits support timeout and caller cancellation.</summary>
    [Fact]
    public async Task CommandAckTrackerSupportsTimeoutAndCancellation()
    {
        var tracker = new CommandAckTracker();
        await Assert.ThrowsAsync<TimeoutException>(() => tracker.WaitForAckAsync(new VehicleId(1, 1), 1, TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        var wait = tracker.WaitForAckAsync(new VehicleId(1, 1), 2, TimeSpan.FromSeconds(1), cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    /// <summary>Verifies mission download buffers early/out-of-order items and rejects the wrong system.</summary>
    [Fact]
    public async Task MissionDownloadHandlesOutOfOrderAndWrongVehicleResponses()
    {
        var (session, registry) = CreateSession();
        var connection = Substitute.For<IMavLinkConnection>();
        var encoder = Substitute.For<IMavLinkMissionEncoder>();
        encoder.EncodeMissionRequestList(1, 1, Arg.Any<MavMissionType>()).Returns([1]);
        encoder.EncodeMissionRequestInt(1, 1, Arg.Any<ushort>(), Arg.Any<MavMissionType>()).Returns([2]);
        encoder.EncodeMissionAck(1, 1, 0, Arg.Any<MavMissionType>()).Returns([3]);
        var eventHub = Substitute.For<IEventHub>();
        Func<MavLinkMessage, CancellationToken, Task>? callback = null;
        eventHub.SubscribeAsync(MavLinkEventTopics.ReceivedMessage, Arg.Any<Func<MavLinkMessage, CancellationToken, Task>>()).Returns(call => { callback = call.Arg<Func<MavLinkMessage, CancellationToken, Task>>(); return Substitute.For<IDisposable>(); });
        var sends = 0;
        connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), session.EndPoint, Arg.Any<CancellationToken>()).Returns(call =>
        {
            sends++;
            if (sends == 1)
            {
                _ = callback!(new MissionCountMessage(2, 1, EndPoint, 99, 255, 190, 0, ObservedAt), call.Arg<CancellationToken>());
                _ = callback!(new MissionCountMessage(1, 1, EndPoint, 2, 255, 190, 0, ObservedAt), call.Arg<CancellationToken>());
            }
            else if (sends == 2)
            {
                _ = callback!(CreateMissionItem(1), call.Arg<CancellationToken>());
                _ = callback!(CreateMissionItem(0), call.Arg<CancellationToken>());
            }
            return ValueTask.CompletedTask;
        });
        var service = new MissionTransferService(registry, connection, encoder, Substitute.For<IMissionProtocolMapper>(), eventHub);

        var result = await service.DownloadAsync(session.Id, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Assert.Equal([0, 1], result.Items.Select(item => (int)item.Sequence));
        Assert.Equal(3, sends);
    }

    /// <summary>Verifies packed MAVFTP parameters are preferred and classic streaming remains the fallback.</summary>
    [Fact]
    public async Task PackedParametersArePreferredWithClassicFallback()
    {
        var parameterService = Substitute.For<IVehicleParameterService>();
        var registry = new VehicleParameterRegistry();
        var eventHub = Substitute.For<IDomainEventHub>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ObservedAt);
        var fileSystem = Substitute.For<IVehicleFileSystemService>();
        fileSystem.DownloadFileAsync(Arg.Any<VehicleId>(), "@PARAM/param.pck", Arg.Any<Stream>(), Arg.Any<IProgress<VehicleFileTransferProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.Arg<Stream>()!.WriteByte(1);
                return Task.CompletedTask;
            });
        var decoder = new StubPackedDecoder([new VehicleParameter("TEST", 42, MavParamType.Real32, 0, 1)]);
        var service = new VehicleParameterStreamService(parameterService, registry, eventHub, clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<VehicleParameterStreamService>.Instance, decoder, fileSystem);

        var result = await service.StreamAllParametersWithRetryAsync(new VehicleId(1, 1), maxRetries: 0, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal(42, result.Parameters["TEST"].Value);
        await parameterService.DidNotReceive().RequestParameterListAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>());

        var failingService = new VehicleParameterStreamService(parameterService, registry, eventHub, clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<VehicleParameterStreamService>.Instance, new StubPackedDecoder([], true), fileSystem);
        parameterService.RequestParameterListAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>()).Returns(false);
        var fallback = await failingService.StreamAllParametersWithRetryAsync(new VehicleId(1, 1), maxRetries: 0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(fallback.Success);
        await parameterService.Received(1).RequestParameterListAsync(new VehicleId(1, 1), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies product and peripheral protocol families all have effective typed decoders.</summary>
    [Fact]
    public void WorkflowAndPeripheralFamiliesHaveTypedProtocolAccess()
    {
        string[] names =
        [
            "SET_MODE", "COMMAND_INT", "COMMAND_LONG", "COMMAND_ACK", "SET_POSITION_TARGET_LOCAL_NED", "MANUAL_CONTROL", "RC_CHANNELS_OVERRIDE",
            "MISSION_ITEM", "MISSION_REQUEST", "MISSION_ITEM_INT", "MISSION_REQUEST_INT", "MISSION_WRITE_PARTIAL_LIST", "RALLY_POINT",
            "PARAM_REQUEST_LIST", "PARAM_VALUE", "PARAM_EXT_REQUEST_LIST", "PARAM_EXT_VALUE", "PARAM_EXT_ACK",
            "LOG_REQUEST_LIST", "LOG_ENTRY", "LOG_REQUEST_DATA", "LOG_DATA", "LOG_ERASE", "LOG_REQUEST_END",
            "CAMERA_INFORMATION", "CAMERA_SETTINGS", "CAMERA_CAPTURE_STATUS", "GIMBAL_DEVICE_INFORMATION", "GIMBAL_MANAGER_STATUS", "MOUNT_STATUS",
            "ADSB_VEHICLE", "GENERATOR_STATUS", "EFI_STATUS", "ESC_STATUS", "WINCH_STATUS", "LANDING_TARGET", "OPEN_DRONE_ID_BASIC_ID",
            "CELLULAR_STATUS", "WIFI_CONFIG_AP", "CAN_FRAME", "SERIAL_CONTROL", "TUNNEL", "DEVICE_OP_READ", "DEVICE_OP_WRITE_REPLY"
        ];
        var definitions = new MavLinkMessageDefinitionRegistry();
        Assert.DoesNotContain(definitions.Definitions, item => item.Name == "MISSION_CHANGED");
        var decoders = new MavLinkMessageDecoderCatalog(definitions).Entries.ToDictionary(entry => entry.Definition.MessageId);
        foreach (var name in names)
        {
            var definition = definitions.Definitions.Single(item => item.Name == name);
            var frame = new MavLinkFrame(1, 1, EndPoint, definition.MessageId, 0, new byte[definition.MaximumPayloadLength], ReadOnlyMemory<byte>.Empty, ObservedAt);
            Assert.True(decoders[definition.MessageId].Decoder.TryDecode(frame, out var message), name);
            Assert.IsNotType<RawMavLinkMessage>(message);
        }
    }

    private static MissionItemIntMessage CreateMissionItem(ushort sequence) => new(1, 1, EndPoint, 0, 0, 0, 0, 0, 0, 10, sequence, 16, 255, 190, 6, 0, 1, 0, ObservedAt);

    private static (VehicleSession Session, IVehicleRegistry Registry) CreateSession()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ObservedAt);
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, ObservedAt, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        var session = new VehicleSession(state, EndPoint, clock);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(session.Id).Returns(session);
        return (session, registry);
    }

    private sealed class StubPackedDecoder(IReadOnlyList<VehicleParameter> result, bool shouldThrow = false) : IArduPilotPackedParameterDecoder
    {
        public IReadOnlyList<VehicleParameter> Decode(ReadOnlySpan<byte> data) => shouldThrow ? throw new InvalidDataException("Unsupported packed file.") : result;
    }
}
