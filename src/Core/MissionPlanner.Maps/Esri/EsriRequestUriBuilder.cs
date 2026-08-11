using MissionPlanner.Maps.Credentials;

namespace MissionPlanner.Maps.Esri;

/// <summary>Builds optional authenticated Esri requests without persisting or logging tokens.</summary>
public static class EsriRequestUriBuilder
{
    /// <summary>Appends a token to an Esri URI at the request boundary.</summary>
    public static Uri WithToken(Uri endpoint, string token)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var builder = new UriBuilder(endpoint);
        builder.Query = string.IsNullOrEmpty(builder.Query) ? $"token={Uri.EscapeDataString(token)}" : $"{builder.Query.TrimStart('?')}&token={Uri.EscapeDataString(token)}";
        return builder.Uri;
    }

    /// <summary>Returns a redacted diagnostic form of an authenticated URI.</summary>
    public static string ToDiagnosticString(Uri endpoint) => MapDiagnosticRedactor.Redact(endpoint.ToString());
}
