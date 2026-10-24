using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

/// <summary>A persistent hardware-ID reservation for one explicitly named isolated CAN network.</summary>
public sealed record DroneCanAllocation(string Network, string UniqueId, byte NodeId);

/// <summary>Persists allocations before advertising them, so a tool restart does not recycle occupied IDs.</summary>
public interface IDroneCanAllocationStore
{
    /// <summary>Loads reservations for a network.</summary>
    Task<IReadOnlyList<DroneCanAllocation>> LoadAsync(string network, CancellationToken token);
    /// <summary>Saves the complete reservation list for that network.</summary>
    Task SaveAsync(string network, IReadOnlyList<DroneCanAllocation> values, CancellationToken token);
}

/// <summary>Stores isolated-bus reservations with local Planner preferences.</summary>
public sealed class DroneCanAllocationStore(IPlannerSettingsService settings) : IDroneCanAllocationStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DroneCanAllocation>> LoadAsync(string network, CancellationToken token)
    {
        await settings.InitializeAsync(token).ConfigureAwait(false);
        return settings.Current.DroneCanAllocations.Where(value => value.Network == network).ToArray();
    }
    /// <inheritdoc />
    public async Task SaveAsync(string network, IReadOnlyList<DroneCanAllocation> values, CancellationToken token)
    {
        var all = settings.Current.DroneCanAllocations.Where(value => value.Network != network).Concat(values).ToArray();
        Validate(all);
        var result = await settings.SaveAsync(settings.Current with { DroneCanAllocations = all }, token).ConfigureAwait(false);
        if (!result.Success) throw new IOException("Could not persist CAN allocation reservations: " + string.Join(" ", result.Errors.Select(error => error.Message)));
    }
    /// <summary>Validates unique network/UID and network/node reservations with bounded storage.</summary>
    public static void Validate(IReadOnlyList<DroneCanAllocation>? values)
    {
        if (values is null || values.Count > 1000 || values.Any(value => value is null || string.IsNullOrWhiteSpace(value.Network) || value.Network.Length > 64 || value.Network.Any(char.IsControl) ||
                value.NodeId is < 1 or > 125 || value.UniqueId.Length != 32 || !value.UniqueId.All(char.IsAsciiHexDigit)) ||
            values.Select(value => (value.Network, value.UniqueId.ToUpperInvariant())).Distinct().Count() != values.Count || values.Select(value => (value.Network, value.NodeId)).Distinct().Count() != values.Count)
            throw new ArgumentException("DroneCAN reservations require unique hardware and node IDs on a named network (maximum 1,000 entries).");
    }
}

public sealed partial class DroneCanTransport
{
    private volatile bool allocatorEnabled;
    private DateTimeOffset otherAllocatorAt;
    private List<DroneCanAllocation> reservations = [];
    private readonly List<byte> allocationPrefix = [];
    private DateTimeOffset allocationAt;
    private byte preferredAllocation;
    /// <inheritdoc />
    public bool AllocatorEnabled => allocatorEnabled;
    /// <inheritdoc />
    public async Task SetAllocatorAsync(bool enabled, bool isolatedBusConfirmed, CancellationToken token)
    {
        await operations.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Check();
            if (!enabled) { allocatorEnabled = false; return; }
            if (!isolatedBusConfirmed || kind != DroneCanTransportKind.DirectSlcan || string.IsNullOrWhiteSpace(options.AllocationNetwork) || options.AllocationNetwork.Length > 64 || options.AllocationNetwork.Any(char.IsControl))
                throw new InvalidOperationException("Allocation is available only on a confirmed isolated direct SLCAN bus, with a distinct local network name and no other allocator.");
            var values = await allocations.LoadAsync(options.AllocationNetwork, token).ConfigureAwait(false); DroneCanAllocationStore.Validate(values);
            lock (sync)
            {
                if (otherAllocatorAt != default) throw new InvalidOperationException("Another allocator was detected on this connection. Disconnect and isolate the bench bus first.");
                reservations = values.ToList(); allocationPrefix.Clear(); allocatorEnabled = true;
            }
        }
        finally { operations.Release(); }
    }
    private async Task AllocateAsync(DroneCanTransfer transfer, CancellationToken token)
    {
        if (transfer.Source != 0)
        {
            lock (sync) { otherAllocatorAt = transfer.ObservedAt; allocatorEnabled = false; allocationPrefix.Clear(); }
            return;
        }
        byte[] response; List<DroneCanAllocation>? changed = null;
        lock (sync)
        {
            if (!allocatorEnabled || transfer.Payload.Length is < 2 or > 7) return;
            var first = (transfer.Payload[0] & 1) != 0; var preferred = (byte)(transfer.Payload[0] >> 1);
            if (first) { allocationPrefix.Clear(); preferredAllocation = preferred; }
            else if (allocationPrefix.Count == 0 || preferredAllocation != preferred || transfer.ObservedAt - allocationAt > TimeSpan.FromMilliseconds(500)) { allocationPrefix.Clear(); return; }
            allocationAt = transfer.ObservedAt;
            allocationPrefix.AddRange(transfer.Payload[1..]);
            if (allocationPrefix.Count > 16) { allocationPrefix.Clear(); return; }
            byte granted = 0;
            if (allocationPrefix.Count == 16)
            {
                var uid = Convert.ToHexString(allocationPrefix.ToArray());
                if (uid == new string('0', 32)) { allocationPrefix.Clear(); return; }
                var reserved = reservations.FirstOrDefault(value => value.UniqueId.Equals(uid, StringComparison.OrdinalIgnoreCase));
                if (reserved is not null)
                {
                    if (nodes.TryGetValue(reserved.NodeId, out var occupied) && DateTimeOffset.UtcNow - occupied.LastSeen < TimeSpan.FromSeconds(3) && occupied.UniqueId != uid) { allocationPrefix.Clear(); return; }
                    granted = reserved.NodeId;
                }
                else
                {
                    var start = preferred is > 0 and <= 125 ? preferred : 125;
                    var occupied = reservations.Select(value => value.NodeId).Concat(nodes.Keys).Append(options.LocalNodeId).ToHashSet();
                    granted = Enumerable.Range(start, 126 - start).Concat(Enumerable.Range(1, start).Reverse()).Select(value => (byte)value).FirstOrDefault(value => !occupied.Contains(value));
                    if (granted == 0) { allocationPrefix.Clear(); return; }
                    changed = [.. reservations, new(options.AllocationNetwork, uid, granted)];
                }
            }
            response = [(byte)(granted << 1), .. allocationPrefix];
            if (granted != 0) allocationPrefix.Clear();
        }
        if (changed is not null)
        {
            await allocations.SaveAsync(options.AllocationNetwork, changed, token).ConfigureAwait(false);
            lock (sync) reservations = changed;
        }
        if (!allocatorEnabled) return;
        await SendTransferAsync(new(24, 1, options.LocalNodeId, 0, false, false, 0, response, DateTimeOffset.UtcNow), token).ConfigureAwait(false);
    }
}
