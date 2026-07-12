using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.MavLink.Parameters.Metadata;

/// <summary>
/// Parses ArduPilot parameter metadata from XML files in the apm.pdef.xml format.
/// </summary>
public sealed class ParameterMetadataXmlParser(ILogger<ParameterMetadataXmlParser> logger)
    : IParameterMetadataParser
{
    /// <inheritdoc/>
    public async Task<Dictionary<string, ParameterMetadata>> ParseAsync(Stream xmlStream, VehicleType vehicleType, CancellationToken cancellationToken = default)
    {
        var metadata = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var doc = await XDocument.LoadAsync(xmlStream, LoadOptions.None, cancellationToken);

            // Navigate to the correct structure: <paramfile>/<vehicles>/<parameters name="ArduCopter">
            var vehicleElementName = VehicleTypeUtil.GetVehicleElementName(vehicleType);
            var parametersElement = doc.Root?
                .Element("vehicles")?
                .Elements("parameters")
                .FirstOrDefault(p => p.Attribute("name")?.Value == vehicleElementName);

            if (parametersElement == null)
            {
                logger.LogWarning("No metadata found for vehicle type {VehicleType} (looking for parameters[@name='{ElementName}'])", vehicleType, vehicleElementName);
                return metadata;
            }

            // Parse each <param> element
            foreach (var paramElement in parametersElement.Elements("param"))
            {
                var paramNameAttr = paramElement.Attribute("name")?.Value;

                if (string.IsNullOrWhiteSpace(paramNameAttr))
                {
                    logger.LogDebug("Skipping parameter with no name attribute");
                    continue;
                }

                // Remove vehicle prefix from parameter name (e.g., "ArduCopter:FORMAT_VERSION" -> "FORMAT_VERSION")
                var paramName = paramNameAttr.Contains(':')
                    ? paramNameAttr.Substring(paramNameAttr.IndexOf(':') + 1)
                    : paramNameAttr;

                try
                {
                    var paramMetadata = ParseParameterElement(paramName, paramElement);
                    metadata[paramName] = paramMetadata;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse metadata for parameter {ParameterName}", paramName);
                }
            }

            logger.LogInformation("Parsed {Count} parameter metadata entries for {VehicleType}", metadata.Count, vehicleType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse parameter metadata XML");
            throw;
        }

        return metadata;
    }

    private ParameterMetadata ParseParameterElement(string paramName, XElement element)
    {
        // Get attributes from <param> element
        var displayName = element.Attribute("humanName")?.Value;
        var description = element.Attribute("documentation")?.Value;
        var userLevel = element.Attribute("user")?.Value;

        // Get field values from <field name="..."> elements
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
                    rebootRequired = fieldValue?.Equals("True", StringComparison.OrdinalIgnoreCase) ?? false;
                    break;
                case "ReadOnly":
                    readOnly = fieldValue?.Equals("True", StringComparison.OrdinalIgnoreCase) ?? false;
                    break;
            }
        }

        // Parse <values> element if present (enumerated values)
        string? valuesStr = null;
        var valuesElement = element.Element("values");
        if (valuesElement != null)
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

        // Use bitmask from <field> if values wasn't set from <values> element
        // (Some params have bitmask defined in field, others in dedicated element)
        if (string.IsNullOrWhiteSpace(valuesStr) && !string.IsNullOrWhiteSpace(bitmaskStr))
        {
            // Bitmask is already in correct format from field
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

/// <summary>
/// Utility class for vehicle type related operations.
/// </summary>
public static class VehicleTypeUtil
{
    /// <summary>
    /// Gets the XML element name for a given vehicle type.
    /// </summary>
    /// <param name="vehicleType">The vehicle type.</param>
    /// <returns>The XML element name corresponding to the vehicle type.</returns>
    /// <exception cref="ArgumentException">Thrown when the vehicle type is unknown.</exception>
    public static string GetVehicleElementName(VehicleType vehicleType)
    {
        return vehicleType switch
        {
            VehicleType.ArduCopter => "ArduCopter",
            VehicleType.ArduPlane => "ArduPlane",
            VehicleType.Rover => "Rover",
            VehicleType.ArduSub => "ArduSub",
            VehicleType.AntennaTracker => "AntennaTracker",
            VehicleType.AP_Periph => "AP_Periph",
            VehicleType.SITL => "SITL",
            VehicleType.Blimp => "Blimp",
            VehicleType.Heli => "Heli",
            var _ => throw new ArgumentException($"Unknown vehicle type: {vehicleType}", nameof(vehicleType))
        };
    }
}
