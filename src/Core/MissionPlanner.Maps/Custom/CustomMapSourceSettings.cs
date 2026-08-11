using System.Text.Json;
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
