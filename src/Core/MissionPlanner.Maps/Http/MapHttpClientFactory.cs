namespace MissionPlanner.Maps.Http;

/// <summary>Default map HTTP client factory.</summary>
public sealed class MapHttpClientFactory(HttpMessageHandler handler, MapHttpOptions options) : IMapHttpClientFactory
{
    /// <inheritdoc />
    public HttpClient CreateClient()
    {
        var client = new HttpClient(handler, false) { Timeout = options.Timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        return client;
    }
}
