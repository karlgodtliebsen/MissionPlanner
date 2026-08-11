using System.Text.Json;
using System.Text.Json.Serialization;

namespace MissionPlanner.Maps.Catalog;

/// <summary>Serializes and deserializes versioned map catalogs.</summary>
public static class MapCatalogSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Deserializes and validates a catalog.</summary>
    /// <param name="json">Catalog JSON.</param>
    /// <returns>The validated catalog.</returns>
    public static MapCatalog Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var catalog = JsonSerializer.Deserialize<MapCatalog>(json, Options)
            ?? throw new JsonException("The map catalog did not contain an object.");
        MapCatalogValidator.ValidateAndThrow(catalog);
        return catalog;
    }

    /// <summary>Serializes a catalog in deterministic identifier order.</summary>
    /// <param name="catalog">Catalog to serialize.</param>
    /// <returns>Formatted catalog JSON.</returns>
    public static string Serialize(MapCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        MapCatalogValidator.ValidateAndThrow(catalog);
        var ordered = catalog with
        {
            Providers = catalog.Providers.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Products = catalog.Products.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Policies = catalog.Policies.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Attributions = catalog.Attributions.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            Sources = catalog.Sources
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => item with { AttributionIds = item.AttributionIds.Order(StringComparer.Ordinal).ToArray() })
                .ToArray()
        };
        return JsonSerializer.Serialize(ordered, Options);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
