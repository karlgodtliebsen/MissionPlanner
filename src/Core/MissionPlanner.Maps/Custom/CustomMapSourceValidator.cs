using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Custom;

/// <summary>Validates custom and self-hosted map source configuration.</summary>
public static class CustomMapSourceValidator
{
    /// <summary>Validates a custom source.</summary>
    public static IReadOnlyList<CustomMapSourceValidationIssue> Validate(CustomMapSourceSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var issues = new List<CustomMapSourceValidationIssue>();
        if (string.IsNullOrWhiteSpace(source.Id) || source.Id.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
        {
            issues.Add(new CustomMapSourceValidationIssue("id", "ID must contain only letters, digits, hyphens, or underscores.", false));
        }

        if (string.IsNullOrWhiteSpace(source.DisplayName))
        {
            issues.Add(new CustomMapSourceValidationIssue("displayName", "Display name is required.", false));
        }

        if (source.MinimumZoom < 0 || source.MaximumZoom < source.MinimumZoom)
        {
            issues.Add(new CustomMapSourceValidationIssue("zoom", "Zoom range is invalid.", false));
        }

        if (string.IsNullOrWhiteSpace(source.Attribution))
        {
            issues.Add(new CustomMapSourceValidationIssue("attribution", "Attribution is required for user-controlled sources.", false));
        }

        if (!Uri.TryCreate(ReplaceTemplateTokens(source.Endpoint), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            issues.Add(new CustomMapSourceValidationIssue("endpoint", "An absolute HTTP or HTTPS endpoint is required.", false));
        }
        else if (uri.Scheme == "http")
        {
            issues.Add(new CustomMapSourceValidationIssue("endpoint", "Plain HTTP is not encrypted; HTTPS is strongly preferred.", true));
        }

        if (ContainsSecretQuery(source.Endpoint))
        {
            issues.Add(new CustomMapSourceValidationIssue("endpoint", "Credentials must not be embedded in the endpoint; use secure credential storage.", false));
        }

        if (source.AccessKind is MapAccessKind.HttpXyz or MapAccessKind.HttpTms)
        {
            foreach (var token in new[] { "{z}", "{x}", "{y}" })
            {
                if (!source.Endpoint.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new CustomMapSourceValidationIssue("endpoint", $"Raster tile templates require {token}.", false));
                }
            }
        }
        else if (source.AccessKind == MapAccessKind.Wms && string.IsNullOrWhiteSpace(source.LayerName))
        {
            issues.Add(new CustomMapSourceValidationIssue("layerName", "WMS requires a layer name.", false));
        }
        else if (source.AccessKind == MapAccessKind.Wmts)
        {
            if (string.IsNullOrWhiteSpace(source.LayerName))
            {
                issues.Add(new CustomMapSourceValidationIssue("layerName", "WMTS requires a layer name.", false));
            }

            if (string.IsNullOrWhiteSpace(source.TileMatrixSet))
            {
                issues.Add(new CustomMapSourceValidationIssue("tileMatrixSet", "WMTS requires a tile matrix set.", false));
            }
        }
        else if (source.AccessKind is not (MapAccessKind.HttpXyz or MapAccessKind.HttpTms or MapAccessKind.Wms or MapAccessKind.Wmts))
        {
            issues.Add(new CustomMapSourceValidationIssue("accessKind", "Custom network configuration supports XYZ, TMS, WMS, or WMTS. Use the offline pack API for MBTiles.", false));
        }

        return issues;
    }

    /// <summary>Throws when a custom source contains an error.</summary>
    public static void ValidateAndThrow(CustomMapSourceSettings source)
    {
        var errors = Validate(source).Where(issue => !issue.IsWarning).ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors.Select(issue => $"{issue.Path}: {issue.Message}")));
        }
    }

    private static string ReplaceTemplateTokens(string value) => value.Replace("{z}", "0", StringComparison.OrdinalIgnoreCase).Replace("{x}", "0", StringComparison.OrdinalIgnoreCase).Replace("{y}", "0", StringComparison.OrdinalIgnoreCase);
    private static bool ContainsSecretQuery(string value) => System.Text.RegularExpressions.Regex.IsMatch(value, "(?i)[?&](api_?key|access_?token|token|password|key)=[^&{]+", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
