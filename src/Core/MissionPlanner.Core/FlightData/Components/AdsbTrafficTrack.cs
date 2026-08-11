namespace MissionPlanner.Core.FlightData.Components;

/// <summary>Describes a bounded ADS-B traffic track.</summary>
public sealed record AdsbTrafficTrack(
    uint IcaoAddress,
    string Callsign,
    double? Latitude,
    double? Longitude,
    double? AltitudeMeters,
    ushort Squawk,
    ushort ValidityFlags,
    DateTimeOffset ObservedAt);
