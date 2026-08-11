using System.Text.Json;

namespace MissionPlanner.Maps.Offline;

/// <summary>Serializes offline map pack manifests.</summary>
public static class OfflineMapPackJson
{
    private static readonly JsonSerializerOptions options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };

    /// <summary>Serializes a manifest.</summary>
    public static string Serialize(OfflineMapPackManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, options);
    }

    /// <summary>Deserializes a manifest.</summary>
    public static OfflineMapPackManifest Deserialize(string json)
    {
        return JsonSerializer.Deserialize<OfflineMapPackManifest>(json, options) ?? throw new InvalidDataException("The pack manifest was empty.");
    }
}
