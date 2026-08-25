using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.FlightData.Components;

/// <summary>Thread-safe, bounded component and ADS-B traffic registry.</summary>
public sealed class VehicleComponentRegistry : IVehicleComponentRegistry
{
    private static readonly TimeSpan expiry = TimeSpan.FromSeconds(30);
    private readonly object sync = new();
    private readonly Dictionary<VehicleComponentKey, VehicleComponentState> components = [];
    private readonly Dictionary<VehicleComponentKey, TransponderComponentState> transponders = [];
    private readonly Dictionary<(byte SystemId, uint Icao), AdsbTrafficTrack> traffic = [];

    /// <inheritdoc /> public event EventHandler? Changed;
    public event Action? Changed;

    /// <summary>Records component heartbeat discovery.</summary>
    public void Observe(HeartbeatMessage message)
    {
        lock (sync)
        {
            var key = new VehicleComponentKey(message.SystemId, message.ComponentId);
            components[key] = components.TryGetValue(key, out var current)
                ? current with
                {
                    MavType = message.VehicleType,
                    Autopilot = message.Autopilot,
                    LastSeen = message.ReceivedAt,
                    IsOnline = true
                }
                : new VehicleComponentState(key, message.VehicleType, message.Autopilot, message.ReceivedAt, message.ReceivedAt, true);
        }

        Changed?.Invoke();
    }

    /// <summary>Records uAvionix status for its exact source component.</summary>
    public void Observe(UavionixAdsbOutStatusMessage message)
    {
        lock (sync)
        {
            transponders[new VehicleComponentKey(message.SystemId, message.ComponentId)] = new TransponderComponentState(new VehicleComponentKey(message.SystemId, message.ComponentId),
                message.Squawk, message.FlightId.Trim(), message.State, message.Fault, message.Boardtemp, message.ReceivedAt);
        }

        Changed?.Invoke();
    }

    /// <summary>Records and deduplicates one traffic observation.</summary>
    public void Observe(AdsbVehicleMessage message)
    {
        lock (sync)
        {
            traffic[(message.SystemId, message.IcaoAddress)] = new AdsbTrafficTrack(message.IcaoAddress, message.Callsign.Trim(),
                message.Lat / 1e7, message.Lon / 1e7, message.Altitude / 1000d, message.Squawk, message.Flags, message.ReceivedAt);
            if (traffic.Count > 512)
            {
                foreach (var key in traffic.OrderBy(x => x.Value.ObservedAt).Take(traffic.Count - 512).Select(x => x.Key).ToArray())
                {
                    traffic.Remove(key);
                }
            }
        }

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public IReadOnlyList<VehicleComponentState> GetComponents(byte systemId)
    {
        lock (sync)
        {
            return components.Values.Where(x => x.Key.SystemId == systemId).OrderBy(x => x.Key.ComponentId).ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TransponderComponentState> GetTransponders(byte systemId)
    {
        lock (sync)
        {
            return transponders.Values.Where(x => x.Key.SystemId == systemId).OrderBy(x => x.Key.ComponentId).ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AdsbTrafficTrack> GetTraffic(byte systemId, DateTimeOffset now)
    {
        lock (sync)
        {
            foreach (var key in traffic.Where(x => now - x.Value.ObservedAt > expiry).Select(x => x.Key).ToArray())
            {
                traffic.Remove(key);
            }

            return traffic.Where(x => x.Key.SystemId == systemId).Select(x => x.Value).OrderByDescending(x => x.ObservedAt).ToArray();
        }
    }
}
