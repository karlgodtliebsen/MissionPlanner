using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Decodes ArduPilot's packed parameter virtual file format.
/// </summary>
public interface IArduPilotPackedParameterDecoder
{
    /// <summary>
    /// Decodes a complete <c>@PARAM/param.pck</c> payload.
    /// </summary>
    /// <param name="data">The packed file contents.</param>
    /// <returns>The decoded parameters.</returns>
    IReadOnlyList<VehicleParameter> Decode(ReadOnlySpan<byte> data);
}
