using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Tuning;

/// <summary>Defines one repeated component in an advanced tuning descriptor.</summary>
/// <param name="Key">The stable component key and parameter suffix.</param>
/// <param name="Title">The component title.</param>
/// <param name="Description">The plain-language component explanation.</param>
/// <param name="FallbackUnits">Units used when firmware metadata supplies none.</param>
public sealed record AdvancedTuningComponent(
    string Key,
    string Title,
    string Description,
    string FallbackUnits);

/// <summary>Generates one lazy advanced tuning group across axes and instances.</summary>
/// <param name="Key">The stable descriptor key.</param>
/// <param name="Category">The search and navigation category.</param>
/// <param name="Title">The descriptor title.</param>
/// <param name="Description">The descriptor explanation.</param>
/// <param name="ParameterPrefixPattern">The prefix containing optional {axis} and {instance} tokens.</param>
/// <param name="Axes">Axis tokens, or one empty token for a non-axis descriptor.</param>
/// <param name="Instances">Instance numbers, or zero for a non-instance descriptor.</param>
/// <param name="Components">The generated parameter suffixes.</param>
/// <param name="Rules">Per-axis/per-instance coupled validation rules.</param>
/// <param name="ExpertWarning">The required expert warning.</param>
public sealed record AdvancedTuningDescriptor(
    string Key,
    string Category,
    string Title,
    string Description,
    string ParameterPrefixPattern,
    IReadOnlyList<string> Axes,
    IReadOnlyList<int> Instances,
    IReadOnlyList<AdvancedTuningComponent> Components,
    IReadOnlyList<BasicTuningRule> Rules,
    string ExpertWarning)
{
    /// <summary>Gets whether the descriptor supports copying between axes.</summary>
    public bool SupportsAxisCopy => Axes.Count(axis => !string.IsNullOrWhiteSpace(axis)) > 1;
}

/// <summary>Defines the expanded form of one advanced descriptor field.</summary>
/// <param name="DescriptorKey">The source descriptor key.</param>
/// <param name="Category">The source category.</param>
/// <param name="Axis">The axis token, when applicable.</param>
/// <param name="Instance">The instance number, when applicable.</param>
/// <param name="Component">The component definition.</param>
/// <param name="Parameter">The exact vehicle parameter definition.</param>
public sealed record AdvancedTuningFieldDefinition(
    string DescriptorKey,
    string Category,
    string Axis,
    int Instance,
    AdvancedTuningComponent Component,
    ParameterFieldDefinition Parameter);

/// <summary>Defines an advanced tuning catalog for one firmware family.</summary>
/// <param name="Family">The firmware family.</param>
/// <param name="Descriptors">The ordered reusable descriptors.</param>
public sealed record ExtendedTuningProfile(
    FirmwareFamily Family,
    IReadOnlyList<AdvancedTuningDescriptor> Descriptors);

/// <summary>Associates an expanded advanced field with a present vehicle parameter.</summary>
/// <param name="Definition">The expanded field definition.</param>
/// <param name="ParameterName">The resolved parameter name.</param>
public sealed record ResolvedAdvancedTuningField(
    AdvancedTuningFieldDefinition Definition,
    string ParameterName);

/// <summary>Associates one descriptor with the fields present on a vehicle.</summary>
/// <param name="Descriptor">The descriptor.</param>
/// <param name="Fields">The supported expanded fields.</param>
public sealed record ResolvedAdvancedTuningGroup(
    AdvancedTuningDescriptor Descriptor,
    IReadOnlyList<ResolvedAdvancedTuningField> Fields)
{
    /// <summary>Gets the present axis names in stable order.</summary>
    public IReadOnlyList<string> Axes => Fields
        .Select(item => item.Definition.Axis)
        .Where(axis => !string.IsNullOrWhiteSpace(axis))
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

/// <summary>Represents an opened advanced tuning workspace over the shared parameter session.</summary>
/// <param name="Profile">The selected family profile.</param>
/// <param name="Session">The shared parameter session.</param>
/// <param name="Groups">The presence-gated lazy groups.</param>
public sealed record ExtendedTuningWorkspace(
    ExtendedTuningProfile Profile,
    IParameterEditSession Session,
    IReadOnlyList<ResolvedAdvancedTuningGroup> Groups);

/// <summary>Describes an advanced cross-field validation failure.</summary>
/// <param name="DescriptorKey">The affected descriptor.</param>
/// <param name="ParameterNames">The involved parameters.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record ExtendedTuningValidationIssue(
    string DescriptorKey,
    IReadOnlyList<string> ParameterNames,
    string Message);

/// <summary>Reports an advanced group apply.</summary>
/// <param name="Success">Whether validation and confirmed writes succeeded.</param>
/// <param name="ValidationIssues">The validation failures.</param>
/// <param name="ParameterReport">The shared-session write result, when writes were attempted.</param>
public sealed record ExtendedTuningApplyResult(
    bool Success,
    IReadOnlyList<ExtendedTuningValidationIssue> ValidationIssues,
    ParameterApplyReport? ParameterReport);

/// <summary>Provides a normalized axis-comparison value for one component.</summary>
/// <param name="Axis">The axis.</param>
/// <param name="Component">The component key.</param>
/// <param name="ParameterName">The vehicle parameter.</param>
/// <param name="PendingValue">The pending value.</param>
/// <param name="NormalizedMagnitude">The magnitude divided by the largest axis magnitude for the component.</param>
public sealed record AdvancedTuningComparisonValue(
    string Axis,
    string Component,
    string ParameterName,
    double PendingValue,
    double NormalizedMagnitude);

/// <summary>Describes one proposed target change in an axis-copy preview.</summary>
/// <param name="SourceParameter">The source parameter.</param>
/// <param name="TargetParameter">The target parameter.</param>
/// <param name="Component">The copied component.</param>
/// <param name="SourceValue">The proposed source value.</param>
/// <param name="TargetValue">The target pending value captured by the preview.</param>
public sealed record AxisCopyChange(
    string SourceParameter,
    string TargetParameter,
    string Component,
    double SourceValue,
    double TargetValue);

/// <summary>Represents a non-mutating, scope-bound axis-copy preview.</summary>
/// <param name="Scope">The vehicle and firmware scope.</param>
/// <param name="DescriptorKey">The descriptor.</param>
/// <param name="SourceAxis">The source axis.</param>
/// <param name="TargetAxis">The target axis.</param>
/// <param name="Changes">The proposed component changes.</param>
public sealed record AxisCopyPreview(
    ParameterEditScope Scope,
    string DescriptorKey,
    string SourceAxis,
    string TargetAxis,
    IReadOnlyList<AxisCopyChange> Changes);

/// <summary>Reports applying a previously reviewed axis-copy preview to pending state.</summary>
/// <param name="Success">Whether every preview value was accepted.</param>
/// <param name="Errors">Stale-preview, metadata, or coupled validation errors.</param>
public sealed record AxisCopyApplyResult(bool Success, IReadOnlyList<string> Errors);

/// <summary>Captures read-only controller response context from PID_TUNING telemetry.</summary>
/// <param name="VehicleId">The source vehicle.</param>
/// <param name="Axis">The protocol axis identifier.</param>
/// <param name="Desired">The desired response.</param>
/// <param name="Achieved">The achieved response.</param>
/// <param name="Error">Desired minus achieved.</param>
/// <param name="FeedForward">The feed-forward contribution.</param>
/// <param name="Proportional">The proportional contribution.</param>
/// <param name="Integral">The integral contribution.</param>
/// <param name="Derivative">The derivative contribution.</param>
/// <param name="ReceivedAt">The reception time.</param>
public sealed record ControlResponseMetric(
    VehicleId VehicleId,
    byte Axis,
    float Desired,
    float Achieved,
    float Error,
    float FeedForward,
    float Proportional,
    float Integral,
    float Derivative,
    DateTimeOffset ReceivedAt);

/// <summary>Provides the event payload for a read-only control-response update.</summary>
/// <param name="metric">The latest metric.</param>
public sealed class ControlResponseMetricChangedEventArgs(ControlResponseMetric metric) : EventArgs
{
    /// <summary>Gets the latest metric.</summary>
    public ControlResponseMetric Metric { get; } = metric;
}
