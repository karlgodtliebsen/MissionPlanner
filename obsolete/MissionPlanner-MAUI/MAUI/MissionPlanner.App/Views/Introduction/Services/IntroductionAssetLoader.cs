using System.Collections.Concurrent;
using System.Diagnostics;

namespace MissionPlanner.App.Views.Introduction.Services;

/// <summary>
/// Reads Introduction files that are packaged as MauiAsset items.
/// The project file gives them logical names rooted at "Introduction/".
/// </summary>
public static class IntroductionAssetLoader
{
    private const string Root = "Introduction/";

    private static readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> ByteCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reads an Introduction asset as a string asynchronously.
    /// </summary>
    /// <param name="relativePath">The relative path to the Introduction asset.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the string content of the asset.</returns>
    public static async Task<string> ReadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        Debug.Print("Reading text from " + relativePath);

        var bytes = await ReadBytesAsync(relativePath, cancellationToken).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Loads an Introduction asset as an ImageSource asynchronously.
    /// </summary>
    /// <param name="relativePath">The relative path to the Introduction asset.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous load operation. The task result contains the ImageSource.</returns>
    public static async Task<ImageSource> LoadImageSourceAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        Debug.Print("Reading image  from " + relativePath);
        var bytes = await ReadBytesAsync(relativePath, cancellationToken).ConfigureAwait(false);

        // A new stream is required each time MAUI asks the ImageSource to open it.
        return ImageSource.FromStream(() => new MemoryStream(bytes, false));
    }

    /// <summary>
    /// Reads the bytes of an Introduction asset asynchronously.
    /// </summary>
    /// <param name="relativePath">The relative path to the Introduction asset.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation. The task result contains the bytes of the asset.</returns>
    /// <exception cref="ArgumentException">Thrown if the relativePath is null or whitespace.</exception>
    public static Task<byte[]> ReadBytesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        Debug.Print("Reading Bytes from " + relativePath);

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("An Introduction asset path is required.", nameof(relativePath));
        }

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var lazy = ByteCache.GetOrAdd(normalized, static path => new Lazy<Task<byte[]>>(() => LoadBytesCoreAsync(path), LazyThreadSafetyMode.ExecutionAndPublication));

        return AwaitWithCancellationAsync(lazy.Value, cancellationToken);
    }

    private static async Task<byte[]> LoadBytesCoreAsync(string relativePath)
    {
        var logicalName = Root + relativePath;
        await using var stream = await FileSystem.OpenAppPackageFileAsync(logicalName).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static async Task<T> AwaitWithCancellationAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        return !cancellationToken.CanBeCanceled
            ? await task.ConfigureAwait(false)
            : await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
