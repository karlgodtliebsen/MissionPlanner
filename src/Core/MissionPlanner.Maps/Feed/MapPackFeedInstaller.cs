using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Maps.Policy;

namespace MissionPlanner.Maps.Feed;

/// <summary>Downloads and atomically installs compatible artifacts from an approved feed.</summary>
public sealed class MapPackFeedInstaller
{
    private readonly IMapHttpClientFactory httpClientFactory;
    private readonly IOfflineMapPackInstaller installer;
    private readonly IOfflineMapPackRepository repository;
    private readonly MapCatalog catalog;
    private readonly IMapPolicyEvaluator policyEvaluator;
    private readonly IActiveMapSourceStore? activeSource;

    /// <summary>Initializes a feed installer with the built-in reviewed catalog.</summary>
    public MapPackFeedInstaller(IMapHttpClientFactory httpClientFactory, IOfflineMapPackInstaller installer, IOfflineMapPackRepository repository)
        : this(httpClientFactory, installer, repository, BuiltInMapCatalog.Load(), new MapPolicyEvaluator(), null)
    {
    }

    /// <summary>Initializes a feed installer with explicit policy and active-source dependencies.</summary>
    public MapPackFeedInstaller(IMapHttpClientFactory httpClientFactory, IOfflineMapPackInstaller installer, IOfflineMapPackRepository repository, MapCatalog catalog, IMapPolicyEvaluator policyEvaluator, IActiveMapSourceStore? activeSource)
    {
        this.httpClientFactory = httpClientFactory;
        this.installer = installer;
        this.repository = repository;
        this.catalog = catalog;
        this.policyEvaluator = policyEvaluator;
        this.activeSource = activeSource;
    }

    /// <summary>Downloads and installs an upgrade, retaining the working version on failure.</summary>
    public async ValueTask<InstalledOfflineMapPack> InstallAsync(
        MapPackFeedEntry entry,
        Version missionPlannerVersion,
        Version rendererVersion,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        EnsureCompatible(entry, missionPlannerVersion, rendererVersion);
        ValidatePolicy(entry);
        var installed = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        var existing = installed.Where(value => value.Manifest.Id == entry.Manifest.Id).ToArray();
        if (existing.Any(value => CompareVersion(value.Manifest.Version, entry.Manifest.Version) >= 0))
        {
            throw new InvalidOperationException("Map pack downgrade or duplicate installation is not allowed.");
        }

        using var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(entry.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } length && length != entry.Manifest.SizeBytes)
        {
            throw new InvalidDataException("Map pack response size does not match its signed manifest.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var reviewedSource = catalog.Sources.Single(item => item.Id == entry.SourceId);
        var policy = catalog.Policies.Single(item => item.Id == reviewedSource.PolicyId);
        var manifest = entry.Manifest with
        {
            SourceId = entry.SourceId,
            ProductId = entry.ProductId,
            PolicyId = policy.Id,
            PolicyReviewedOn = policy.ReviewedOn,
            InstallOrigin = OfflineMapPackInstallOrigin.ApprovedFeed,
            Provenance = SafeProvenance(entry.DownloadUri),
            RetrievedAt = DateTimeOffset.UtcNow,
            AttributionIds = reviewedSource.AttributionIds,
            NoticeReferences = entry.NoticeUris.Select(uri => uri.ToString()).ToArray()
        };
        var result = await installer.InstallAsync(manifest, new ProgressReadStream(source, entry.Manifest.SizeBytes, progress), cancellationToken).ConfigureAwait(false);
        if (activeSource is not null && existing.Any(prior => activeSource.SelectedSourceId == $"pack:{prior.Manifest.Id}:{prior.Manifest.Version}"))
        {
            await activeSource.SetSelectedSourceIdAsync($"pack:{result.Manifest.Id}:{result.Manifest.Version}", cancellationToken).ConfigureAwait(false);
        }

        foreach (var prior in existing)
        {
            await repository.RemoveAsync(prior.Manifest.Id, prior.Manifest.Version, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private void ValidatePolicy(MapPackFeedEntry entry)
    {
        var source = catalog.Sources.SingleOrDefault(item => item.Id == entry.SourceId)
                     ?? throw new InvalidDataException($"Feed source '{entry.SourceId}' is not reviewed.");
        if (!StringComparer.Ordinal.Equals(source.ProductId, entry.ProductId))
        {
            throw new InvalidDataException("Feed source and product do not match the reviewed catalog.");
        }

        if (source.ArchiveFormat != MapArchiveFormat.MbTiles || source.ContentFormat == MapTileContentFormat.VectorMvt)
        {
            throw new NotSupportedException("Only reviewed raster MBTiles feed artifacts are supported under ADR-0006.");
        }

        var policy = catalog.Policies.Single(item => item.Id == source.PolicyId);
        if (!policyEvaluator.Evaluate(source, policy, MapOperation.OfflineAreaDownload).IsAllowed)
        {
            throw new InvalidDataException("Reviewed source policy does not permit durable offline installation.");
        }

        if (entry.NoticeUris.Length == 0 || string.IsNullOrWhiteSpace(entry.Manifest.Attribution) || string.IsNullOrWhiteSpace(entry.Manifest.LicenseNotice))
        {
            throw new InvalidDataException("Feed attribution and license notices are required.");
        }
    }

    private static string SafeProvenance(Uri uri) => $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

    private static void EnsureCompatible(MapPackFeedEntry entry, Version app, Version renderer)
    {
        if (!Version.TryParse(entry.MinimumMissionPlannerVersion, out var minimumApp) || !Version.TryParse(entry.MinimumRendererVersion, out var minimumRenderer))
        {
            throw new InvalidDataException("Pack compatibility versions are malformed.");
        }

        if (app < minimumApp || renderer < minimumRenderer)
        {
            throw new NotSupportedException("Map pack requires a newer Mission Planner or renderer version.");
        }
    }

    private static int CompareVersion(string left, string right) =>
        Version.TryParse(left, out var leftVersion) && Version.TryParse(right, out var rightVersion)
            ? leftVersion.CompareTo(rightVersion)
            : StringComparer.Ordinal.Compare(left, right);

    private sealed class ProgressReadStream(Stream inner, long length, IProgress<double>? progress) : Stream
    {
        private long read;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => read; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            read += count;
            progress?.Report(length == 0 ? 1 : (double)read / length);
            return count;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { }
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
