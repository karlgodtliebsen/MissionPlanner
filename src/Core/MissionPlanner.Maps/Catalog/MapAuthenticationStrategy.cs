namespace MissionPlanner.Maps.Catalog;

/// <summary>Identifies how a reviewed provider credential is attached to a request.</summary>
public enum MapAuthenticationStrategy
{
    /// <summary>No authentication is added.</summary>
    None,

    /// <summary>An API key is added as a query parameter.</summary>
    QueryApiKey,

    /// <summary>A bearer token is added to the Authorization header.</summary>
    AuthorizationBearer,

    /// <summary>An API key is added to a reviewed request header.</summary>
    HeaderApiKey
}
