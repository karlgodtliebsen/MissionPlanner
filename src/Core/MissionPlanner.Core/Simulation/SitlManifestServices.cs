using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Loads configured or official HTTPS SITL release manifests.</summary>
public sealed class JsonSitlManifestProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<SitlManifestOptions> options,
    ILogger<JsonSitlManifestProvider> logger) : ISitlManifestProvider
{
    private static readonly JsonSerializerOptions jsonOptions = CreateJsonOptions();

    /// <inheritdoc />
    public async Task<IReadOnlyList<SitlManifestEntry>> GetReleasesAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ManifestUrl))
        {
            return options.Value.Releases.ToArray();
        }

        if (!Uri.TryCreate(options.Value.ManifestUrl, UriKind.Absolute, out var manifestUri) ||
            manifestUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("SITL manifest URL must be an absolute HTTPS URI.");
        }

        logger.LogInformation("Downloading the configured SITL manifest from {ManifestHost}.", manifestUri.Host);
        var client = httpClientFactory.CreateClient("SITL");
        return await client.GetFromJsonAsync<List<SitlManifestEntry>>(
            manifestUri,
            jsonOptions,
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var result = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        result.Converters.Add(new JsonStringEnumConverter());
        return result;
    }
}

/// <summary>Selects exact family, channel, platform, and architecture SITL artifacts.</summary>
public sealed class SitlReleaseSelector : ISitlReleaseSelector
{
    /// <inheritdoc />
    public IReadOnlyList<SitlManifestEntry> Select(
        IEnumerable<SitlManifestEntry> releases,
        FirmwareFamily family,
        FirmwareReleaseChannel channel,
        SitlPlatformCapability capability)
    {
        ArgumentNullException.ThrowIfNull(releases);
        ArgumentNullException.ThrowIfNull(capability);
        if (!capability.CanExecuteNative)
        {
            return [];
        }

        return releases.Where(release =>
                release.Family == family &&
                release.Channel == channel &&
                release.Platform == capability.Platform &&
                release.Architecture == capability.Architecture)
            .OrderByDescending(release => release.PublishedAt)
            .ThenByDescending(release => release.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

/// <summary>Discovers configured/cached installations and resolves reproducible profile pins.</summary>
public sealed class SitlInstallationService(
    ISitlManifestProvider manifestProvider,
    ISitlReleaseSelector releaseSelector,
    ISitlPlatformService platformService,
    ISitlPackageManager packageManager,
    IOptions<SitlManifestOptions> options,
    ILogger<SitlInstallationService> logger) : ISitlInstallationService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SitlInstallation>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<SitlInstallation>();
        foreach (var configured in options.Value.ExternalInstallations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(await DiscoverExternalAsync(configured, cancellationToken).ConfigureAwait(false));
        }

        result.AddRange(await packageManager.DiscoverCachedAsync(cancellationToken).ConfigureAwait(false));
        return result.OrderBy(item => item.Family).ThenByDescending(item => item.PublishedAt)
            .ThenByDescending(item => item.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SitlManifestEntry>> GetReleasesAsync(
        FirmwareFamily family,
        FirmwareReleaseChannel channel,
        CancellationToken cancellationToken = default)
    {
        var releases = await manifestProvider.GetReleasesAsync(cancellationToken).ConfigureAwait(false);
        return releaseSelector.Select(releases, family, channel, platformService.Current);
    }

    /// <inheritdoc />
    public Task<SitlInstallation> InstallAsync(
        SitlManifestEntry release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        var capability = platformService.Current;
        if (!capability.CanExecuteNative || release.Platform != capability.Platform ||
            release.Architecture != capability.Architecture)
        {
            throw new PlatformNotSupportedException(
                $"SITL release {release.Version} targets {release.Platform}/{release.Architecture}; " +
                $"this runtime is {capability.Platform}/{capability.Architecture}.");
        }

        return packageManager.PrepareAsync(release, progress, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(SitlInstallation installation, CancellationToken cancellationToken = default) =>
        packageManager.RemoveAsync(installation, cancellationToken);

    /// <inheritdoc />
    public SitlInstallationResolution Resolve(
        SimulatorProfile profile,
        IReadOnlyList<SitlInstallation> installations)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(installations);
        SitlInstallation? selected = null;
        if (!string.IsNullOrWhiteSpace(profile.Binary.InstallationId))
        {
            selected = installations.FirstOrDefault(item =>
                item.InstallationId.Equals(profile.Binary.InstallationId, StringComparison.Ordinal));
            if (selected is null)
            {
                return new SitlInstallationResolution(
                    SitlInstallationState.Missing,
                    null,
                    $"Pinned SITL installation '{profile.Binary.InstallationId}' is not installed.");
            }
        }
        else
        {
            selected = installations.FirstOrDefault(item =>
                item.Family == profile.FirmwareFamily &&
                item.Version.Equals(profile.Binary.Version, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(profile.Binary.ExecutablePath) ||
                 PathsEqual(item.ExecutablePath, profile.Binary.ExecutablePath)));
        }
        if (selected is null)
        {
            return new SitlInstallationResolution(
                SitlInstallationState.Missing,
                null,
                $"Pinned SITL version '{profile.Binary.Version}' is not installed for {profile.FirmwareFamily}.");
        }

        return new SitlInstallationResolution(selected.State, selected, selected.Message);
    }

    private async Task<SitlInstallation> DiscoverExternalAsync(
        ExternalSitlInstallationOptions configured,
        CancellationToken cancellationToken)
    {
        var path = configured.ExecutablePath;
        var fullPath = string.IsNullOrWhiteSpace(path) ? path : Path.GetFullPath(path);
        var id = CreateExternalId(configured.Family, fullPath);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return new SitlInstallation(
                id,
                configured.Family,
                platformService.Current.Platform,
                platformService.Current.Architecture,
                configured.Version ?? "unknown",
                fullPath,
                SitlInstallationSource.External,
                SitlInstallationState.Missing,
                null,
                "Configured external SITL executable was not found.");
        }

        if (!platformService.Current.CanExecuteNative)
        {
            return new SitlInstallation(
                id,
                configured.Family,
                platformService.Current.Platform,
                platformService.Current.Architecture,
                configured.Version ?? "unknown",
                fullPath,
                SitlInstallationSource.External,
                SitlInstallationState.Incompatible,
                null,
                platformService.Current.Message);
        }

        var queried = await platformService.TryQueryVersionAsync(fullPath, cancellationToken).ConfigureAwait(false);
        var version = string.IsNullOrWhiteSpace(queried) ? configured.Version ?? "unknown" : queried.Trim();
        logger.LogInformation("Discovered configured external SITL installation {InstallationId}.", id);
        return new SitlInstallation(
            id,
            configured.Family,
            platformService.Current.Platform,
            platformService.Current.Architecture,
            version,
            fullPath,
            SitlInstallationSource.External,
            SitlInstallationState.Available,
            null,
            "Configured external SITL installation is available.");
    }

    private static string CreateExternalId(FirmwareFamily family, string path)
    {
        var data = Encoding.UTF8.GetBytes($"{family}|{path}");
        return $"external-{Convert.ToHexString(SHA256.HashData(data))[..16].ToLowerInvariant()}";
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
