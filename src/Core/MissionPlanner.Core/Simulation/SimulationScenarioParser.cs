using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Core.Simulation;

/// <summary>Parses the closed, versioned JSON scenario schema without code execution.</summary>
public sealed partial class SimulationScenarioParser(IOptions<SimulationScenarioOptions> options) : ISimulationScenarioParser
{
    private static readonly JsonSerializerOptions jsonOptions = CreateOptions();
    private readonly SimulationScenarioOptions options = options.Value;

    /// <inheritdoc />
    public SimulationScenarioDocument Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (Encoding.UTF8.GetByteCount(json) > Math.Clamp(options.MaximumDocumentBytes, 1024, 16 * 1024 * 1024))
        {
            throw new InvalidDataException("The scenario document exceeds the configured size limit.");
        }

        SimulationScenarioDocument document;
        try
        {
            document = JsonSerializer.Deserialize<SimulationScenarioDocument>(json, jsonOptions) ??
                throw new InvalidDataException("The scenario document was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid simulation scenario JSON: {exception.Message}", exception);
        }

        var issues = Validate(document);
        if (issues.Any(item => item.Severity == SimulationScenarioValidationSeverity.Error))
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, issues.Select(item => $"{item.Path}: {item.Message}")));
        }

        return document;
    }

    /// <inheritdoc />
    public IReadOnlyList<SimulationScenarioValidationIssue> Validate(SimulationScenarioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<SimulationScenarioValidationIssue>();
        if (document.SchemaVersion != 1)
        {
            Error(issues, "schemaVersion", $"Unsupported schema version {document.SchemaVersion}; expected 1.");
        }

        if (document.Id == Guid.Empty)
        {
            Error(issues, "id", "A non-empty scenario ID is required.");
        }

        if (string.IsNullOrWhiteSpace(document.Name) || document.Name.Length > 200)
        {
            Error(issues, "name", "A scenario name of 1–200 characters is required.");
        }

        if (document.Variables is null)
        {
            Error(issues, "variables", "The variables object is required; use an empty object when unused.");
        }
        else
        {
            foreach (var variable in document.Variables)
            {
                if (!VariableName().IsMatch(variable.Key))
                {
                    Error(issues, $"variables.{variable.Key}", "Variable names must use 1–64 letters, digits, underscores, or hyphens.");
                }

                ValidateValue(variable.Value, $"variables.{variable.Key}", document.Variables, allowReference: false, issues);
            }
        }

        if (document.Steps is null || document.Steps.Count == 0)
        {
            Error(issues, "steps", "At least one step is required.");
            return issues;
        }

        if (document.Steps.Count > Math.Clamp(options.MaximumSteps, 1, 10000))
        {
            Error(issues, "steps", "The scenario exceeds the configured step limit.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < document.Steps.Count; index++)
        {
            ValidateStep(document.Steps[index], index, document.Variables ?? new Dictionary<string, SimulationScenarioValue>(), ids, issues);
        }

        return issues;
    }

    /// <inheritdoc />
    public string Serialize(SimulationScenarioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, jsonOptions);
    }

    private static void ValidateStep(
        SimulationScenarioStep step,
        int index,
        IReadOnlyDictionary<string, SimulationScenarioValue> variables,
        ISet<string> ids,
        ICollection<SimulationScenarioValidationIssue> issues)
    {
        var path = $"steps[{index}]";
        if (string.IsNullOrWhiteSpace(step.Id) || step.Id.Length > 100 || !ids.Add(step.Id))
        {
            Error(issues, $"{path}.id", "A unique step ID of 1–100 characters is required.");
        }

        if (string.IsNullOrWhiteSpace(step.Name) || step.Name.Length > 200)
        {
            Error(issues, $"{path}.name", "A step name of 1–200 characters is required.");
        }

        if (step.TimeoutSeconds is < 1 or > 3600)
        {
            Error(issues, $"{path}.timeoutSeconds", "Every step requires an explicit timeout from 1 to 3600 seconds.");
        }

        switch (step.Kind)
        {
            case SimulationScenarioStepKind.WaitForState when step.State is null:
                Error(issues, $"{path}.state", "WaitForState requires a named state.");
                break;
            case SimulationScenarioStepKind.SetMode when string.IsNullOrWhiteSpace(step.Mode):
                Error(issues, $"{path}.mode", "SetMode requires a firmware mode name.");
                break;
            case SimulationScenarioStepKind.Takeoff:
                ValidateValue(step.Value, $"{path}.value", variables, allowReference: true, issues, SimulationScenarioValueKind.Number);
                break;
            case SimulationScenarioStepKind.UploadMission:
                ValidateMission(step.MissionItems, path, issues);
                break;
            case SimulationScenarioStepKind.WaitCondition or SimulationScenarioStepKind.AssertTelemetry:
                ValidateCondition(step.Condition, path, variables, issues);
                break;
            case SimulationScenarioStepKind.InjectFault:
                if (string.IsNullOrWhiteSpace(step.ControlKey))
                {
                    Error(issues, $"{path}.controlKey", "InjectFault requires a documented control key.");
                }

                ValidateValue(step.Value, $"{path}.value", variables, allowReference: true, issues, SimulationScenarioValueKind.Number);
                if (step.DurationSeconds is < 1 or > 3600)
                {
                    Error(issues, $"{path}.durationSeconds", "InjectFault requires a duration from 1 to 3600 seconds.");
                }
                break;
            case SimulationScenarioStepKind.ClearFault when string.IsNullOrWhiteSpace(step.ControlKey):
                Error(issues, $"{path}.controlKey", "ClearFault requires a documented control key.");
                break;
        }
    }

    private static void ValidateMission(
        IReadOnlyList<SimulationScenarioMissionItem>? items,
        string path,
        ICollection<SimulationScenarioValidationIssue> issues)
    {
        if (items is null || items.Count == 0)
        {
            Error(issues, $"{path}.missionItems", "UploadMission requires at least one typed mission item.");
            return;
        }

        if (items.Count > ushort.MaxValue)
        {
            Error(issues, $"{path}.missionItems", "Mission item count exceeds the MAVLink protocol limit.");
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Command == 0 || !float.IsFinite(item.Param1) || !float.IsFinite(item.Param2) ||
                !float.IsFinite(item.Param3) || !float.IsFinite(item.Param4) || !float.IsFinite(item.Z))
            {
                Error(issues, $"{path}.missionItems[{index}]", "Mission command and all floating-point values must be finite and non-zero where required.");
            }
        }
    }

    private static void ValidateCondition(
        SimulationTelemetryCondition? condition,
        string path,
        IReadOnlyDictionary<string, SimulationScenarioValue> variables,
        ICollection<SimulationScenarioValidationIssue> issues)
    {
        if (condition is null)
        {
            Error(issues, $"{path}.condition", "A typed telemetry condition is required.");
            return;
        }

        var expectedKind = condition.Metric switch
        {
            SimulationTelemetryMetric.Online or SimulationTelemetryMetric.Armed => SimulationScenarioValueKind.Boolean,
            SimulationTelemetryMetric.Mode or SimulationTelemetryMetric.LandedState or SimulationTelemetryMetric.GpsFixType => SimulationScenarioValueKind.Text,
            _ => SimulationScenarioValueKind.Number
        };
        ValidateValue(condition.Expected, $"{path}.condition.expected", variables, allowReference: true, issues, expectedKind);
        if (expectedKind != SimulationScenarioValueKind.Number && condition.Operator is not (SimulationComparisonOperator.Equal or SimulationComparisonOperator.NotEqual))
        {
            Error(issues, $"{path}.condition.operator", "Boolean and text telemetry support only Equal or NotEqual.");
        }

        if (condition.Tolerance is < 0 || condition.Tolerance is { } tolerance && !double.IsFinite(tolerance))
        {
            Error(issues, $"{path}.condition.tolerance", "Tolerance must be a finite non-negative number.");
        }
    }

    private static void ValidateValue(
        SimulationScenarioValue? value,
        string path,
        IReadOnlyDictionary<string, SimulationScenarioValue> variables,
        bool allowReference,
        ICollection<SimulationScenarioValidationIssue> issues,
        SimulationScenarioValueKind? requiredKind = null)
    {
        if (value is null)
        {
            Error(issues, path, "A typed value is required.");
            return;
        }

        if (requiredKind is { } expected && value.Kind != expected)
        {
            Error(issues, $"{path}.kind", $"Expected {expected}.");
        }

        if (!string.IsNullOrWhiteSpace(value.Variable))
        {
            if (!allowReference)
            {
                Error(issues, $"{path}.variable", "Variable declarations cannot reference other variables.");
            }
            else if (!variables.TryGetValue(value.Variable, out var declared))
            {
                Error(issues, $"{path}.variable", $"Variable '{value.Variable}' is not declared.");
            }
            else if (declared.Kind != value.Kind)
            {
                Error(issues, $"{path}.variable", $"Variable '{value.Variable}' is {declared.Kind}, not {value.Kind}.");
            }

            if (value.BooleanValue is not null || value.NumberValue is not null || value.TextValue is not null)
            {
                Error(issues, path, "A variable reference cannot also contain a literal.");
            }

            return;
        }

        var valid = value.Kind switch
        {
            SimulationScenarioValueKind.Boolean => value.BooleanValue is not null && value.NumberValue is null && value.TextValue is null,
            SimulationScenarioValueKind.Number => value.NumberValue is { } number && double.IsFinite(number) && value.BooleanValue is null && value.TextValue is null,
            SimulationScenarioValueKind.Text => !string.IsNullOrWhiteSpace(value.TextValue) && value.TextValue.Length <= 200 && value.BooleanValue is null && value.NumberValue is null,
            _ => false
        };
        if (!valid)
        {
            Error(issues, path, "The value must contain exactly one valid literal matching its kind.");
        }
    }

    private static void Error(ICollection<SimulationScenarioValidationIssue> issues, string path, string message) =>
        issues.Add(new SimulationScenarioValidationIssue(SimulationScenarioValidationSeverity.Error, path, message));

    private static JsonSerializerOptions CreateOptions()
    {
        var result = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        result.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return result;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex VariableName();
}
