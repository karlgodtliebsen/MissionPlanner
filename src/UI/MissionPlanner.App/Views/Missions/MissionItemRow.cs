using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Display row for a mission item in the mission list.
/// </summary>
/// <param name="Id">The identifier of the underlying mission item.</param>
/// <param name="Number">The 1-based display number (sequence + 1).</param>
/// <param name="Command">The mission command name.</param>
/// <param name="Latitude">The latitude in degrees, or empty when the item has no position.</param>
/// <param name="Longitude">The longitude in degrees, or empty when the item has no position.</param>
/// <param name="Altitude">The altitude in meters, or empty when the item has no altitude.</param>
public sealed record MissionItemRow(
    MissionItemId Id,
    int Number,
    string Command,
    string Latitude,
    string Longitude,
    string Altitude);
