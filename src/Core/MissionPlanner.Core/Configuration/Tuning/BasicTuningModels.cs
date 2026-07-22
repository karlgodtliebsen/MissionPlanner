using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Tuning;

/// <summary>Identifies a supported coupled validation relationship.</summary>
public enum BasicTuningRuleKind
{
    /// <summary>The first value must be less than or equal to the second value.</summary>
    LessThanOrEqual,
    /// <summary>A positive first value requires a positive companion value.</summary>
    PositiveCompanion
}

/// <summary>Defines a coupled validation rule between two logical tuning fields.</summary>
/// <param name="Kind">The relationship to validate.</param>
/// <param name="FirstFieldKey">The first logical field key.</param>
/// <param name="SecondFieldKey">The second logical field key.</param>
/// <param name="Message">The plain-language validation message.</param>
public sealed record BasicTuningRule(
    BasicTuningRuleKind Kind,
    string FirstFieldKey,
    string SecondFieldKey,
    string Message);

/// <summary>Defines one curated field in a firmware-family tuning profile.</summary>
/// <param name="Key">The stable logical field key.</param>
/// <param name="Title">The user-facing field title.</param>
/// <param name="Description">The plain-language tuning explanation.</param>
/// <param name="FallbackUnits">Units used only when firmware metadata supplies none.</param>
/// <param name="Parameter">The exact parameter names and justified aliases.</param>
/// <param name="Warning">An optional control-stability warning.</param>
/// <param name="RecommendedValue">An authoritative recommendation, when one is available.</param>
/// <param name="RecommendationSource">The authoritative recommendation source.</param>
/// <param name="ExpertOnly">Whether the field must be hidden from Basic Tuning.</param>
public sealed record BasicTuningFieldDefinition(
    string Key,
    string Title,
    string Description,
    string FallbackUnits,
    ParameterFieldDefinition Parameter,
    string? Warning = null,
    double? RecommendedValue = null,
    string? RecommendationSource = null,
    bool ExpertOnly = false)
{
    /// <summary>Gets whether a recommendation has both a value and an authoritative source.</summary>
    public bool HasAuthoritativeRecommendation =>
        RecommendedValue is not null && !string.IsNullOrWhiteSpace(RecommendationSource);
}

/// <summary>Defines one curated group in a basic-tuning profile.</summary>
/// <param name="Key">The stable group key.</param>
/// <param name="Title">The group title.</param>
/// <param name="Description">The group explanation.</param>
/// <param name="Fields">The fields presented in order.</param>
/// <param name="Rules">Coupled validation rules for the group.</param>
/// <param name="Warning">An optional group-level control-stability warning.</param>
public sealed record BasicTuningGroupDefinition(
    string Key,
    string Title,
    string Description,
    IReadOnlyList<BasicTuningFieldDefinition> Fields,
    IReadOnlyList<BasicTuningRule> Rules,
    string? Warning = null);

/// <summary>Defines curated Basic Tuning groups for one firmware family.</summary>
/// <param name="Family">The firmware family.</param>
/// <param name="Groups">The ordered tuning groups.</param>
public sealed record BasicTuningProfile(
    FirmwareFamily Family,
    IReadOnlyList<BasicTuningGroupDefinition> Groups);

/// <summary>Associates a curated field with the parameter name present on a vehicle.</summary>
/// <param name="Definition">The curated field definition.</param>
/// <param name="ParameterName">The resolved live parameter name.</param>
public sealed record ResolvedBasicTuningField(
    BasicTuningFieldDefinition Definition,
    string ParameterName);

/// <summary>Associates a curated group with its supported vehicle parameters.</summary>
/// <param name="Definition">The group definition.</param>
/// <param name="Fields">The supported, non-expert fields.</param>
public sealed record ResolvedBasicTuningGroup(
    BasicTuningGroupDefinition Definition,
    IReadOnlyList<ResolvedBasicTuningField> Fields);

/// <summary>Represents an opened Basic Tuning workspace over the shared parameter session.</summary>
/// <param name="Profile">The selected firmware-family profile.</param>
/// <param name="Session">The shared vehicle-scoped parameter session.</param>
/// <param name="Groups">The supported curated groups.</param>
public sealed record BasicTuningWorkspace(
    BasicTuningProfile Profile,
    IParameterEditSession Session,
    IReadOnlyList<ResolvedBasicTuningGroup> Groups)
{
    /// <summary>Gets every parameter presented by this workspace.</summary>
    public IReadOnlyList<string> PresentedParameterNames => Groups
        .SelectMany(group => group.Fields)
        .Select(item => item.ParameterName)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

/// <summary>Describes a coupled validation failure.</summary>
/// <param name="GroupKey">The affected group key.</param>
/// <param name="ParameterNames">The involved resolved parameter names.</param>
/// <param name="Message">The user-facing validation message.</param>
public sealed record BasicTuningValidationIssue(
    string GroupKey,
    IReadOnlyList<string> ParameterNames,
    string Message);

/// <summary>Reports a group-scoped apply operation.</summary>
/// <param name="Success">Whether validation and all writes succeeded.</param>
/// <param name="ValidationIssues">Coupled validation failures.</param>
/// <param name="ParameterReport">The shared-session write report, when writes were attempted.</param>
public sealed record BasicTuningApplyResult(
    bool Success,
    IReadOnlyList<BasicTuningValidationIssue> ValidationIssues,
    ParameterApplyReport? ParameterReport);

/// <summary>Reports an import into the currently presented tuning fields.</summary>
/// <param name="Success">Whether the import was valid and applied to pending state.</param>
/// <param name="ImportedCount">The number of presented values imported.</param>
/// <param name="IgnoredNames">Names not presented by the current profile.</param>
/// <param name="Errors">Parse, metadata, family, or coupled validation errors.</param>
public sealed record BasicTuningImportResult(
    bool Success,
    int ImportedCount,
    IReadOnlyList<string> IgnoredNames,
    IReadOnlyList<string> Errors);
