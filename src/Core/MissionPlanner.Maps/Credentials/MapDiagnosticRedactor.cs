namespace MissionPlanner.Maps.Credentials;

/// <summary>Redacts secrets and sensitive query parameters from map diagnostics.</summary>
public static class MapDiagnosticRedactor
{
    private static readonly string[] SensitiveNames = ["access_token", "api_key", "apikey", "key", "token", "password"];

    /// <summary>Redacts a known secret and sensitive URI query values.</summary>
    public static string Redact(string value, string? secret = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        var redacted = string.IsNullOrEmpty(secret) ? value : value.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        foreach (var name in SensitiveNames)
        {
            redacted = System.Text.RegularExpressions.Regex.Replace(redacted, $"(?i)([?&]{System.Text.RegularExpressions.Regex.Escape(name)}=)[^&#]*", "$1[REDACTED]");
        }

        return redacted;
    }
}
