using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using MissionPlanner.MavLink.Parameters.Metadata.Abstractions;

namespace MissionPlanner.MavLink.Parameters.Metadata;

/// <summary>
/// Parses ArduPilot parameter metadata from XML files in the apm.pdef.xml format.
/// </summary>
public sealed class ParameterMetadataXmlParser(ILogger<ParameterMetadataXmlParser> logger)
    : IParameterMetadataParser
{
    /// <inheritdoc/>
    public async Task<Dictionary<string, ParameterMetadata>> ParseAsync(
        Stream xmlStream,
        VehicleType vehicleType,
        CancellationToken cancellationToken = default)
    {
        var metadata = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var doc = await XDocument.LoadAsync(xmlStream, LoadOptions.None, cancellationToken);
            var root = doc.Root;

            if (root is null)
            {
                logger.LogWarning("Parameter metadata XML has no root element");
                return metadata;
            }

            var vehicleElementName = VehicleTypeUtil.GetVehicleElementName(vehicleType);
            var vehicleParameters = root
                .Element("vehicles")?
                .Elements("parameters")
                .FirstOrDefault(p => string.Equals(
                    p.Attribute("name")?.Value,
                    vehicleElementName,
                    StringComparison.OrdinalIgnoreCase));

            if (vehicleParameters is null)
            {
                logger.LogWarning(
                    "No vehicle-specific metadata found for vehicle type {VehicleType} " +
                    "(looking for vehicles/parameters[@name='{ElementName}'])",
                    vehicleType,
                    vehicleElementName);
            }

            // A per-vehicle apm.pdef.xml contains both the selected vehicle's own
            // parameters and every library parameter applicable to that firmware.
            // Load libraries first, then vehicle-specific metadata so the latter wins
            // if the same parameter name is present in both sections.
            var libraryGroups = root
                .Element("libraries")?
                .Elements("parameters")
                .ToArray() ?? [];

            var libraryCount = 0;
            foreach (var libraryGroup in libraryGroups)
            {
                libraryCount += ParseParameterGroup(libraryGroup, metadata, overwriteExisting: false);
            }

            var vehicleCount = vehicleParameters is null
                ? 0
                : ParseParameterGroup(vehicleParameters, metadata, overwriteExisting: true);

            logger.LogInformation(
                "Parsed {TotalCount} parameter metadata entries for {VehicleType}: " +
                "{VehicleCount} vehicle entries and {LibraryCount} library entries from {LibraryGroupCount} library groups",
                metadata.Count,
                vehicleType,
                vehicleCount,
                libraryCount,
                libraryGroups.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse parameter metadata XML");
            throw;
        }

        return metadata;
    }

    private int ParseParameterGroup(
        XElement parametersElement,
        Dictionary<string, ParameterMetadata> metadata,
        bool overwriteExisting)
    {
        var parsedCount = 0;
        var groupName = parametersElement.Attribute("name")?.Value ?? "<unnamed>";

        foreach (var paramElement in parametersElement.Elements("param"))
        {
            var qualifiedName = paramElement.Attribute("name")?.Value;

            if (string.IsNullOrWhiteSpace(qualifiedName))
            {
                logger.LogDebug("Skipping parameter with no name attribute in metadata group {GroupName}", groupName);
                continue;
            }

            var paramName = GetUnqualifiedParameterName(qualifiedName);

            try
            {
                var paramMetadata = ParseParameterElement(paramName, paramElement);

                if (overwriteExisting)
                {
                    metadata[paramName] = paramMetadata;
                    parsedCount++;
                    continue;
                }

                if (metadata.TryAdd(paramName, paramMetadata))
                {
                    parsedCount++;
                }
                else
                {
                    logger.LogDebug(
                        "Ignoring duplicate library metadata for parameter {ParameterName} from group {GroupName}",
                        paramName,
                        groupName);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to parse metadata for parameter {ParameterName} from group {GroupName}",
                    paramName,
                    groupName);
            }
        }

        return parsedCount;
    }

    private static string GetUnqualifiedParameterName(string qualifiedName)
    {
        var separatorIndex = qualifiedName.IndexOf(':');
        return separatorIndex >= 0
            ? qualifiedName[(separatorIndex + 1)..]
            : qualifiedName;
    }

    private static ParameterMetadata ParseParameterElement(string paramName, XElement element)
    {
        var displayName = element.Attribute("humanName")?.Value;
        var description = element.Attribute("documentation")?.Value;
        var userLevel = element.Attribute("user")?.Value;

        string? units = null;
        string? unitText = null;
        string? range = null;
        string? increment = null;
        string? bitmaskStr = null;
        var rebootRequired = false;
        var readOnly = false;

        foreach (var field in element.Elements("field"))
        {
            var fieldName = field.Attribute("name")?.Value;
            var fieldValue = field.Value;

            switch (fieldName)
            {
                case "Units":
                    units = fieldValue;
                    break;
                case "UnitText":
                    unitText = fieldValue;
                    break;
                case "Range":
                    range = fieldValue;
                    break;
                case "Increment":
                    increment = fieldValue;
                    break;
                case "Bitmask":
                    bitmaskStr = fieldValue;
                    break;
                case "RebootRequired":
                    rebootRequired = fieldValue.Equals("True", StringComparison.OrdinalIgnoreCase);
                    break;
                case "ReadOnly":
                    readOnly = fieldValue.Equals("True", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }

        string? valuesStr = null;
        var valuesElement = element.Element("values");
        if (valuesElement is not null)
        {
            var valuesList = new List<string>();
            foreach (var valueElement in valuesElement.Elements("value"))
            {
                var code = valueElement.Attribute("code")?.Value;
                var label = valueElement.Value;
                if (!string.IsNullOrWhiteSpace(code))
                {
                    valuesList.Add($"{code}:{label}");
                }
            }

            if (valuesList.Count > 0)
            {
                valuesStr = string.Join(",", valuesList);
            }
        }

        return new ParameterMetadata(
            paramName,
            displayName,
            description,
            units,
            unitText,
            range,
            valuesStr,
            bitmaskStr,
            increment,
            userLevel,
            rebootRequired,
            readOnly);
    }
}
