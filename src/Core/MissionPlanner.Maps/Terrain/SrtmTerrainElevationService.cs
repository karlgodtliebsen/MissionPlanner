using System.Collections.Concurrent;
using System.IO.Compression;
using MissionPlanner.Maps.Http;

namespace MissionPlanner.Maps.Terrain;

/// <summary>
/// Downloads, caches, and queries SRTM HGT tiles from the AWS Open Data terrain dataset.
/// </summary>
public sealed class SrtmTerrainElevationService(IMapHttpClientFactory httpClientFactory, string cacheDirectory) : ITerrainElevationService
{
    private const long MaximumUncompressedTileBytes = 30L * 1_048_576;
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> tileRequests = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<double?> GetElevationMetersAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return null;
        }

        var south = Math.Min((int)Math.Floor(latitude), 89);
        var west = Math.Min((int)Math.Floor(longitude), 179);
        var name = TileName(south, west);
        var lazy = tileRequests.GetOrAdd(name, _ => new Lazy<Task<string?>>(() => EnsureTileAsync(name), LazyThreadSafetyMode.ExecutionAndPublication));
        string? path;
        try
        {
            path = await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            tileRequests.TryRemove(name, out var _);
            throw;
        }

        return path is null ? null : await SrtmHgtReader.ReadAsync(path, latitude, longitude, south, west, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> EnsureTileAsync(string name)
    {
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, name + ".hgt");
        var missingPath = path + ".missing";
        if (File.Exists(path))
        {
            return path;
        }

        if (File.Exists(missingPath))
        {
            return null;
        }

        var uri = new Uri($"https://s3.amazonaws.com/elevation-tiles-prod/skadi/{name[..3]}/{name}.hgt.gz");
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
            await using var compressed = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
            await using var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[81920];
            long length = 0;
            int read;
            while ((read = await gzip.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            {
                length += read;
                if (length > MaximumUncompressedTileBytes)
                {
                    throw new InvalidDataException("The SRTM tile exceeds the supported size.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
            }

            await output.FlushAsync().ConfigureAwait(false);
            var samples = length / 2;
            var side = (int)Math.Sqrt(samples);
            if (length == 0 || length % 2 != 0 || (long)side * side != samples)
            {
                throw new InvalidDataException("The downloaded SRTM tile has an invalid HGT grid size.");
            }

            File.Move(staging, path);
            return path;
        }
        finally
        {
            if (File.Exists(staging))
            {
                File.Delete(staging);
            }
        }
    }

    private static string TileName(int latitude, int longitude)
    {
        return $"{(latitude >= 0 ? 'N' : 'S')}{Math.Abs(latitude):00}{(longitude >= 0 ? 'E' : 'W')}{Math.Abs(longitude):000}";
    }
}
