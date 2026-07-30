using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
