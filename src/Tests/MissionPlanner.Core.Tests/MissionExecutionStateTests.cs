using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;
using MissionState = MissionPlanner.MavLink.Generated.MissionState;

namespace MissionPlanner.Core.Tests;

/// <summary>Validates complete mission execution decoding and ID-based snapshot safety.</summary>
public sealed class MissionExecutionStateTests
{
    private static readonly DateTimeOffset ObservedAt = DateTimeOffset.UnixEpoch;
    private static readonly TransportEndPoint EndPoint = new("test");

    [Fact]
    public void MissionCurrentDecoderRetainsFullMavLink2ExecutionState()
    {
        byte[] payload = [7, 0, 10, 0, 3, 2, 0x78, 0x56, 0x34, 0x12];
        var decoder = new MissionCurrentMessageDecoder();

        Assert.True(decoder.TryDecode(Frame(MessageIds.MissionCurrent, payload), out var decoded));
        var message = Assert.IsType<MissionCurrentMessage>(decoded);
        Assert.Equal((ushort)7, message.Sequence);
        Assert.Equal((ushort)10, message.Total);
        Assert.Equal((byte)3, message.MissionState);
        Assert.Equal((byte)2, message.MissionMode);
        Assert.Equal(0x12345678u, message.MissionId);
    }

    [Fact]
    public void MissionCurrentDecoderTreatsMissingExtensionsAsUnsupported()
    {
        var decoder = new MissionCurrentMessageDecoder();
        Assert.True(decoder.TryDecode(Frame(MessageIds.MissionCurrent, [4, 0]), out var decoded));
        var message = Assert.IsType<MissionCurrentMessage>(decoded);
        Assert.Null(message.Total);
        Assert.Null(message.MissionState);
        Assert.Null(message.MissionMode);
        Assert.Null(message.MissionId);
    }

    [Fact]
    public void MissionCountDecoderRetainsNonZeroOpaqueId()
    {
        byte[] payload = [2, 0, 255, 190, 0, 0x44, 0x33, 0x22, 0x11];
        var decoder = new MissionCountMessageDecoder();
        Assert.True(decoder.TryDecode(Frame(MessageIds.MissionCount, payload), out var decoded));
        Assert.Equal(0x11223344u, Assert.IsType<MissionCountMessage>(decoded).OpaqueId);
    }

    [Fact]
    public void SnapshotFreshnessRequiresMatchingNonZeroIdsAndExactVehicle()
    {
        var store = CreateStore();
        var vehicle = new VehicleId(1, 1);
        store.Record(Snapshot(vehicle, 42));

        Assert.Equal(MissionSnapshotFreshness.VerifiedCurrent, store.GetFreshness(State(vehicle, 42, 3)));
        Assert.Equal(MissionSnapshotFreshness.Stale, store.GetFreshness(State(vehicle, 43, 3)));
        Assert.Equal(MissionSnapshotFreshness.Unverified, store.GetFreshness(State(vehicle, null, 3)));
        Assert.Equal(MissionSnapshotFreshness.Missing, store.GetFreshness(State(new VehicleId(2, 1), 42, 3)));
    }

    [Fact]
    public void CurrentItemLookupFailsClosedUnlessSnapshotIsVerifiedAndSequenceExists()
    {
        var store = CreateStore();
        var vehicle = new VehicleId(1, 1);
        store.Record(Snapshot(vehicle, 42));

        Assert.True(store.TryGetCurrentItem(State(vehicle, 42, 3), out var item));
        Assert.Equal((ushort)21, item!.Command);
        Assert.False(store.TryGetCurrentItem(State(vehicle, 42, 2), out _));
        Assert.False(store.TryGetCurrentItem(State(vehicle, 99, 3), out _));
        Assert.False(store.TryGetCurrentItem(State(new VehicleId(2, 1), 42, 3), out _));
    }

    private static OnboardMissionSnapshotStore CreateStore() => new(Substitute.For<IDomainEventHub>());

    private static OnboardMissionSnapshot Snapshot(VehicleId vehicle, uint? missionId) => new(
        vehicle,
        MissionPlanType.FlightMission,
        [new MavLinkMissionItem(3, 0, 21, false, true, 0, 0, 0, 0, 0, 0, 0, MavMissionType.Mission)],
        missionId,
        ObservedAt);

    private static VehicleState State(VehicleId vehicle, uint? missionId, ushort sequence)
    {
        var state = new VehicleState(vehicle, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, ObservedAt, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        return state with
        {
            Navigation = state.Navigation with
            {
                CurrentMissionSequence = sequence,
                MissionId = missionId,
                MissionState = MissionState.Active,
                MissionMode = VehicleMissionMode.Mission
            }
        };
    }

    private static MavLinkFrame Frame(uint messageId, byte[] payload) =>
        new(1, 1, EndPoint, messageId, 0, payload, ReadOnlyMemory<byte>.Empty, ObservedAt);
}
