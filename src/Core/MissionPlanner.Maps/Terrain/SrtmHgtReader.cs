namespace MissionPlanner.Maps.Terrain;

/// <summary>
/// Reads big-endian signed SRTM HGT elevation grids.
/// </summary>
public static class SrtmHgtReader
{
    private const short VoidValue = -32768;

    /// <summary>
    /// Interpolates elevation from an HGT file whose name identifies its south-west corner.
    /// </summary>
    public static async ValueTask<double?> ReadAsync(string path, double latitude, double longitude, int tileSouth, int tileWest, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.RandomAccess);
        var sampleCount = stream.Length / 2;
        var side = (int)Math.Sqrt(sampleCount);
        if (side < 2 || (long)side * side != sampleCount)
        {
            throw new InvalidDataException("The SRTM HGT file is not a square 16-bit elevation grid.");
        }

        var x = Math.Clamp((longitude - tileWest) * (side - 1), 0, side - 1);
        var y = Math.Clamp((tileSouth + 1 - latitude) * (side - 1), 0, side - 1);
        var column = Math.Min((int)Math.Floor(x), side - 2);
        var row = Math.Min((int)Math.Floor(y), side - 2);
        var xFraction = x - column;
        var yFraction = y - row;
        var northWest = await ReadSampleAsync(stream, side, row, column, cancellationToken).ConfigureAwait(false);
        var northEast = await ReadSampleAsync(stream, side, row, column + 1, cancellationToken).ConfigureAwait(false);
        var southWest = await ReadSampleAsync(stream, side, row + 1, column, cancellationToken).ConfigureAwait(false);
        var southEast = await ReadSampleAsync(stream, side, row + 1, column + 1, cancellationToken).ConfigureAwait(false);
        if (northWest == VoidValue || northEast == VoidValue || southWest == VoidValue || southEast == VoidValue)
        {
            return null;
        }

        var north = northWest + ((northEast - northWest) * xFraction);
        var south = southWest + ((southEast - southWest) * xFraction);
        return north + ((south - north) * yFraction);
    }

    private static async ValueTask<short> ReadSampleAsync(FileStream stream, int side, int row, int column, CancellationToken cancellationToken)
    {
        var buffer = new byte[2];
        stream.Position = (((long)row * side) + column) * 2;
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return (short)((buffer[0] << 8) | buffer[1]);
    }
}
