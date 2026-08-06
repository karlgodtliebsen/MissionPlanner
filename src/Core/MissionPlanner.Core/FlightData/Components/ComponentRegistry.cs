using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.FlightData.Components;

/// <summary>Uniquely identifies a peripheral component on a vehicle system.</summary>
public readonly record struct VehicleComponentKey(byte SystemId, byte ComponentId);

/// <summary>Describes the latest discovery evidence for a vehicle component.</summary>
public sealed record VehicleComponentState(VehicleComponentKey Key, byte MavType, byte Autopilot,
    DateTimeOffset FirstSeen, DateTimeOffset LastSeen, bool IsOnline);

/// <summary>Describes a bounded ADS-B traffic track.</summary>
public sealed record AdsbTrafficTrack(uint IcaoAddress, string Callsign, double? Latitude, double? Longitude,
    double? AltitudeMeters, ushort Squawk, ushort ValidityFlags, DateTimeOffset ObservedAt);

/// <summary>Contains observed uAvionix transponder state.</summary>
public sealed record TransponderComponentState(VehicleComponentKey Key, ushort Squawk, string FlightId,
    byte State, byte Fault, byte TemperatureCelsius, DateTimeOffset ObservedAt);

/// <summary>Stores discovered peripheral components and component-scoped workflow state.</summary>
public interface IVehicleComponentRegistry
{
    /// <summary>Occurs when component or traffic state changes.</summary>
    event EventHandler? Changed;
    /// <summary>Returns discovered components for a system.</summary>
    IReadOnlyList<VehicleComponentState> GetComponents(byte systemId);
    /// <summary>Returns transponder states for a system.</summary>
    IReadOnlyList<TransponderComponentState> GetTransponders(byte systemId);
    /// <summary>Returns current bounded traffic tracks for a system.</summary>
    IReadOnlyList<AdsbTrafficTrack> GetTraffic(byte systemId, DateTimeOffset now);
}

/// <summary>Thread-safe, bounded component and ADS-B traffic registry.</summary>
public sealed class VehicleComponentRegistry : IVehicleComponentRegistry
{
    private static readonly TimeSpan expiry = TimeSpan.FromSeconds(30);
    private readonly object sync = new();
    private readonly Dictionary<VehicleComponentKey, VehicleComponentState> components = [];
    private readonly Dictionary<VehicleComponentKey, TransponderComponentState> transponders = [];
    private readonly Dictionary<(byte SystemId, uint Icao), AdsbTrafficTrack> traffic = [];
    /// <inheritdoc /> public event EventHandler? Changed;
    public event EventHandler? Changed;

    /// <summary>Records component heartbeat discovery.</summary>
    public void Observe(HeartbeatMessage message)
    {
        lock (sync)
        {
            var key = new VehicleComponentKey(message.SystemId, message.ComponentId);
            components[key] = components.TryGetValue(key, out var current)
                ? current with { MavType = message.VehicleType, Autopilot = message.Autopilot, LastSeen = message.ReceivedAt, IsOnline = true }
                : new(key, message.VehicleType, message.Autopilot, message.ReceivedAt, message.ReceivedAt, true);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Records uAvionix status for its exact source component.</summary>
    public void Observe(UavionixAdsbOutStatusMessage message)
    {
        lock (sync) transponders[new(message.SystemId, message.ComponentId)] = new(new(message.SystemId, message.ComponentId),
            message.Squawk, message.FlightId.Trim(), message.State, message.Fault, message.Boardtemp, message.ReceivedAt);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Records and deduplicates one traffic observation.</summary>
    public void Observe(AdsbVehicleMessage message)
    {
        lock (sync)
        {
            traffic[(message.SystemId, message.IcaoAddress)] = new(message.IcaoAddress, message.Callsign.Trim(),
                message.Lat / 1e7, message.Lon / 1e7, message.Altitude / 1000d, message.Squawk, message.Flags, message.ReceivedAt);
            if (traffic.Count > 512)
                foreach (var key in traffic.OrderBy(x => x.Value.ObservedAt).Take(traffic.Count - 512).Select(x => x.Key).ToArray()) traffic.Remove(key);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public IReadOnlyList<VehicleComponentState> GetComponents(byte systemId) { lock (sync) return components.Values.Where(x => x.Key.SystemId == systemId).OrderBy(x => x.Key.ComponentId).ToArray(); }
    /// <inheritdoc />
    public IReadOnlyList<TransponderComponentState> GetTransponders(byte systemId) { lock (sync) return transponders.Values.Where(x => x.Key.SystemId == systemId).OrderBy(x => x.Key.ComponentId).ToArray(); }
    /// <inheritdoc />
    public IReadOnlyList<AdsbTrafficTrack> GetTraffic(byte systemId, DateTimeOffset now)
    {
        lock (sync)
        {
            foreach (var key in traffic.Where(x => now - x.Value.ObservedAt > expiry).Select(x => x.Key).ToArray()) traffic.Remove(key);
            return traffic.Where(x => x.Key.SystemId == systemId).Select(x => x.Value).OrderByDescending(x => x.ObservedAt).ToArray();
        }
    }
}

/// <summary>Routes peripheral messages into the component registry without polluting VehicleState.</summary>
public sealed class PeripheralComponentHandler(VehicleComponentRegistry registry) : IVehicleMessageHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<Type> MessageTypes { get; } = [typeof(HeartbeatMessage), typeof(UavionixAdsbOutStatusMessage), typeof(AdsbVehicleMessage)];
    /// <inheritdoc />
    public ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        switch (message) { case HeartbeatMessage heartbeat: registry.Observe(heartbeat); break; case UavionixAdsbOutStatusMessage status: registry.Observe(status); break; case AdsbVehicleMessage track: registry.Observe(track); break; }
        return ValueTask.CompletedTask;
    }
}

/// <summary>Validates human-entered four-digit octal squawk values.</summary>
public static class TransponderValidation
{
    /// <summary>Returns whether text contains exactly four octal digits.</summary>
    public static bool IsSquawk(string? value) => value is { Length: 4 } && value.All(c => c is >= '0' and <= '7');
}
