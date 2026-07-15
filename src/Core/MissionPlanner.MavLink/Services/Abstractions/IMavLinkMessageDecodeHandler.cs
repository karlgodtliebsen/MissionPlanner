using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.MavLink.Services.Abstractions;

/// <summary>
/// Defines a contract for decoding MAVLink messages from frames.
/// </summary>
public interface IMavLinkMessageDecodeHandler
{
    /// <summary>
    /// Tries to decode a MAVLink message from the given frame.
    /// </summary>
    /// <param name="frame">The MAVLink frame to decode.</param>
    /// <param name="message">The decoded MAVLink message, if successful.</param>
    /// <returns><c>true</c> if the message was successfully decoded; otherwise, <c>false</c>.</returns>
    bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message);
}
