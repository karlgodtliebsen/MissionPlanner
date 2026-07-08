namespace MissionPlanner.MavLink.Parameters.Metadata;

/// <summary>
/// Downloads parameter metadata from ArduPilot's servers.
/// </summary>
public interface IParameterMetadataDownloader
{
    /// <summary>
    /// Downloads and decompresses parameter metadata for a vehicle type.
    /// </summary>
    /// <param name="vehicleType">The vehicle type to download metadata for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the decompressed XML data.</returns>
    Task<Stream> DownloadAsync(VehicleType vehicleType, CancellationToken cancellationToken = default);
}
