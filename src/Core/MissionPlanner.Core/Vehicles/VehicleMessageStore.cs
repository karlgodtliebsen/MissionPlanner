using Microsoft.Extensions.Options;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Provides thread-safe bounded per-vehicle status-text histories.
/// </summary>
public sealed class VehicleMessageStore : IVehicleMessageStore
{
    private readonly object sync = new();
    private readonly Dictionary<VehicleId, List<VehicleStatusText>> messages = [];
    private readonly int capacity;
    private long nextIdentity;

    /// <summary>Initializes a new bounded vehicle message store.</summary>
    /// <param name="options">The configured history options.</param>
    public VehicleMessageStore(IOptions<VehicleMessageStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        capacity = Math.Max(1, options.Value.Capacity);
    }

    /// <inheritdoc />
    public event EventHandler<VehicleStatusTextAddedEventArgs>? MessageAdded;

    /// <inheritdoc />
    public IReadOnlyList<VehicleStatusText> GetMessages(VehicleId vehicleId)
    {
        lock (sync)
        {
            return messages.TryGetValue(vehicleId, out var history) ? history.ToArray() : [];
        }
    }

    /// <inheritdoc />
    public VehicleStatusText Add(VehicleStatusText message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var stored = message with { Identity = Interlocked.Increment(ref nextIdentity) };
        lock (sync)
        {
            if (!messages.TryGetValue(stored.VehicleId, out var history))
            {
                history = [];
                messages.Add(stored.VehicleId, history);
            }

            history.Add(stored);
            if (history.Count > capacity)
            {
                history.RemoveRange(0, history.Count - capacity);
            }
        }

        MessageAdded?.Invoke(this, new VehicleStatusTextAddedEventArgs(stored));
        return stored;
    }

    /// <inheritdoc />
    public void Clear(VehicleId vehicleId, Func<VehicleStatusText, bool>? predicate = null)
    {
        lock (sync)
        {
            if (!messages.TryGetValue(vehicleId, out var history))
            {
                return;
            }

            if (predicate is null)
            {
                history.Clear();
            }
            else
            {
                history.RemoveAll(message => predicate(message));
            }
        }
    }
}
