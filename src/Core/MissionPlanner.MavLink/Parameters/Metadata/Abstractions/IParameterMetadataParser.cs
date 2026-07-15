namespace MissionPlanner.MavLink.Parameters.Metadata.Abstractions;

/// <summary>
/// Parser for ArduPilot parameter metadata XML files.
/// </summary>
public interface IParameterMetadataParser
{
    /// <summary>
    /// Parses parameter metadata from an XML stream.
    /// </summary>
    /// <param name="xmlStream">The XML stream to parse.</param>
    /// <param name="vehicleType">The vehicle type to parse metadata for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of parameter name to metadata.</returns>
    Task<Dictionary<string, ParameterMetadata>> ParseAsync(
        Stream xmlStream,
        VehicleType vehicleType,
        CancellationToken cancellationToken = default);
}
