using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Core.Simulation;

/// <summary>Downloads, verifies, and atomically extracts MissionPlanner-owned SITL packages.</summary>
public sealed class SitlPackageManager : ISitlPackageManager
{
    private const string MarkerFileName = "installation.json";
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ISitlCachePathProvider cachePathProvider;
    private readonly SitlManifestOptions options;
    private readonly ILogger<SitlPackageManager> logger;

    /// <summary>Initializes the verified SITL package manager.</summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="cachePathProvider">Platform cache root provider.</param>
    /// <param name="options">Manifest and extraction limits.</param>
    /// <param name="logger">Logger.</param>
    public SitlPackageManager(
        IHttpClientFactory httpClientFactory,
        ISitlCachePathProvider cachePathProvider,
        IOptions<SitlManifestOptions> options,
        ILogger<SitlPackageManager> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.cachePathProvider = cachePathProvider;
        this.options = options.Value;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<SitlInstallation> PrepareAsync(
        SitlManifestEntry release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        ValidateRelease(release);
        var cacheKey = CreateCacheKey(release);
        var root = GetValidatedRoot();
        var installDirectory = ChildPath(root, "installations", cacheKey);
        var existing = await ReadInstallationAsync(installDirectory, cancellationToken).ConfigureAwait(false);
        if (existing is { State: SitlInstallationState.Available } &&
            string.Equals(existing.CacheKey, cacheKey, StringComparison.Ordinal))
        {
            progress?.Report(1);
            return existing;
        }

        Directory.CreateDirectory(ChildPath(root, "downloads"));
        Directory.CreateDirectory(ChildPath(root, "staging"));
        Directory.CreateDirectory(ChildPath(root, "installations"));
        var extension = release.ArchiveFormat == SitlArchiveFormat.Zip ? ".zip" : ".tar.gz";
        var archivePath = ChildPath(root, "downloads", cacheKey + extension);
        await EnsureVerifiedArchiveAsync(release, archivePath, progress, cancellationToken).ConfigureAwait(false);

        var stagingDirectory = ChildPath(root, "staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            await ExtractSafelyAsync(release, archivePath, stagingDirectory, cancellationToken).ConfigureAwait(false);
            var executablePath = SafeArchiveDestination(stagingDirectory, release.ExecutableRelativePath);
            if (!File.Exists(executablePath))
            {
                throw new InvalidDataException(
                    $"SITL archive does not contain the manifest executable '{release.ExecutableRelativePath}'.");
            }

            EnsureExecutablePermission(executablePath);
            var relativeExecutable = Path.GetRelativePath(stagingDirectory, executablePath);
            var marker = new CachedInstallationMarker(cacheKey, release, relativeExecutable);
            await File.WriteAllTextAsync(
                Path.Combine(stagingDirectory, MarkerFileName),
                JsonSerializer.Serialize(marker, jsonOptions),
                cancellationToken).ConfigureAwait(false);
            if (Directory.Exists(installDirectory))
            {
                DeleteOwnedDirectory(root, installDirectory);
            }

            Directory.Move(stagingDirectory, installDirectory);
            progress?.Report(1);
            logger.LogInformation(
                "Installed verified SITL {Version} for {Family} in cache entry {CacheKey}.",
                release.Version,
                release.Family,
                cacheKey);
            return (await ReadInstallationAsync(installDirectory, cancellationToken).ConfigureAwait(false))!;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                DeleteOwnedDirectory(root, stagingDirectory);
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SitlInstallation>> DiscoverCachedAsync(
        CancellationToken cancellationToken = default)
    {
        var root = GetValidatedRoot();
        var installationsRoot = ChildPath(root, "installations");
        if (!Directory.Exists(installationsRoot))
        {
            return [];
        }

        var result = new List<SitlInstallation>();
        foreach (var directory in Directory.EnumerateDirectories(installationsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var installation = await ReadInstallationAsync(directory, cancellationToken).ConfigureAwait(false);
            if (installation is not null)
            {
                result.Add(installation);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public Task RemoveAsync(SitlInstallation installation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        cancellationToken.ThrowIfCancellationRequested();
        if (installation.Source != SitlInstallationSource.VerifiedCache || string.IsNullOrWhiteSpace(installation.CacheKey))
        {
            throw new InvalidOperationException("External SITL installations are not owned by MissionPlanner and cannot be removed.");
        }

        var root = GetValidatedRoot();
        DeleteOwnedDirectory(root, ChildPath(root, "installations", installation.CacheKey));
        var downloads = ChildPath(root, "downloads");
        if (Directory.Exists(downloads))
        {
            foreach (var archive in Directory.EnumerateFiles(downloads, installation.CacheKey + ".*"))
            {
                DeleteOwnedFile(root, archive);
            }
        }

        logger.LogInformation("Removed MissionPlanner-owned SITL cache entry {CacheKey}.", installation.CacheKey);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PruneAsync(
        IReadOnlySet<string> pinnedInstallationIds,
        int keepLatestPerFamily,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pinnedInstallationIds);
        if (keepLatestPerFamily < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keepLatestPerFamily));
        }

        var cached = await DiscoverCachedAsync(cancellationToken).ConfigureAwait(false);
        var retained = cached.GroupBy(item => item.Family)
            .SelectMany(group => group.OrderByDescending(item => item.PublishedAt)
                .ThenByDescending(item => item.Version, StringComparer.OrdinalIgnoreCase)
                .Take(keepLatestPerFamily))
            .Select(item => item.InstallationId)
            .ToHashSet(StringComparer.Ordinal);
        retained.UnionWith(pinnedInstallationIds);
        var removed = new List<string>();
        foreach (var installation in cached.Where(item => !retained.Contains(item.InstallationId)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RemoveAsync(installation, cancellationToken).ConfigureAwait(false);
            removed.Add(installation.InstallationId);
        }

        return removed;
    }

    private async Task EnsureVerifiedArchiveAsync(
        SitlManifestEntry release,
        string archivePath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (File.Exists(archivePath))
        {
            var cachedHash = await ComputeHashAsync(archivePath, cancellationToken).ConfigureAwait(false);
            if (HashMatches(cachedHash, release.Sha256))
            {
                progress?.Report(1);
                return;
            }

            DeleteOwnedFile(GetValidatedRoot(), archivePath);
        }

        var temporaryPath = archivePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            var client = httpClientFactory.CreateClient("SITL");
            using var response = await client.GetAsync(
                release.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength > options.MaximumArchiveBytes)
            {
                throw new InvalidDataException("SITL archive exceeds the configured download size limit.");
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                received += read;
                if (received > options.MaximumArchiveBytes)
                {
                    throw new InvalidDataException("SITL archive exceeds the configured download size limit.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                if (contentLength is > 0)
                {
                    progress?.Report(Math.Clamp((double)received / contentLength.Value, 0, 1));
                }
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            await output.DisposeAsync().ConfigureAwait(false);
            var computed = await ComputeHashAsync(temporaryPath, cancellationToken).ConfigureAwait(false);
            if (!HashMatches(computed, release.Sha256))
            {
                throw new InvalidDataException(
                    $"SITL archive checksum mismatch. Expected {release.Sha256}, computed {computed}.");
            }

            File.Move(temporaryPath, archivePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task ExtractSafelyAsync(
        SitlManifestEntry release,
        string archivePath,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        await using var archive = new FileStream(
            archivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (release.ArchiveFormat == SitlArchiveFormat.Zip)
        {
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
            long extracted = 0;
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RejectZipLink(entry);
                var destination = SafeArchiveDestination(stagingDirectory, entry.FullName);
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                extracted = checked(extracted + entry.Length);
                EnsureExtractedLimit(extracted);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await using var input = entry.Open();
                await using var output = new FileStream(
                    destination,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        await using var gzip = new GZipStream(archive, CompressionMode.Decompress, leaveOpen: true);
        using var tar = new TarReader(gzip, leaveOpen: true);
        long total = 0;
        TarEntry? tarEntry;
        while ((tarEntry = tar.GetNextEntry()) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = SafeArchiveDestination(stagingDirectory, tarEntry.Name);
            if (tarEntry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            if (tarEntry.EntryType is not TarEntryType.RegularFile and not TarEntryType.V7RegularFile)
            {
                throw new InvalidDataException($"SITL archive entry '{tarEntry.Name}' is not a regular file or directory.");
            }

            total = checked(total + tarEntry.Length);
            EnsureExtractedLimit(total);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous);
            if (tarEntry.DataStream is not null)
            {
                await tarEntry.DataStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<SitlInstallation?> ReadInstallationAsync(
        string installDirectory,
        CancellationToken cancellationToken)
    {
        var markerPath = Path.Combine(installDirectory, MarkerFileName);
        if (!File.Exists(markerPath))
        {
            return null;
        }

        try
        {
            var marker = JsonSerializer.Deserialize<CachedInstallationMarker>(
                await File.ReadAllTextAsync(markerPath, cancellationToken).ConfigureAwait(false),
                jsonOptions);
            if (marker is null ||
                Path.GetFileName(marker.CacheKey) != marker.CacheKey ||
                !string.Equals(
                    Path.GetFileName(Path.TrimEndingDirectorySeparator(installDirectory)),
                    marker.CacheKey,
                    StringComparison.Ordinal))
            {
                return null;
            }

            var executable = SafeArchiveDestination(installDirectory, marker.ExecutableRelativePath);
            var available = File.Exists(executable);
            return new SitlInstallation(
                $"cached-{marker.CacheKey}",
                marker.Release.Family,
                marker.Release.Platform,
                marker.Release.Architecture,
                marker.Release.Version,
                executable,
                SitlInstallationSource.VerifiedCache,
                available ? SitlInstallationState.Available : SitlInstallationState.Corrupt,
                marker.CacheKey,
                available ? "Verified cached SITL installation is available." : "Cached SITL executable is missing.",
                marker.Release.PublishedAt);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException)
        {
            logger.LogWarning(exception, "Ignoring invalid SITL cache marker at {MarkerPath}.", markerPath);
            return null;
        }
    }

    private string GetValidatedRoot()
    {
        var root = Path.GetFullPath(cachePathProvider.CacheRoot);
        if (string.IsNullOrWhiteSpace(cachePathProvider.CacheRoot) || Path.GetPathRoot(root) == root)
        {
            throw new InvalidOperationException("SITL cache root must be a dedicated non-root directory.");
        }

        return root;
    }

    private static string ChildPath(string root, params string[] segments)
    {
        var result = Path.GetFullPath(Path.Combine([root, .. segments]));
        EnsureUnderRoot(root, result);
        return result;
    }

    private static string SafeArchiveDestination(string root, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || Path.IsPathRooted(entryName))
        {
            throw new InvalidDataException("SITL archive contains an empty or rooted path.");
        }

        var normalized = entryName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(Path.Combine(root, normalized));
        EnsureUnderRoot(root, destination);
        return destination;
    }

    private static void EnsureUnderRoot(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(fullRoot, comparison))
        {
            throw new InvalidDataException("SITL cache or archive path escapes its owned root.");
        }
    }

    private static void DeleteOwnedDirectory(string root, string path)
    {
        EnsureUnderRoot(root, path);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private static void DeleteOwnedFile(string root, string path)
    {
        EnsureUnderRoot(root, path);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void RejectZipLink(ZipArchiveEntry entry)
    {
        const int unixFileTypeMask = 0xF000;
        const int unixSymbolicLink = 0xA000;
        if (((entry.ExternalAttributes >> 16) & unixFileTypeMask) == unixSymbolicLink)
        {
            throw new InvalidDataException($"SITL archive symbolic link '{entry.FullName}' is not allowed.");
        }
    }

    private void EnsureExtractedLimit(long extractedBytes)
    {
        if (extractedBytes > options.MaximumExtractedBytes)
        {
            throw new InvalidDataException("SITL archive exceeds the configured extracted-size limit.");
        }
    }

    private static void EnsureExecutablePermission(string executablePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = File.GetUnixFileMode(executablePath);
        mode |= UnixFileMode.UserRead | UnixFileMode.UserExecute;
        File.SetUnixFileMode(executablePath, mode);
    }

    private static void ValidateRelease(SitlManifestEntry release)
    {
        if (!release.DownloadUri.IsAbsoluteUri || release.DownloadUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("SITL packages must use an absolute HTTPS URI.");
        }

        if (release.Sha256.Length != 64 || !release.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("SITL manifest SHA-256 must contain exactly 64 hexadecimal characters.");
        }

        if (string.IsNullOrWhiteSpace(release.Version) || string.IsNullOrWhiteSpace(release.ExecutableRelativePath))
        {
            throw new InvalidDataException("SITL manifest version and executable path are required.");
        }
    }

    private static string CreateCacheKey(SitlManifestEntry release)
    {
        var identity = $"{release.Family}|{release.Platform}|{release.Architecture}|{release.Version}|{release.DownloadUri}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(digest).ToLower(CultureInfo.InvariantCulture);
    }

    private static bool HashMatches(string computed, string expected) =>
        string.Equals(computed, expected, StringComparison.OrdinalIgnoreCase);

    private sealed record CachedInstallationMarker(
        string CacheKey,
        SitlManifestEntry Release,
        string ExecutableRelativePath);
}
