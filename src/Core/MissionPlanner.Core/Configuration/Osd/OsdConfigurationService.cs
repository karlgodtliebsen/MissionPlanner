using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Osd;

/// <summary>Discovers parameter-pattern OSD layouts and coordinates validated shared-session writes.</summary>
public sealed partial class OsdConfigurationService(
    IParameterEditSessionFactory sessionFactory,
    IVehicleParameterRegistry parameterRegistry,
    ILogger<OsdConfigurationService> logger) : IOsdConfigurationService
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <inheritdoc />
    public async Task<OsdConfigurationWorkspace?> OpenAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var live = parameterRegistry.GetAllParameters(vehicleId);
        var osdNames = live.Keys
            .Where(name => name.StartsWith("OSD", StringComparison.Ordinal) && name.Length > 3)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (osdNames.Length == 0)
        {
            return null;
        }

        var session = sessionFactory.Create(vehicleId);
        await session.LoadAsync(osdNames, cancellationToken).ConfigureAwait(false);
        var numbered = osdNames
            .Select(name => (Name: name, Match: ScreenParameterPattern().Match(name)))
            .Where(item => item.Match.Success)
            .Select(item => (item.Name, Screen: int.Parse(item.Match.Groups["screen"].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .GroupBy(item => item.Screen)
            .OrderBy(group => group.Key)
            .ToArray();
        var screens = numbered
            .Select(group => DiscoverScreen(group.Key, group.Select(item => item.Name).ToArray(), session))
            .Where(screen => screen.Items.Count > 0 || screen.ScreenParameterNames.Count > 0)
            .ToArray();
        if (screens.Length == 0)
        {
            return null;
        }

        var numberedNames = numbered.SelectMany(group => group.Select(item => item.Name)).ToHashSet(StringComparer.Ordinal);
        var globals = osdNames.Where(name => !numberedNames.Contains(name)).ToArray();
        logger.LogInformation(
            "Discovered {ScreenCount} OSD screens and {ItemCount} items for {VehicleId}.",
            screens.Length,
            screens.Sum(screen => screen.Items.Count),
            vehicleId);
        return new OsdConfigurationWorkspace(session.Scope, session, globals, screens);
    }

    /// <inheritdoc />
    public IReadOnlyList<OsdValidationIssue> ValidateScreen(OsdConfigurationWorkspace workspace, int screenNumber)
    {
        var screen = GetScreen(workspace, screenNumber);
        var issues = new List<OsdValidationIssue>();
        var enabledPositions = new List<(OsdItemDefinition Item, int Column, int Row)>();
        foreach (var item in screen.Items)
        {
            var enabled = item.EnableParameterName is null || workspace.Session.GetField(item.EnableParameterName)?.PendingValue > 0.5;
            if (!enabled)
            {
                continue;
            }

            var columnValue = workspace.Session.GetField(item.ColumnParameterName)?.PendingValue;
            var rowValue = workspace.Session.GetField(item.RowParameterName)?.PendingValue;
            if (columnValue is null || rowValue is null || !IsInteger(columnValue.Value) || !IsInteger(rowValue.Value))
            {
                issues.Add(new OsdValidationIssue(
                    OsdValidationSeverity.Error,
                    screenNumber,
                    [item.Key],
                    $"{item.Title} requires whole-number row and column coordinates."));
                continue;
            }

            var column = checked((int)Math.Round(columnValue.Value));
            var row = checked((int)Math.Round(rowValue.Value));
            if (column < 0 || column >= screen.GridWidth || row < 0 || row >= screen.GridHeight)
            {
                issues.Add(new OsdValidationIssue(
                    OsdValidationSeverity.Error,
                    screenNumber,
                    [item.Key],
                    $"{item.Title} at ({column}, {row}) is outside the {screen.GridWidth}×{screen.GridHeight} character grid."));
                continue;
            }

            enabledPositions.Add((item, column, row));
        }

        foreach (var overlap in enabledPositions.GroupBy(item => (item.Column, item.Row)).Where(group => group.Count() > 1))
        {
            var items = overlap.Select(value => value.Item).ToArray();
            issues.Add(new OsdValidationIssue(
                screen.SupportsDynamicOverlaps ? OsdValidationSeverity.Warning : OsdValidationSeverity.Error,
                screenNumber,
                items.Select(item => item.Key).ToArray(),
                $"{string.Join(" and ", items.Select(item => item.Title))} overlap at ({overlap.Key.Column}, {overlap.Key.Row})." +
                (screen.SupportsDynamicOverlaps ? " This firmware advertises dynamic items; explicit acknowledgement is required." : string.Empty)));
        }

        return issues;
    }

    /// <inheritdoc />
    public string? MoveItem(
        OsdConfigurationWorkspace workspace,
        int screenNumber,
        string itemKey,
        int column,
        int row)
    {
        var screen = GetScreen(workspace, screenNumber);
        var item = screen.Items.FirstOrDefault(value => string.Equals(value.Key, itemKey, StringComparison.Ordinal));
        if (item is null)
        {
            return $"OSD item '{itemKey}' was not discovered on screen {screenNumber}.";
        }

        if (column < 0 || column >= screen.GridWidth || row < 0 || row >= screen.GridHeight)
        {
            return $"Position ({column}, {row}) is outside the {screen.GridWidth}×{screen.GridHeight} character grid.";
        }

        var oldColumn = workspace.Session.GetField(item.ColumnParameterName)!.PendingValue;
        var oldRow = workspace.Session.GetField(item.RowParameterName)!.PendingValue;
        var columnAccepted = workspace.Session.TrySetPending(item.ColumnParameterName, column, out var columnError);
        var rowAccepted = workspace.Session.TrySetPending(item.RowParameterName, row, out var rowError);
        if (!columnAccepted || !rowAccepted)
        {
            workspace.Session.TrySetPending(item.ColumnParameterName, oldColumn, out _);
            workspace.Session.TrySetPending(item.RowParameterName, oldRow, out _);
            return columnError ?? rowError ?? "The OSD position was rejected by parameter metadata.";
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<OsdApplyResult> ApplyScreenAsync(
        OsdConfigurationWorkspace workspace,
        int screenNumber,
        bool allowDynamicOverlapWarnings,
        CancellationToken cancellationToken = default)
    {
        var screen = GetScreen(workspace, screenNumber);
        var issues = ValidateScreen(workspace, screenNumber);
        if (issues.Any(issue => issue.Severity == OsdValidationSeverity.Error) ||
            !allowDynamicOverlapWarnings && issues.Any(issue => issue.Severity == OsdValidationSeverity.Warning))
        {
            return new OsdApplyResult(false, issues, null);
        }

        var names = ScreenParameterNames(screen);
        var report = await workspace.Session.ApplyAsync(names, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "OSD screen {ScreenNumber} apply for {VehicleId} completed with success {Success}.",
            screenNumber,
            workspace.Session.VehicleId,
            report.Success);
        return new OsdApplyResult(report.Success, issues, report);
    }

    /// <inheritdoc />
    public void ResetScreen(OsdConfigurationWorkspace workspace, int screenNumber)
    {
        foreach (var name in ScreenParameterNames(GetScreen(workspace, screenNumber)))
        {
            workspace.Session.Revert(name);
        }
    }

    /// <inheritdoc />
    public string Export(OsdConfigurationWorkspace workspace)
    {
        var names = workspace.GlobalParameterNames
            .Concat(workspace.Screens.SelectMany(ScreenParameterNames))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var values = names
            .Select(workspace.Session.GetField)
            .Where(state => state is not null)
            .ToDictionary(state => state!.Name, state => state!.PendingValue, StringComparer.Ordinal);
        return JsonSerializer.Serialize(
            new OsdLayoutDocument(1, workspace.Scope.FirmwareIdentity.Family, values),
            jsonOptions);
    }

    /// <inheritdoc />
    public OsdImportResult Import(OsdConfigurationWorkspace workspace, string json)
    {
        OsdLayoutDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<OsdLayoutDocument>(json, jsonOptions);
        }
        catch (JsonException exception)
        {
            return Failure($"The OSD layout is not valid JSON: {exception.Message}");
        }

        if (document is null || document.Version != 1 || document.Values is null)
        {
            return Failure("The OSD layout uses an unsupported or incomplete format.");
        }

        if (document.Family != workspace.Scope.FirmwareIdentity.Family)
        {
            return Failure($"The layout targets {document.Family}, not {workspace.Scope.FirmwareIdentity.Family}.");
        }

        var discovered = workspace.GlobalParameterNames
            .Concat(workspace.Screens.SelectMany(ScreenParameterNames))
            .ToHashSet(StringComparer.Ordinal);
        var imported = document.Values.Where(item => discovered.Contains(item.Key)).ToArray();
        var ignored = document.Values.Keys.Where(name => !discovered.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        if (imported.Any(item => !double.IsFinite(item.Value)))
        {
            return new OsdImportResult(false, 0, ignored, [], ["OSD values must be finite numbers."]);
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

        var issues = workspace.Screens.SelectMany(screen => ValidateScreen(workspace, screen.Number)).ToArray();
        if (errors.Count > 0 || issues.Any(issue => issue.Severity == OsdValidationSeverity.Error))
        {
            foreach (var item in previous)
            {
                workspace.Session.TrySetPending(item.Key, item.Value, out _);
            }

            return new OsdImportResult(false, 0, ignored, issues, errors);
        }

        return new OsdImportResult(true, imported.Length, ignored, issues, []);
    }

    /// <inheritdoc />
    public IReadOnlyList<OsdPreviewItem> GetPreviewItems(OsdConfigurationWorkspace workspace, int screenNumber) =>
        GetScreen(workspace, screenNumber).Items.Select(item => new OsdPreviewItem(
            item.Key,
            PreviewTitle(item.Title),
            (int)Math.Round(workspace.Session.GetField(item.ColumnParameterName)!.PendingValue),
            (int)Math.Round(workspace.Session.GetField(item.RowParameterName)!.PendingValue),
            item.EnableParameterName is null || workspace.Session.GetField(item.EnableParameterName)?.PendingValue > 0.5)).ToArray();

    private static OsdScreenDefinition DiscoverScreen(
        int screenNumber,
        IReadOnlyList<string> names,
        IParameterEditSession session)
    {
        var itemProperties = names
            .Select(name => (Name: name, Match: ItemParameterPattern().Match(name)))
            .Where(item => item.Match.Success && int.Parse(item.Match.Groups["screen"].Value, System.Globalization.CultureInfo.InvariantCulture) == screenNumber)
            .GroupBy(item => item.Match.Groups["item"].Value, StringComparer.Ordinal)
            .ToArray();
        var items = new List<OsdItemDefinition>();
        var itemOwnedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in itemProperties)
        {
            var properties = group.ToDictionary(item => item.Match.Groups["property"].Value, item => item.Name, StringComparer.Ordinal);
            if (!properties.TryGetValue("X", out var column) || !properties.TryGetValue("Y", out var row))
            {
                continue;
            }

            properties.TryGetValue("EN", out var enable);
            var prefix = $"OSD{screenNumber}_{group.Key}_";
            var additional = names.Where(name =>
                    name.StartsWith(prefix, StringComparison.Ordinal) &&
                    !string.Equals(name, column, StringComparison.Ordinal) &&
                    !string.Equals(name, row, StringComparison.Ordinal) &&
                    !string.Equals(name, enable, StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            itemOwnedNames.UnionWith(properties.Values);
            itemOwnedNames.UnionWith(additional);
            var metadata = enable is not null ? session.GetField(enable)?.Metadata : session.GetField(column)?.Metadata;
            var title = string.IsNullOrWhiteSpace(metadata?.DisplayName)
                ? ToTitle(group.Key)
                : metadata.DisplayName!;
            items.Add(new OsdItemDefinition(
                screenNumber,
                group.Key,
                title,
                enable,
                column,
                row,
                additional,
                metadata?.Description ?? $"Firmware-discovered OSD item {ToTitle(group.Key)}."));
        }

        var screenParameters = names.Where(name => !itemOwnedNames.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var xMax = items.Select(item => session.GetField(item.ColumnParameterName)?.Metadata.Maximum).Where(value => value is not null).DefaultIfEmpty(29).Max() ?? 29;
        var yMax = items.Select(item => session.GetField(item.RowParameterName)?.Metadata.Maximum).Where(value => value is not null).DefaultIfEmpty(15).Max() ?? 15;
        var width = Math.Clamp((int)Math.Floor(xMax) + 1, 1, 120);
        var height = Math.Clamp((int)Math.Floor(yMax) + 1, 1, 60);
        var supportsDynamic = screenParameters
            .Select(session.GetField)
            .Where(state => state is not null)
            .Any(state =>
                state!.Metadata.Bitmask.Any(option => ContainsDynamic(option.Label)) ||
                ContainsDynamic(state.Metadata.Description));
        return new OsdScreenDefinition(
            screenNumber,
            $"Screen {screenNumber}",
            width,
            height,
            supportsDynamic,
            screenParameters,
            items.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IReadOnlyList<string> ScreenParameterNames(OsdScreenDefinition screen) => screen.ScreenParameterNames
        .Concat(screen.Items.SelectMany(item =>
            item.AdditionalParameterNames
                .Append(item.ColumnParameterName)
                .Append(item.RowParameterName)
                .Concat(item.EnableParameterName is null ? [] : [item.EnableParameterName])))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static OsdScreenDefinition GetScreen(OsdConfigurationWorkspace workspace, int screenNumber) =>
        workspace.Screens.FirstOrDefault(screen => screen.Number == screenNumber)
        ?? throw new ArgumentException($"OSD screen {screenNumber} was not discovered.", nameof(screenNumber));

    private static bool ContainsDynamic(string? value) =>
        value?.Contains("dynamic", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Contains("overlap", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsInteger(double value) => Math.Abs(value - Math.Round(value)) < 0.0001;

    private static string ToTitle(string key) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' ').ToLowerInvariant());

    private static string PreviewTitle(string title) => title.Length <= 12 ? title : title[..12];

    private static OsdImportResult Failure(string error) => new(false, 0, [], [], [error]);

    [GeneratedRegex("^OSD(?<screen>[0-9]+)_", RegexOptions.CultureInvariant)]
    private static partial Regex ScreenParameterPattern();

    [GeneratedRegex("^OSD(?<screen>[0-9]+)_(?<item>.+)_(?<property>EN|X|Y)$", RegexOptions.CultureInvariant)]
    private static partial Regex ItemParameterPattern();

    private sealed record OsdLayoutDocument(
        int Version,
        FirmwareFamily Family,
        Dictionary<string, double> Values);
}
