using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Osd;

/// <summary>Identifies the severity of an OSD layout validation result.</summary>
public enum OsdValidationSeverity
{
    /// <summary>The layout cannot be applied.</summary>
    Error,
    /// <summary>The layout can be applied only after explicit acknowledgement.</summary>
    Warning
}

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

/// <summary>Defines one discovered OSD screen and its character-grid capabilities.</summary>
/// <param name="Number">The one-based screen number.</param>
/// <param name="Title">The screen title.</param>
/// <param name="GridWidth">The discovered character-grid width.</param>
/// <param name="GridHeight">The discovered character-grid height.</param>
/// <param name="SupportsDynamicOverlaps">Whether metadata advertises dynamic overlapping items.</param>
/// <param name="ScreenParameterNames">Screen enable/options/font/resolution parameters.</param>
/// <param name="Items">The discovered screen items.</param>
public sealed record OsdScreenDefinition(
    int Number,
    string Title,
    int GridWidth,
    int GridHeight,
    bool SupportsDynamicOverlaps,
    IReadOnlyList<string> ScreenParameterNames,
    IReadOnlyList<OsdItemDefinition> Items);

/// <summary>Represents an opened OSD workspace over the shared parameter session.</summary>
/// <param name="Scope">The active vehicle and firmware scope.</param>
/// <param name="Session">The shared editing session.</param>
/// <param name="GlobalParameterNames">OSD parameters not owned by a numbered screen.</param>
/// <param name="Screens">The discovered screens.</param>
public sealed record OsdConfigurationWorkspace(
    ParameterEditScope Scope,
    IParameterEditSession Session,
    IReadOnlyList<string> GlobalParameterNames,
    IReadOnlyList<OsdScreenDefinition> Screens);

/// <summary>Describes a grid-bound or overlap validation result.</summary>
/// <param name="Severity">The result severity.</param>
/// <param name="ScreenNumber">The affected screen.</param>
/// <param name="ItemKeys">The affected item keys.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record OsdValidationIssue(
    OsdValidationSeverity Severity,
    int ScreenNumber,
    IReadOnlyList<string> ItemKeys,
    string Message);

/// <summary>Reports an OSD screen apply.</summary>
/// <param name="Success">Whether validation and all confirmed writes succeeded.</param>
/// <param name="ValidationIssues">The layout validation results.</param>
/// <param name="ParameterReport">The shared-session report, when writes were attempted.</param>
public sealed record OsdApplyResult(
    bool Success,
    IReadOnlyList<OsdValidationIssue> ValidationIssues,
    ParameterApplyReport? ParameterReport);

/// <summary>Reports a layout import into pending state.</summary>
/// <param name="Success">Whether the import was valid and applied atomically.</param>
/// <param name="ImportedCount">The number of imported discovered parameters.</param>
/// <param name="IgnoredNames">Parameters not discovered on the active firmware.</param>
/// <param name="Issues">Layout validation issues.</param>
/// <param name="Errors">Format, scope, or metadata errors.</param>
public sealed record OsdImportResult(
    bool Success,
    int ImportedCount,
    IReadOnlyList<string> IgnoredNames,
    IReadOnlyList<OsdValidationIssue> Issues,
    IReadOnlyList<string> Errors);

/// <summary>Provides one item for rendering in a platform-neutral character-grid preview.</summary>
/// <param name="Key">The item key.</param>
/// <param name="Title">The short preview label.</param>
/// <param name="Column">The zero-based column.</param>
/// <param name="Row">The zero-based row.</param>
/// <param name="IsEnabled">Whether the item is enabled.</param>
public sealed record OsdPreviewItem(string Key, string Title, int Column, int Row, bool IsEnabled);
