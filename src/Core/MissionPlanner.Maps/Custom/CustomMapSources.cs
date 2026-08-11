using System.Text.Json;
using System.Xml.Linq;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;

namespace MissionPlanner.Maps.Custom;

/// <summary>Defines a user-controlled map source without storing secrets.</summary>
/// <param name="Id">Stable user source identifier.</param>
/// <param name="DisplayName">User-facing name.</param>
/// <param name="AccessKind">XYZ, TMS, WMS, WMTS, or local archive access.</param>
/// <param name="Endpoint">Endpoint or template.</param>
/// <param name="MinimumZoom">Minimum zoom.</param>
/// <param name="MaximumZoom">Maximum zoom.</param>
/// <param name="LayerName">WMS or WMTS layer name.</param>
/// <param name="StyleName">Optional service style.</param>
/// <param name="TileMatrixSet">WMTS tile matrix set.</param>
/// <param name="CredentialRequirement">Credential type stored separately.</param>
/// <param name="Attribution">Required user-provided attribution.</param>
/// <param name="EnableHttpCache">Whether the technical HTTP cache is enabled.</param>
public sealed record CustomMapSourceSettings(
    string Id,
    string DisplayName,
    MapAccessKind AccessKind,
    string Endpoint,
    int MinimumZoom,
    int MaximumZoom,
    string? LayerName,
    string? StyleName,
    string? TileMatrixSet,
    MapCredentialRequirement CredentialRequirement,
    string Attribution,
    bool EnableHttpCache);

/// <summary>Represents a custom source validation message.</summary>
/// <param name="Path">Configuration path.</param>
/// <param name="Message">Message text.</param>
/// <param name="IsWarning">Whether the message is advisory rather than invalidating.</param>
public sealed record CustomMapSourceValidationIssue(string Path, string Message, bool IsWarning);

/// <summary>Validates custom and self-hosted map source configuration.</summary>
public static class CustomMapSourceValidator
{
    /// <summary>Validates a custom source.</summary>
    public static IReadOnlyList<CustomMapSourceValidationIssue> Validate(CustomMapSourceSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var issues = new List<CustomMapSourceValidationIssue>();
        if (string.IsNullOrWhiteSpace(source.Id) || source.Id.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
            issues.Add(new("id", "ID must contain only letters, digits, hyphens, or underscores.", false));
        if (string.IsNullOrWhiteSpace(source.DisplayName)) issues.Add(new("displayName", "Display name is required.", false));
        if (source.MinimumZoom < 0 || source.MaximumZoom < source.MinimumZoom) issues.Add(new("zoom", "Zoom range is invalid.", false));
        if (string.IsNullOrWhiteSpace(source.Attribution)) issues.Add(new("attribution", "Attribution is required for user-controlled sources.", false));
        if (!Uri.TryCreate(ReplaceTemplateTokens(source.Endpoint), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            issues.Add(new("endpoint", "An absolute HTTP or HTTPS endpoint is required.", false));
        else if (uri.Scheme == "http")
            issues.Add(new("endpoint", "Plain HTTP is not encrypted; HTTPS is strongly preferred.", true));
        if (ContainsSecretQuery(source.Endpoint)) issues.Add(new("endpoint", "Credentials must not be embedded in the endpoint; use secure credential storage.", false));

        if (source.AccessKind is MapAccessKind.HttpXyz or MapAccessKind.HttpTms)
        {
            foreach (var token in new[] { "{z}", "{x}", "{y}" })
                if (!source.Endpoint.Contains(token, StringComparison.OrdinalIgnoreCase)) issues.Add(new("endpoint", $"Raster tile templates require {token}.", false));
        }
        else if (source.AccessKind == MapAccessKind.Wms && string.IsNullOrWhiteSpace(source.LayerName))
            issues.Add(new("layerName", "WMS requires a layer name.", false));
        else if (source.AccessKind == MapAccessKind.Wmts)
        {
            if (string.IsNullOrWhiteSpace(source.LayerName)) issues.Add(new("layerName", "WMTS requires a layer name.", false));
            if (string.IsNullOrWhiteSpace(source.TileMatrixSet)) issues.Add(new("tileMatrixSet", "WMTS requires a tile matrix set.", false));
        }
        else if (source.AccessKind is not (MapAccessKind.HttpXyz or MapAccessKind.HttpTms or MapAccessKind.Wms or MapAccessKind.Wmts))
            issues.Add(new("accessKind", "Custom network configuration supports XYZ, TMS, WMS, or WMTS. Use the offline pack API for MBTiles.", false));
        return issues;
    }

    /// <summary>Throws when a custom source contains an error.</summary>
    public static void ValidateAndThrow(CustomMapSourceSettings source)
    {
        var errors = Validate(source).Where(issue => !issue.IsWarning).ToArray();
        if (errors.Length != 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors.Select(issue => $"{issue.Path}: {issue.Message}")));
    }

    private static string ReplaceTemplateTokens(string value) => value.Replace("{z}", "0", StringComparison.OrdinalIgnoreCase).Replace("{x}", "0", StringComparison.OrdinalIgnoreCase).Replace("{y}", "0", StringComparison.OrdinalIgnoreCase);
    private static bool ContainsSecretQuery(string value) => System.Text.RegularExpressions.Regex.IsMatch(value, "(?i)[?&](api_?key|access_?token|token|password|key)=[^&{]+", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}

/// <summary>Summarizes parsed WMS or WMTS capabilities.</summary>
/// <param name="ServiceTitle">Service title.</param>
/// <param name="LayerNames">Advertised layer identifiers.</param>
/// <param name="TileMatrixSets">Advertised WMTS matrix sets.</param>
public sealed record MapServiceMetadata(string? ServiceTitle, IReadOnlyList<string> LayerNames, IReadOnlyList<string> TileMatrixSets);

/// <summary>Parses WMS and WMTS capabilities without renderer dependencies.</summary>
public static class MapServiceMetadataParser
{
    /// <summary>Parses a capabilities XML document.</summary>
    public static MapServiceMetadata Parse(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        static string? Value(XElement element, string name) => element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value;
        var service = document.Descendants().FirstOrDefault(element => element.Name.LocalName is "Service" or "ServiceIdentification");
        var title = service is null ? null : Value(service, "Title");
        var layers = document.Descendants().Where(element => element.Name.LocalName == "Layer")
            .Select(element => Value(element, "Identifier") ?? Value(element, "Name")).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Cast<string>().ToArray();
        var matrixSets = document.Descendants().Where(element => element.Name.LocalName == "TileMatrixSet")
            .Select(element => Value(element, "Identifier")).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Cast<string>().ToArray();
        return new(title, layers, matrixSets);
    }
}
