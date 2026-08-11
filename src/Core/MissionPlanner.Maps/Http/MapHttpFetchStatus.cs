namespace MissionPlanner.Maps.Http;

/// <summary>Identifies a normal map HTTP fetch outcome.</summary>
public enum MapHttpFetchStatus
{
    /// <summary>Content was returned.</summary>
    Success,

    /// <summary>Policy denied the request.</summary>
    PolicyDenied,

    /// <summary>A required credential is missing.</summary>
    CredentialMissing,

    /// <summary>The server rejected authentication or authorization.</summary>
    Unauthorized,

    /// <summary>The server rate limited the request.</summary>
    RateLimited,

    /// <summary>The resource was not found.</summary>
    NotFound,

    /// <summary>A network or HTTP error occurred.</summary>
    NetworkFailure,

    /// <summary>The operation was cancelled.</summary>
    Cancelled
}
