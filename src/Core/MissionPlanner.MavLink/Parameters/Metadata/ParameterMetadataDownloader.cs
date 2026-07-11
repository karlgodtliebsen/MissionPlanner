using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.MavLink.Parameters.Metadata;

/// <summary>
/// Downloads parameter metadata from ArduPilot's autotest server.
/// </summary>
public sealed class ParameterMetadataDownloader(
    IHttpClientFactory httpClientFactory,
    ILogger<ParameterMetadataDownloader> logger)
    : IParameterMetadataDownloader
{
    private const string BaseUrl = "https://autotest.ardupilot.org/Parameters";

    /// <inheritdoc/>
    public async Task<Stream> DownloadAsync(VehicleType vehicleType, CancellationToken cancellationToken = default)
    {
        var vehicleName = GetVehicleName(vehicleType);
        var url = $"{BaseUrl}/{vehicleName}/apm.pdef.xml.gz";

        logger.LogInformation("Downloading parameter metadata from {Url}", url);

        try
        {
            using var httpClient = httpClientFactory.CreateClient("ParameterMetadata");
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Download the gzipped content
            await using var gzipStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // Decompress to memory stream
            var decompressedStream = new MemoryStream();
            await using (var decompressionStream = new GZipStream(gzipStream, CompressionMode.Decompress))
            {
                await decompressionStream.CopyToAsync(decompressedStream, cancellationToken);
            }

            decompressedStream.Position = 0;

            logger.LogInformation(
                "Successfully downloaded and decompressed {Bytes} bytes of metadata for {VehicleType}",
                decompressedStream.Length,
                vehicleType);

            return decompressedStream;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Failed to download parameter metadata for {VehicleType} from {Url}",
                vehicleType,
                url);
            throw new InvalidOperationException(
                $"Failed to download parameter metadata for {vehicleType}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected error downloading parameter metadata for {VehicleType}",
                vehicleType);
            throw;
        }
    }

    private static string GetVehicleName(VehicleType vehicleType)
    {
        return vehicleType switch
        {
            VehicleType.ArduCopter => "ArduCopter",
            VehicleType.ArduPlane => "ArduPlane",
            VehicleType.Rover => "Rover",
            VehicleType.ArduSub => "ArduSub",
            VehicleType.AntennaTracker => "AntennaTracker",
            VehicleType.AP_Periph => "AP_Periph",
            VehicleType.SITL => "SITL",
            VehicleType.Blimp => "Blimp",
            VehicleType.Heli => "Heli",
            var _ => throw new ArgumentException($"Unknown vehicle type: {vehicleType}", nameof(vehicleType))
        };
    }
}
