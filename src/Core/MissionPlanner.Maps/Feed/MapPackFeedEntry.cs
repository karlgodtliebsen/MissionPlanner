using System.Text;
using MissionPlanner.Maps.Offline;

namespace MissionPlanner.Maps.Feed;

/// <summary>Describes one Mission Planner-reviewed pack artifact.</summary>
/// <param name="Manifest">Offline-pack manifest and integrity metadata.</param>
/// <param name="SourceId">Reviewed source identifier.</param>
/// <param name="ProductId">Reviewed data-product identifier.</param>
/// <param name="DownloadUri">HTTPS artifact URI; never a hosted tile template.</param>
/// <param name="MinimumMissionPlannerVersion">Minimum compatible Mission Planner version.</param>
/// <param name="MinimumRendererVersion">Minimum compatible renderer version.</param>
/// <param name="NoticeUris">License, provenance, or notice references.</param>
public sealed record MapPackFeedEntry(
    OfflineMapPackManifest Manifest,
    string SourceId,
    string ProductId,
    Uri DownloadUri,
    string MinimumMissionPlannerVersion,
    string MinimumRendererVersion,
    Uri[] NoticeUris);
