namespace MissionPlanner.MavLink.Services.Abstractions;

/// <summary>
/// Provides CRC extra bytes for common MAVLink messages.
/// </summary>
public interface IMavLinkCrcExtraProvider
{
    /// <summary>
    /// Tries to get the CRC extra byte for the specified MAVLink message ID.
    /// </summary>
    /// <param name="messageId">The MAVLink message ID.</param>
    /// <param name="crcExtra">The CRC extra byte if found.</param>
    /// <returns>True if the CRC extra byte was found; otherwise, false.</returns>
    bool TryGetCrcExtra(uint messageId, out byte crcExtra);
}