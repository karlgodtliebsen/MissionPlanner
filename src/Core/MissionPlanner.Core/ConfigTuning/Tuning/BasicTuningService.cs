using System.Text.Json;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Coordinates Basic Tuning catalogs, validation, import/export, and confirmed group writes.</summary>
public sealed class BasicTuningService(
    IBasicTuningProfileCatalog catalog,
    IParameterEditSessionFactory sessionFactory,
    IVehicleParameterRegistry parameterRegistry,
    ILogger<BasicTuningService> logger) : IBasicTuningService
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <inheritdoc />
    public async Task<BasicTuningWorkspace?> OpenAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var session = sessionFactory.Create(vehicleId);
        var profile = catalog.GetProfile(session.Scope.FirmwareIdentity.Family);
        if (profile is null)
        {
            logger.LogInformation("Basic Tuning is not supported for firmware family {FirmwareFamily} on {VehicleId}.",
                session.Scope.FirmwareIdentity.Family,
                vehicleId);
            return null;
        }

        var definitions = profile.Groups
            .SelectMany(group => group.Fields)
            .Where(field => !field.ExpertOnly)
            .Select(field => field.Parameter)
            .ToArray();
        await session.LoadDefinitionsAsync(definitions, cancellationToken).ConfigureAwait(false);

        var liveParameters = parameterRegistry.GetAllParameters(vehicleId);
        var groups = profile.Groups
            .Select(group => new ResolvedBasicTuningGroup(
                group,
                group.Fields
                    .Where(field => !field.ExpertOnly)
                    .Select(field => (Field: field, Name: field.Parameter.Resolve(liveParameters)))
                    .Where(item => item.Name is not null && session.GetField(item.Name) is not null)
                    .Select(item => new ResolvedBasicTuningField(item.Field, item.Name!))
                    .ToArray()))
            .Where(group => group.Fields.Count > 0)
            .ToArray();

        logger.LogInformation("Opened Basic Tuning for {VehicleId} with {GroupCount} supported groups and {FieldCount} fields.",
            vehicleId,
            groups.Length,
            groups.Sum(group => group.Fields.Count));
        return new BasicTuningWorkspace(profile, session, groups);
    }

    /// <inheritdoc />
    public IReadOnlyList<BasicTuningValidationIssue> ValidateGroup(BasicTuningWorkspace workspace, string groupKey)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var group = GetGroup(workspace, groupKey);
        var byKey = group.Fields.ToDictionary(field => field.Definition.Key, StringComparer.Ordinal);
        var issues = new List<BasicTuningValidationIssue>();

        foreach (var field in group.Fields)
        {
            if (workspace.Session.GetField(field.ParameterName)?.ValidationError is { } metadataError)
            {
                issues.Add(new BasicTuningValidationIssue(groupKey, [field.ParameterName], metadataError));
            }
        }

        foreach (var rule in group.Definition.Rules)
        {
            if (!byKey.TryGetValue(rule.FirstFieldKey, out var first) ||
                !byKey.TryGetValue(rule.SecondFieldKey, out var second) ||
                workspace.Session.GetField(first.ParameterName) is not { } firstField ||
                workspace.Session.GetField(second.ParameterName) is not { } secondField)
            {
                continue;
            }

            var invalid = rule.Kind switch
            {
                BasicTuningRuleKind.LessThanOrEqual => firstField.PendingValue > secondField.PendingValue,
                BasicTuningRuleKind.PositiveCompanion => firstField.PendingValue > 0 && secondField.PendingValue <= 0,
                var _ => throw new ArgumentOutOfRangeException(nameof(rule.Kind), rule.Kind, "Unknown Basic Tuning rule kind.")
            };
            if (invalid)
            {
                issues.Add(new BasicTuningValidationIssue(
                    groupKey,
                    [first.ParameterName, second.ParameterName],
                    rule.Message));
            }
        }

        return issues;
    }

    /// <inheritdoc />
    public async Task<BasicTuningApplyResult> ApplyGroupAsync(
        BasicTuningWorkspace workspace,
        string groupKey,
        CancellationToken cancellationToken = default)
    {
        var group = GetGroup(workspace, groupKey);
        var issues = ValidateGroup(workspace, groupKey);
        if (issues.Count > 0)
        {
            logger.LogWarning("Basic Tuning group {GroupKey} for {VehicleId} failed validation with {IssueCount} issues.",
                groupKey,
                workspace.Session.VehicleId,
                issues.Count);
            return new BasicTuningApplyResult(false, issues, null);
        }

        var report = await workspace.Session.ApplyAsync(
            group.Fields.Select(field => field.ParameterName).ToArray(),
            cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Basic Tuning group {GroupKey} apply for {VehicleId} completed with success {Success}.",
            groupKey,
            workspace.Session.VehicleId,
            report.Success);
        return new BasicTuningApplyResult(report.Success, [], report);
    }

    /// <inheritdoc />
    public void RevertGroup(BasicTuningWorkspace workspace, string groupKey)
    {
        var group = GetGroup(workspace, groupKey);
        foreach (var field in group.Fields)
        {
            workspace.Session.Revert(field.ParameterName);
        }
    }

    /// <inheritdoc />
    public string Export(BasicTuningWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var values = workspace.PresentedParameterNames
            .Select(name => workspace.Session.GetField(name))
            .Where(field => field is not null)
            .ToDictionary(field => field!.Name, field => field!.PendingValue, StringComparer.Ordinal);
        var document = new BasicTuningDocument(1, workspace.Profile.Family, values);
        return JsonSerializer.Serialize(document, jsonOptions);
    }

    /// <inheritdoc />
    public BasicTuningImportResult Import(BasicTuningWorkspace workspace, string json)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure("The Basic Tuning import is empty.");
        }

        BasicTuningDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<BasicTuningDocument>(json, jsonOptions);
        }
        catch (JsonException exception)
        {
            return Failure($"The Basic Tuning import is not valid JSON: {exception.Message}");
        }

        if (document is null || document.Version != 1 || document.Values is null)
        {
            return Failure("The Basic Tuning import uses an unsupported or incomplete format.");
        }

        if (document.Family != workspace.Profile.Family)
        {
            return Failure($"The import targets {document.Family}, not {workspace.Profile.Family}.");
        }

        var presented = workspace.PresentedParameterNames.ToHashSet(StringComparer.Ordinal);
        var imported = document.Values.Where(item => presented.Contains(item.Key)).ToArray();
        var ignored = document.Values.Keys.Where(name => !presented.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (imported.Any(item => !double.IsFinite(item.Value)))
        {
            return new BasicTuningImportResult(false, 0, ignored, ["Imported values must be finite numbers."]);
        }

        var previous = imported.ToDictionary(
            item => item.Key,
            item => workspace.Session.GetField(item.Key)!.PendingValue,
            StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var item in imported)
        {
            if (!workspace.Session.TrySetPending(item.Key, item.Value, out var error) && error is not null)
            {
                errors.Add($"{item.Key}: {error}");
            }
        }

        if (errors.Count == 0)
        {
            errors.AddRange(workspace.Groups
                .SelectMany(group => ValidateGroup(workspace, group.Definition.Key))
                .Select(issue => issue.Message)
                .Distinct(StringComparer.Ordinal));
        }

        if (errors.Count > 0)
        {
            foreach (var item in previous)
            {
                workspace.Session.TrySetPending(item.Key, item.Value, out var _);
            }

            return new BasicTuningImportResult(false, 0, ignored, errors);
        }

        logger.LogInformation("Imported {Count} Basic Tuning values for {VehicleId}; ignored {IgnoredCount} names.",
            imported.Length,
            workspace.Session.VehicleId,
            ignored.Length);
        return new BasicTuningImportResult(true, imported.Length, ignored, []);
    }

    private static ResolvedBasicTuningGroup GetGroup(BasicTuningWorkspace workspace, string groupKey)
    {
        return workspace.Groups.FirstOrDefault(group => string.Equals(group.Definition.Key, groupKey, StringComparison.Ordinal))
               ?? throw new ArgumentException($"Basic Tuning group '{groupKey}' is not present in this workspace.", nameof(groupKey));
    }

    private static BasicTuningImportResult Failure(string message)
    {
        return new BasicTuningImportResult(false, 0, [], [message]);
    }

    private sealed record BasicTuningDocument(
        int Version,
        FirmwareFamily Family,
        Dictionary<string, double> Values);
}
