namespace MissionPlanner.Core.Setup;

/// <summary>Represents a consolidated, exportable setup summary for one vehicle.</summary>
/// <param name="VehicleKey">The stable vehicle key.</param>
/// <param name="DisplayName">The vehicle display name.</param>
/// <param name="Firmware">The firmware description.</param>
/// <param name="GeneratedAt">The generation timestamp.</param>
/// <param name="Sections">The summary sections.</param>
/// <param name="Warnings">The aggregated warnings.</param>
public sealed record SetupSummary(
    string VehicleKey,
    string DisplayName,
    string Firmware,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SetupSummarySection> Sections,
    IReadOnlyList<string> Warnings);
