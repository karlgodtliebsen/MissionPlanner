using System.Xml.Linq;

namespace MissionPlanner.Maps.Custom;

/// <summary>Parses WMS and WMTS capabilities without renderer dependencies.</summary>
public static class MapServiceMetadataParser
{
    /// <summary>Parses a capabilities XML document.</summary>
    public static MapServiceMetadata Parse(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);

        static string? Value(XElement element, string name)
        {
            return element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value;
        }

        var service = document.Descendants().FirstOrDefault(element => element.Name.LocalName is "Service" or "ServiceIdentification");
        var title = service is null ? null : Value(service, "Title");
        var layers = document.Descendants().Where(element => element.Name.LocalName == "Layer")
            .Select(element => Value(element, "Identifier") ?? Value(element, "Name")).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Cast<string>().ToArray();
        var matrixSets = document.Descendants().Where(element => element.Name.LocalName == "TileMatrixSet")
            .Select(element => Value(element, "Identifier")).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Cast<string>().ToArray();
        return new MapServiceMetadata(title, layers, matrixSets);
    }
}
