using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Core.Firmware;

/// <summary>Loads firmware releases from configured entries or an HTTPS JSON manifest.</summary>
public sealed class JsonFirmwareManifestProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<FirmwareManifestOptions> options,
    ILogger<JsonFirmwareManifestProvider> logger) : IFirmwareManifestProvider
{
    private static readonly JsonSerializerOptions jsonOptions = CreateJsonOptions();

    /// <inheritdoc />
    public async Task<IReadOnlyList<FirmwareManifestEntry>> GetReleasesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ManifestUrl))
        {
            return options.Value.Releases.ToArray();
        }

        if (!Uri.TryCreate(options.Value.ManifestUrl, UriKind.Absolute, out var manifestUri) || manifestUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Firmware manifest URL must be an absolute HTTPS URI.");
        }

        logger.LogInformation("Downloading firmware manifest from {ManifestHost}.", manifestUri.Host);
        var client = httpClientFactory.CreateClient("Firmware");
        return await client.GetFromJsonAsync<List<FirmwareManifestEntry>>(manifestUri, jsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var result = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        result.Converters.Add(new JsonStringEnumConverter());
        return result;
    }
}
