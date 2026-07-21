namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Represents connection-scoped firmware, protocol, and hardware identity.
/// </summary>
/// <param name="Family">The broad firmware family.</param>
/// <param name="MavType">The detailed MAV_TYPE value.</param>
/// <param name="Autopilot">The MAV_AUTOPILOT value.</param>
/// <param name="FlightVersion">The semantic flight software version, when reported.</param>
/// <param name="FlightGitHash">The lowercase custom flight Git identifier, when reported.</param>
/// <param name="Capabilities">The raw protocol capability bits.</param>
/// <param name="BoardVersion">The board hardware version.</param>
/// <param name="VendorId">The hardware vendor ID.</param>
/// <param name="ProductId">The hardware product ID.</param>
/// <param name="HardwareUid">The legacy hardware UID, when reported.</param>
/// <param name="HardwareUid2">The extended hardware UID, when reported.</param>
public sealed record VehicleFirmwareIdentity(
    FirmwareFamily Family,
    byte MavType,
    byte Autopilot,
    FirmwareSemanticVersion? FlightVersion,
    string? FlightGitHash,
    ulong Capabilities,
    uint BoardVersion,
    ushort VendorId,
    ushort ProductId,
    ulong? HardwareUid,
    string? HardwareUid2)
{
    /// <summary>Determines whether the vehicle reports a named protocol capability.</summary>
    /// <param name="capability">The capability to test.</param>
    /// <returns><see langword="true"/> when all requested capability bits are present.</returns>
    public bool Supports(MavProtocolCapability capability) =>
        ((MavProtocolCapability)Capabilities & capability) == capability;
}
