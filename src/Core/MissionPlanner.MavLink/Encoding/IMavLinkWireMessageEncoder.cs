using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.MavLink.Encoding;

/// <summary>Encodes generated dialect-backed messages as unsigned MAVLink 2 wire packets.</summary>
public interface IMavLinkWireMessageEncoder
{
    /// <summary>Encodes a generated message.</summary>
    /// <param name="message">The generated message and source identity.</param>
    /// <returns>The complete MAVLink 2 packet.</returns>
    byte[] Encode(GeneratedMavLinkMessage message);
}
