namespace MissionPlanner.Maps.Hosted;

/// <summary>Identifies a hosted map request failure.</summary>
public enum HostedMapFailureKind
{
    /// <summary>No required credential is configured.</summary>
    MissingCredential,

    /// <summary>The provider rejected authentication or authorization.</summary>
    Unauthorized,

    /// <summary>The account is rate limited or over quota.</summary>
    RateLimited,

    /// <summary>The provider could not be reached.</summary>
    Network,

    /// <summary>An unexpected provider response occurred.</summary>
    Provider
}
