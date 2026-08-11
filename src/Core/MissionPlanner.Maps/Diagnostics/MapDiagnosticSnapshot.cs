using System.Text.Json;
using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Diagnostics;

/// <summary>
/// Contains sanitized, support-safe map subsystem diagnostics.
/// </summary>
/// <param name="SelectedSourceId">Selected stable source identifier.</param>
/// <param name="ProviderId">Provider identifier.</param>
/// <param name="ProductId">Data-product identifier.</param>
/// <param name="SourceKind">Access kind.</param>
/// <param name="ArchiveFormat">Archive format.</param>
/// <param name="PayloadFormat">Tile payload format.</param>
/// <param name="Connectivity">Online/offline state.</param>
/// <param name="CredentialConfigured">Whether the required credential is configured.</param>
/// <param name="PolicyId">Reviewed policy identifier.</param>
/// <param name="PolicyReviewedOn">Policy review date.</param>
/// <param name="AttributionIds">Required attribution identifiers.</param>
/// <param name="CacheSizeBytes">Current HTTP cache size.</param>
/// <param name="ActivePackId">Active pack identifier.</param>
/// <param name="ActivePackVersion">Active pack version.</param>
/// <param name="MapsuiVersion">Mapsui assembly version.</param>
/// <param name="Platform">Runtime platform.</param>
/// <param name="LastSourceError">Sanitized last source error.</param>
public sealed record MapDiagnosticSnapshot(
    string SelectedSourceId,
    string ProviderId,
    string ProductId,
    MapAccessKind SourceKind,
    MapArchiveFormat ArchiveFormat,
    MapTileContentFormat PayloadFormat,
    string Connectivity,
    bool CredentialConfigured,
    string PolicyId,
    DateOnly PolicyReviewedOn,
    IReadOnlyList<string> AttributionIds,
    long CacheSizeBytes,
    string? ActivePackId,
    string? ActivePackVersion,
    string MapsuiVersion,
    string Platform,
    string? LastSourceError)
{
    /// <summary>Serializes the sanitized snapshot for support diagnostics.</summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }
}
