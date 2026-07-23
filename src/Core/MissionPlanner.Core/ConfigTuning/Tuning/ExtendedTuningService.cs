using Microsoft.Extensions.Logging;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Implements presence-gated advanced tuning, reviewable axis copies, and confirmed group writes.</summary>
public sealed class ExtendedTuningService(
    IExtendedTuningProfileCatalog catalog,
    IParameterEditSessionFactory sessionFactory,
    IVehicleParameterRegistry parameterRegistry,
    ILogger<ExtendedTuningService> logger) : IExtendedTuningService
{
    /// <inheritdoc />
    public async Task<ExtendedTuningWorkspace?> OpenAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var session = sessionFactory.Create(vehicleId);
        var profile = catalog.GetProfile(session.Scope.FirmwareIdentity.Family);
        if (profile is null)
        {
            return null;
        }

        var expanded = profile.Descriptors.ToDictionary(
            descriptor => descriptor.Key,
            descriptor => catalog.Expand(descriptor),
            StringComparer.Ordinal);
        await session.LoadDefinitionsAsync(
            expanded.Values.SelectMany(items => items).Select(item => item.Parameter).ToArray(),
            cancellationToken).ConfigureAwait(false);

        var live = parameterRegistry.GetAllParameters(vehicleId);
        var groups = profile.Descriptors
            .Select(descriptor => new ResolvedAdvancedTuningGroup(
                descriptor,
                expanded[descriptor.Key]
                    .Select(definition => (Definition: definition, Name: definition.Parameter.Resolve(live)))
                    .Where(item => item.Name is not null && session.GetField(item.Name) is not null)
                    .Select(item => new ResolvedAdvancedTuningField(item.Definition, item.Name!))
                    .ToArray()))
            .Where(group => group.Fields.Count > 0)
            .ToArray();
        logger.LogInformation(
            "Opened Extended Tuning for {VehicleId} with {GroupCount} lazy groups and {FieldCount} present fields.",
            vehicleId,
            groups.Length,
            groups.Sum(group => group.Fields.Count));
        return new ExtendedTuningWorkspace(profile, session, groups);
    }

    /// <inheritdoc />
    public IReadOnlyList<ExtendedTuningValidationIssue> ValidateGroup(
        ExtendedTuningWorkspace workspace,
        string descriptorKey)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var group = GetGroup(workspace, descriptorKey);
        var issues = new List<ExtendedTuningValidationIssue>();
        foreach (var item in group.Fields)
        {
            if (workspace.Session.GetField(item.ParameterName)?.ValidationError is { } error)
            {
                issues.Add(new ExtendedTuningValidationIssue(descriptorKey, [item.ParameterName], error));
            }
        }

        foreach (var fieldSet in group.Fields.GroupBy(
                     item => (item.Definition.Axis, item.Definition.Instance)))
        {
            var byComponent = fieldSet.ToDictionary(item => item.Definition.Component.Key, StringComparer.Ordinal);
            foreach (var rule in group.Descriptor.Rules)
            {
                if (!byComponent.TryGetValue(rule.FirstFieldKey, out var first) ||
                    !byComponent.TryGetValue(rule.SecondFieldKey, out var second) ||
                    workspace.Session.GetField(first.ParameterName) is not { } firstState ||
                    workspace.Session.GetField(second.ParameterName) is not { } secondState)
                {
                    continue;
                }

                var invalid = rule.Kind switch
                {
                    BasicTuningRuleKind.LessThanOrEqual => firstState.PendingValue > secondState.PendingValue,
                    BasicTuningRuleKind.PositiveCompanion => firstState.PendingValue > 0 && secondState.PendingValue <= 0,
                    _ => throw new ArgumentOutOfRangeException(nameof(rule.Kind), rule.Kind, "Unknown advanced tuning rule kind.")
                };
                if (invalid)
                {
                    var context = string.IsNullOrWhiteSpace(fieldSet.Key.Axis)
                        ? fieldSet.Key.Instance == 0 ? string.Empty : $" Instance {fieldSet.Key.Instance}."
                        : $" Axis {fieldSet.Key.Axis}.";
                    issues.Add(new ExtendedTuningValidationIssue(
                        descriptorKey,
                        [first.ParameterName, second.ParameterName],
                        rule.Message + context));
                }
            }
        }

        return issues;
    }

    /// <inheritdoc />
    public async Task<ExtendedTuningApplyResult> ApplyGroupAsync(
        ExtendedTuningWorkspace workspace,
        string descriptorKey,
        CancellationToken cancellationToken = default)
    {
        var group = GetGroup(workspace, descriptorKey);
        var issues = ValidateGroup(workspace, descriptorKey);
        if (issues.Count > 0)
        {
            return new ExtendedTuningApplyResult(false, issues, null);
        }

        var report = await workspace.Session.ApplyAsync(
            group.Fields.Select(item => item.ParameterName).ToArray(),
            cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Extended Tuning group {DescriptorKey} apply for {VehicleId} completed with success {Success}.",
            descriptorKey,
            workspace.Session.VehicleId,
            report.Success);
        return new ExtendedTuningApplyResult(report.Success, [], report);
    }

    /// <inheritdoc />
    public void RevertGroup(ExtendedTuningWorkspace workspace, string descriptorKey)
    {
        foreach (var item in GetGroup(workspace, descriptorKey).Fields)
        {
            workspace.Session.Revert(item.ParameterName);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AdvancedTuningComparisonValue> CompareAxes(
        ExtendedTuningWorkspace workspace,
        string descriptorKey)
    {
        var fields = GetGroup(workspace, descriptorKey).Fields
            .Where(item => !string.IsNullOrWhiteSpace(item.Definition.Axis))
            .Select(item => (Item: item, State: workspace.Session.GetField(item.ParameterName)))
            .Where(item => item.State is not null)
            .ToArray();
        var maxima = fields
            .GroupBy(item => item.Item.Definition.Component.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Max(item => Math.Abs(item.State!.PendingValue)),
                StringComparer.Ordinal);
        return fields.Select(item =>
        {
            var maximum = maxima[item.Item.Definition.Component.Key];
            var value = item.State!.PendingValue;
            return new AdvancedTuningComparisonValue(
                item.Item.Definition.Axis,
                item.Item.Definition.Component.Key,
                item.Item.ParameterName,
                value,
                maximum <= double.Epsilon ? 0 : Math.Abs(value) / maximum);
        }).ToArray();
    }

    /// <inheritdoc />
    public AxisCopyPreview PreviewCopyAxis(
        ExtendedTuningWorkspace workspace,
        string descriptorKey,
        string sourceAxis,
        string targetAxis)
    {
        var group = GetGroup(workspace, descriptorKey);
        if (!group.Descriptor.SupportsAxisCopy ||
            string.IsNullOrWhiteSpace(sourceAxis) ||
            string.IsNullOrWhiteSpace(targetAxis) ||
            string.Equals(sourceAxis, targetAxis, StringComparison.Ordinal))
        {
            throw new ArgumentException("Choose two different supported axes for the copy preview.");
        }

        var sourceFields = group.Fields
            .Where(item => string.Equals(item.Definition.Axis, sourceAxis, StringComparison.Ordinal))
            .ToDictionary(
                item => (item.Definition.Instance, item.Definition.Component.Key),
                item => item);
        var changes = group.Fields
            .Where(item => string.Equals(item.Definition.Axis, targetAxis, StringComparison.Ordinal))
            .Select(target => sourceFields.TryGetValue(
                (target.Definition.Instance, target.Definition.Component.Key),
                out var source)
                ? new AxisCopyChange(
                    source.ParameterName,
                    target.ParameterName,
                    target.Definition.Component.Key,
                    workspace.Session.GetField(source.ParameterName)!.PendingValue,
                    workspace.Session.GetField(target.ParameterName)!.PendingValue)
                : null)
            .Where(change => change is not null)
            .Cast<AxisCopyChange>()
            .ToArray();
        if (changes.Length == 0)
        {
            throw new InvalidOperationException("The selected axes have no matching present components to copy.");
        }

        return new AxisCopyPreview(workspace.Session.Scope, descriptorKey, sourceAxis, targetAxis, changes);
    }

    /// <inheritdoc />
    public AxisCopyApplyResult ApplyCopyAxisPreview(
        ExtendedTuningWorkspace workspace,
        AxisCopyPreview preview)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(preview);
        if (preview.Scope != workspace.Session.Scope || !workspace.Session.IsValid)
        {
            return new AxisCopyApplyResult(false, ["The preview belongs to a stale vehicle or firmware session."]);
        }

        GetGroup(workspace, preview.DescriptorKey);
        var stale = preview.Changes.FirstOrDefault(change =>
            workspace.Session.GetField(change.TargetParameter) is not { } current ||
            !NearlyEqual(current.PendingValue, change.TargetValue));
        if (stale is not null)
        {
            return new AxisCopyApplyResult(false, [$"The preview is stale because {stale.TargetParameter} changed. Create a new preview."]);
        }

        var previous = preview.Changes.ToDictionary(
            change => change.TargetParameter,
            change => change.TargetValue,
            StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var change in preview.Changes)
        {
            if (!workspace.Session.TrySetPending(change.TargetParameter, change.SourceValue, out var error) && error is not null)
            {
                errors.Add($"{change.TargetParameter}: {error}");
            }
        }

        if (errors.Count == 0)
        {
            errors.AddRange(ValidateGroup(workspace, preview.DescriptorKey).Select(issue => issue.Message));
        }

        if (errors.Count > 0)
        {
            foreach (var item in previous)
            {
                workspace.Session.TrySetPending(item.Key, item.Value, out _);
            }

            return new AxisCopyApplyResult(false, errors);
        }

        return new AxisCopyApplyResult(true, []);
    }

    private static ResolvedAdvancedTuningGroup GetGroup(ExtendedTuningWorkspace workspace, string descriptorKey) =>
        workspace.Groups.FirstOrDefault(group =>
            string.Equals(group.Descriptor.Key, descriptorKey, StringComparison.Ordinal))
        ?? throw new ArgumentException($"Advanced tuning group '{descriptorKey}' is not present.", nameof(descriptorKey));

    private static bool NearlyEqual(double left, double right)
    {
        const double tolerance = 0.0001;
        return Math.Abs(left - right) <= tolerance * Math.Max(1, Math.Max(Math.Abs(left), Math.Abs(right)));
    }
}
