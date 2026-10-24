using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

public sealed partial class CompassConfigurationService
{
    /// <inheritdoc />
    public async Task<CompassParameterApplyResult> SetPrioritiesAsync(VehicleId vehicleId,
        IReadOnlyDictionary<string, float> expected, IReadOnlyList<uint> orderedDeviceIds, bool confirmed,
        CancellationToken cancellationToken = default)
    {
        var state = RequireActiveVehicle(vehicleId);
        var boundary = activeVehicle.ConnectionCancellationToken;
        var now = clock.GetUtcNow();
        if (!confirmed || state.IsArmed || state.Autopilot != 3 || state.LastHeartbeatAt > now || now - state.LastHeartbeatAt > TimeSpan.FromSeconds(3) || orderedDeviceIds.Count > 3 ||
            orderedDeviceIds.Contains(0u) || orderedDeviceIds.Distinct().Count() != orderedDeviceIds.Count)
        {
            return new(false, "Confirm a unique priority order for the freshly connected disarmed ArduPilot vehicle.");
        }
        var names = Enumerable.Range(1, 3).Select(index => $"COMPASS_PRIO{index}_ID").ToArray();
        var live = parameterRegistry.GetAllParameters(vehicleId);
        var trackedNames = live.Keys.Where(name => name.StartsWith("COMPASS_DEV_ID", StringComparison.Ordinal) || names.Contains(name)).ToArray();
        if (names.Any(name => !live.ContainsKey(name)) || trackedNames.Length != expected.Count ||
            trackedNames.Any(name => !expected.TryGetValue(name, out var old) || old != live[name].Value))
        {
            return new(false, "Compass identities or priorities changed. Refresh and review the order again.");
        }
        var known = live.Where(pair => pair.Key.StartsWith("COMPASS_DEV_ID", StringComparison.Ordinal) || names.Contains(pair.Key))
            .Select(pair => ToDeviceId(pair.Value.Value)).ToHashSet();
        if (orderedDeviceIds.Any(id => !known.Contains(id) || (uint)(float)id != id))
        {
            return new(false, "Priority IDs must come from the current inventory and be representable by the parameter protocol.");
        }
        if (!operationGate.TryAcquire(vehicleId, "Compass priority configuration", out var lease))
        {
            return new(false, "Another vehicle operation is in progress.");
        }
        using (lease)
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, boundary))
        {
            var written = 0;
            var reviewed = expected.ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var (name, index) in names.Select((name, index) => (name, index)))
            {
                linked.Token.ThrowIfCancellationRequested();
                var current = RequireActiveVehicle(vehicleId);
                now = clock.GetUtcNow();
                var actual = parameterRegistry.GetAllParameters(vehicleId);
                var actualNames = actual.Keys.Where(key => key.StartsWith("COMPASS_DEV_ID", StringComparison.Ordinal) || names.Contains(key)).ToArray();
                if (current.IsArmed || current.LastHeartbeatAt > now || now - current.LastHeartbeatAt > TimeSpan.FromSeconds(3) ||
                    activeVehicle.ConnectionCancellationToken != boundary || actualNames.Length != reviewed.Count ||
                    reviewed.Any(pair => !actual.TryGetValue(pair.Key, out var value) || value.Value != pair.Value))
                {
                    return new(false, $"Stopped after {written} writes because vehicle state or compass inventory changed. Refresh priorities before proceeding.");
                }
                var next = index < orderedDeviceIds.Count ? (float)orderedDeviceIds[index] : 0;
                if (live[name].Value == next)
                {
                    continue;
                }
                if (!await WriteAndConfirmAsync(vehicleId, name, next, live[name].Type, linked.Token).ConfigureAwait(false))
                {
                    return new(false, $"{name} was not confirmed after {written} confirmed writes. A partial order may remain; refresh and review before rebooting.");
                }
                reviewed[name] = next;
                written++;
            }
            return new(true, "Compass priority values confirmed. Reboot, refresh the inventory and verify calibration before flight.");
        }
    }
}
