using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using MissionPlanner.Maps.Http;

namespace MissionPlanner.Maps.Terrain;

/// <summary>Downloads, caches, and queries SRTM HGT tiles from the AWS Open Data terrain dataset.</summary>
public sealed class SrtmTerrainElevationService(IMapHttpClientFactory httpClientFactory, string cacheDirectory) : ITerrainElevationService
{
    private const long MaximumUncompressedTileBytes = 30L * 1_048_576;
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> tileRequests = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<TerrainElevationResult> GetElevationAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            DebugTerrain("none", TerrainElevationStatus.OutsideCoverage, "Coordinate is outside valid bounds.");
            return new(TerrainElevationStatus.OutsideCoverage, null, null, "Coordinate is outside valid latitude/longitude bounds.");
        }

        var south = Math.Min((int)Math.Floor(latitude), 89);
        var west = Math.Min((int)Math.Floor(longitude), 179);
        var name = TileName(south, west);
        DebugTerrain(name, TerrainElevationStatus.Loading, "Lookup started.");
        var lazy = tileRequests.GetOrAdd(name, _ => new Lazy<Task<string?>>(() => EnsureTileAsync(name), LazyThreadSafetyMode.ExecutionAndPublication));
        string? path;
        try
        {
            path = await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            tileRequests.TryRemove(name, out _);
            return NetworkFailure(name, "Terrain request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            tileRequests.TryRemove(name, out _);
            return NetworkFailure(name, "Terrain source could not be reached.", exception);
        }
        catch (InvalidDataException exception)
        {
            tileRequests.TryRemove(name, out _);
            return InvalidFailure(name, "Downloaded terrain data is invalid.", exception);
        }
        catch (IOException exception)
        {
            tileRequests.TryRemove(name, out _);
            return InvalidFailure(name, "Terrain cache could not be written.", exception);
        }

        if (path is null)
        {
            DebugTerrain(name, TerrainElevationStatus.OutsideCoverage, "No SRTM tile is available.");
            return new(TerrainElevationStatus.OutsideCoverage, null, name, "No SRTM tile is available.");
        }

        try
        {
            var elevation = await SrtmHgtReader.ReadAsync(path, latitude, longitude, south, west, cancellationToken).ConfigureAwait(false);
            if (!elevation.HasValue)
            {
                DebugTerrain(name, TerrainElevationStatus.InvalidData, "SRTM void sample encountered.");
                return new(TerrainElevationStatus.InvalidData, null, name, "Terrain data contains a void sample.");
            }

            DebugTerrain(name, TerrainElevationStatus.Available, $"Elevation={elevation.Value:F1} m MSL.");
            return new(TerrainElevationStatus.Available, elevation, name);
        }
        catch (InvalidDataException exception)
        {
            return InvalidFailure(name, "Cached terrain data is invalid.", exception);
        }
        catch (IOException exception)
        {
            return InvalidFailure(name, "Terrain cache could not be read.", exception);
        }
    }

    private async Task<string?> EnsureTileAsync(string name)
    {
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, name + ".hgt");
        var missingPath = path + ".missing";
        if (File.Exists(path))
        {
            DebugTerrain(name, TerrainElevationStatus.Loading, "Using cached HGT tile.");
            return path;
        }

        if (File.Exists(missingPath))
        {
            DebugTerrain(name, TerrainElevationStatus.OutsideCoverage, "Using cached missing-tile marker.");
            return null;
        }

        var uri = new Uri($"https://s3.amazonaws.com/elevation-tiles-prod/skadi/{name[..3]}/{name}.hgt.gz");
        DebugTerrain(name, TerrainElevationStatus.Loading, $"Downloading {uri}.");
        using var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await File.WriteAllTextAsync(missingPath, "No SRTM tile is available for this coordinate.").ConfigureAwait(false);
            return null;
        }

        response.EnsureSuccessStatusCode();
        var staging = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            long length = 0;
            await using (var compressed = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
            await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await gzip.ReadAsync(buffer).ConfigureAwait(false)) != 0)
                {
                    length += read;
                    if (length > MaximumUncompressedTileBytes)
                        throw new InvalidDataException("The SRTM tile exceeds the supported size.");
                    await output.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                }

                await output.FlushAsync().ConfigureAwait(false);
            }

            var samples = length / 2;
            var side = (int)Math.Sqrt(samples);
            if (length == 0 || length % 2 != 0 || (long)side * side != samples)
                throw new InvalidDataException("The downloaded SRTM tile has an invalid HGT grid size.");
            // All input/decompression/output streams must be closed before the atomic rename on Windows.
            File.Move(staging, path);
            return path;
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }

    private static TerrainElevationResult NetworkFailure(string name, string message, Exception exception)
    {
        DebugTerrain(name, TerrainElevationStatus.NetworkUnavailable, exception.Message);
        return new(TerrainElevationStatus.NetworkUnavailable, null, name, message);
    }

    private static TerrainElevationResult InvalidFailure(string name, string message, Exception exception)
    {
        DebugTerrain(name, TerrainElevationStatus.InvalidData, exception.Message);
        return new(TerrainElevationStatus.InvalidData, null, name, message);
    }

    private static string TileName(int latitude, int longitude) =>
        $"{(latitude >= 0 ? 'N' : 'S')}{Math.Abs(latitude):00}{(longitude >= 0 ? 'E' : 'W')}{Math.Abs(longitude):000}";

    [Conditional("DEBUG")]
    private static void DebugTerrain(string tileId, TerrainElevationStatus status, string message) =>
        Debug.WriteLine($"[Terrain] tile={tileId} status={status} {message}");
}
