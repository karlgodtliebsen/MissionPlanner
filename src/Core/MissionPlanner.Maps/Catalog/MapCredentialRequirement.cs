namespace MissionPlanner.Maps.Catalog;

/// <summary>Identifies the credential required to access a source.</summary>
public enum MapCredentialRequirement
{
    /// <summary>No credential is required.</summary>
    None,

    /// <summary>An API key is required.</summary>
    ApiKey,

    /// <summary>An OAuth bearer token is required.</summary>
    OAuthToken,

    /// <summary>A user name and password are required.</summary>
    UserNamePassword
}
