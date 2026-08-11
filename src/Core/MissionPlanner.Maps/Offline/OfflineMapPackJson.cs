using System.Text.Json;

namespace MissionPlanner.Maps.Offline;

/// <summary>Serializes offline map pack manifests.</summary>
public static class OfflineMapPackJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };

    /// <summary>Serializes a manifest.</summary>
    public static string Serialize(OfflineMapPackManifest manifest) => JsonSerializer.Serialize(manifest, Options);

    /// <summary>Deserializes a manifest.</summary>
    public static OfflineMapPackManifest Deserialize(string json) =>
        JsonSerializer.Deserialize<OfflineMapPackManifest>(json, Options) ?? throw new InvalidDataException("The pack manifest was empty.");
}
