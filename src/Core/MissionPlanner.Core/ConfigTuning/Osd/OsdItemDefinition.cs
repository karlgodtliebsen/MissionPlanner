using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>Defines one discovered parameter-backed OSD item.</summary>
/// <param name="ScreenNumber">The owning screen number.</param>
/// <param name="Key">The firmware item key.</param>
/// <param name="Title">The metadata-derived display title.</param>
/// <param name="EnableParameterName">The enable parameter, when present.</param>
/// <param name="ColumnParameterName">The column parameter.</param>
/// <param name="RowParameterName">The row parameter.</param>
/// <param name="AdditionalParameterNames">Discovered item options, units, or warning parameters.</param>
/// <param name="Description">The metadata-derived description.</param>
public sealed record OsdItemDefinition(
    int ScreenNumber,
    string Key,
    string Title,
    string? EnableParameterName,
    string ColumnParameterName,
    string RowParameterName,
    IReadOnlyList<string> AdditionalParameterNames,
    string Description);
