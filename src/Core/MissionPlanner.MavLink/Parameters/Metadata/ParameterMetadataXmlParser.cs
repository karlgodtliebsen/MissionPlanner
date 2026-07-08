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
    public async Task<Dictionary<string, ParameterMetadata>> ParseAsync(
        Stream xmlStream,
        VehicleType vehicleType,
        CancellationToken cancellationToken = default)
    {
        var metadata = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var doc = await XDocument.LoadAsync(xmlStream, LoadOptions.None, cancellationToken);

            // Get the vehicle-specific root element
            var vehicleElementName = GetVehicleElementName(vehicleType);
            var vehicleElement = doc.Root?.Element(vehicleElementName);

            if (vehicleElement == null)
            {
                logger.LogWarning(
                    "No metadata found for vehicle type {VehicleType} (looking for element {ElementName})",
                    vehicleType,
                    vehicleElementName);
                return metadata;
            }

            // Parse each parameter
            foreach (var paramElement in vehicleElement.Elements())
            {
                var paramName = paramElement.Name.LocalName;

                try
                {
                    var paramMetadata = ParseParameterElement(paramName, paramElement);
                    metadata[paramName] = paramMetadata;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to parse metadata for parameter {ParameterName}",
                        paramName);
                }
            }

            logger.LogInformation(
                "Parsed {Count} parameter metadata entries for {VehicleType}",
                metadata.Count,
                vehicleType);
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
        var displayName = element.Element("DisplayName")?.Value;
        var description = element.Element("Description")?.Value;
        var units = element.Element("Units")?.Value;
        var range = element.Element("Range")?.Value;
        var values = element.Element("Values")?.Value;
        var bitmask = element.Element("Bitmask")?.Value;
        var increment = element.Element("Increment")?.Value;
        var userLevel = element.Element("User")?.Value;

        var rebootRequired = element.Element("RebootRequired")?.Value?.Equals("True",
            StringComparison.OrdinalIgnoreCase) ?? false;

        var readOnly = element.Element("ReadOnly")?.Value?.Equals("True",
            StringComparison.OrdinalIgnoreCase) ?? false;

        return new ParameterMetadata(
            paramName,
            displayName,
            description,
            units,
            range,
            values,
            bitmask,
            increment,
            userLevel,
            rebootRequired,
            readOnly);
    }

    private static string GetVehicleElementName(VehicleType vehicleType)
    {
        return vehicleType switch
        {
            VehicleType.ArduCopter => "ArduCopter2",
            VehicleType.ArduPlane => "ArduPlane",
            VehicleType.Rover => "Rover",
            VehicleType.ArduSub => "ArduSub",
            VehicleType.AntennaTracker => "AntennaTracker",
            VehicleType.AP_Periph => "AP_Periph",
            VehicleType.SITL => "SITL",
            VehicleType.Blimp => "Blimp",
            VehicleType.Heli => "Heli",
            _ => throw new ArgumentException($"Unknown vehicle type: {vehicleType}", nameof(vehicleType))
        };
    }
}
