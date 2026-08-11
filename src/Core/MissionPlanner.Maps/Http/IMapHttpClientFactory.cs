namespace MissionPlanner.Maps.Http;

/// <summary>Creates centrally configured map HTTP clients.</summary>
public interface IMapHttpClientFactory
{
    /// <summary>Creates a client with a bounded timeout and honest User-Agent.</summary>
    HttpClient CreateClient();
}
