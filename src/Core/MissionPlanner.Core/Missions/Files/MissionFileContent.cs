using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Files;

/// <summary>
/// The result of parsing a mission file.
/// </summary>
/// <param name="Items">The mission items in file order.</param>
/// <param name="Home">The home position carried by the file, if any.</param>
/// <param name="SkippedItems">Number of items skipped because their command is not supported by the domain.</param>
/// <param name="Name">The mission name carried by the file, if any (JSON format only).</param>
public sealed record MissionFileContent(
    IReadOnlyList<MissionItem> Items,
    GeoPosition? Home,
    int SkippedItems,
    string? Name = null);
