using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Represents the assessed status of a safety check or summary entry.</summary>
public enum SetupAssessmentStatus
{
    /// <summary>The item is configured as expected.</summary>
    Pass,
    /// <summary>The item is configured in a way that should be reviewed.</summary>
    Warning,
    /// <summary>The item is available but not configured.</summary>
    NotConfigured,
    /// <summary>The item is not supported by this firmware or vehicle.</summary>
    Unsupported,
    /// <summary>The item could not be assessed from available evidence.</summary>
    NotAssessed
}

/// <summary>Projects one assessed safety check.</summary>
/// <param name="Category">The safety category.</param>
/// <param name="Name">The check name.</param>
/// <param name="Status">The assessed status.</param>
/// <param name="Detail">A user-facing explanation.</param>
public sealed record SafetyCheckItem(string Category, string Name, SetupAssessmentStatus Status, string Detail);

/// <summary>Represents the immutable safety assessment projected by the Setup UI.</summary>
/// <param name="VehicleId">The assessed vehicle.</param>
/// <param name="Items">The assessed safety checks.</param>
/// <param name="Warnings">The evidence-based contradiction or gap warnings.</param>
public sealed record SafetyAssessment(
    VehicleId VehicleId,
    IReadOnlyList<SafetyCheckItem> Items,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Creates an empty assessment for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An empty assessment.</returns>
    public static SafetyAssessment Empty(VehicleId vehicleId) => new(vehicleId, [], []);
}

/// <summary>Projects one labelled entry in a setup summary section.</summary>
/// <param name="Label">The entry label.</param>
/// <param name="Value">The entry value.</param>
/// <param name="Status">The assessed status of the entry.</param>
public sealed record SetupSummaryEntry(string Label, string Value, SetupAssessmentStatus Status);

/// <summary>Projects one titled section of a setup summary.</summary>
/// <param name="Title">The section title.</param>
/// <param name="Entries">The section entries.</param>
public sealed record SetupSummarySection(string Title, IReadOnlyList<SetupSummaryEntry> Entries);

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
