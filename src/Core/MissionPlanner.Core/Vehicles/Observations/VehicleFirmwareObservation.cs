namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Carries firmware and hardware identity decoded at the MAVLink boundary.
/// </summary>
/// <param name="Capabilities">The MAVLink protocol capability bits.</param>
/// <param name="FlightSoftwareVersion">The packed flight software version.</param>
/// <param name="BoardVersion">The board hardware version.</param>
/// <param name="FlightCustomVersion">The custom flight version bytes.</param>
/// <param name="VendorId">The hardware vendor ID.</param>
/// <param name="ProductId">The hardware product ID.</param>
/// <param name="Uid">The legacy hardware UID.</param>
/// <param name="Uid2">The extended hardware UID bytes.</param>
/// <param name="ObservedAt">The observation timestamp.</param>
public sealed record VehicleFirmwareObservation(
    ulong Capabilities,
    uint FlightSoftwareVersion,
    uint BoardVersion,
    byte[] FlightCustomVersion,
    ushort VendorId,
    ushort ProductId,
    ulong Uid,
    byte[] Uid2,
    DateTimeOffset ObservedAt);
