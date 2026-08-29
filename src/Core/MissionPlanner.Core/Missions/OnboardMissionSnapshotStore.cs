using System.Collections.Concurrent;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions;

/// <inheritdoc />
public sealed class OnboardMissionSnapshotStore : IOnboardMissionSnapshotStore, IDisposable
{
    private readonly ConcurrentDictionary<(VehicleId Vehicle, MissionPlanType Type), OnboardMissionSnapshot> snapshots = new();
    private readonly IDisposable registeredSubscription;
    private readonly IDisposable disconnectedSubscription;

    public OnboardMissionSnapshotStore(IDomainEventHub eventHub)
    {
        registeredSubscription = eventHub.SubscribeDomainEventAsync<VehicleRegistered>((evt, _) =>
        {
            InvalidateAll(evt.VehicleId);
            return Task.CompletedTask;
        });
        disconnectedSubscription = eventHub.SubscribeDomainEventAsync<VehicleDisconnected>((evt, _) =>
        {
            InvalidateAll(evt.VehicleId);
            return Task.CompletedTask;
        });
    }

    public event EventHandler? Changed;

    public OnboardMissionSnapshot? Get(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission) =>
        snapshots.TryGetValue((vehicleId, missionType), out var snapshot) ? snapshot : null;

    public void Record(OnboardMissionSnapshot snapshot)
    {
        var copy = snapshot with
        {
            Items = snapshot.Items.OrderBy(item => item.Sequence).ToArray(),
            MissionId = snapshot.MissionId is > 0 ? snapshot.MissionId : null
        };
        snapshots[(copy.VehicleId, copy.MissionType)] = copy;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Invalidate(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission)
    {
        if (snapshots.TryRemove((vehicleId, missionType), out _))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public MissionSnapshotFreshness GetFreshness(VehicleState vehicleState, MissionPlanType missionType = MissionPlanType.FlightMission)
    {
        var snapshot = Get(vehicleState.VehicleId, missionType);
        if (snapshot is null)
        {
            return MissionSnapshotFreshness.Missing;
        }

        var snapshotId = snapshot.MissionId;
        var streamedId = vehicleState.Navigation.MissionId;
        if (snapshotId is null || streamedId is null)
        {
            return MissionSnapshotFreshness.Unverified;
        }

        return snapshotId == streamedId ? MissionSnapshotFreshness.VerifiedCurrent : MissionSnapshotFreshness.Stale;
    }

    public bool TryGetCurrentItem(VehicleState vehicleState, out MavLinkMissionItem? item)
    {
        item = null;
        if (GetFreshness(vehicleState) != MissionSnapshotFreshness.VerifiedCurrent ||
            vehicleState.Navigation.CurrentMissionSequence is not { } sequence ||
            Get(vehicleState.VehicleId) is not { } snapshot)
        {
            return false;
        }

        item = snapshot.Items.SingleOrDefault(candidate => candidate.Sequence == sequence);
        return item is not null;
    }

    private void InvalidateAll(VehicleId vehicleId)
    {
        var changed = false;
        foreach (var key in snapshots.Keys.Where(key => key.Vehicle == vehicleId))
        {
            changed |= snapshots.TryRemove(key, out _);
        }
        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        registeredSubscription.Dispose();
        disconnectedSubscription.Dispose();
    }
}
