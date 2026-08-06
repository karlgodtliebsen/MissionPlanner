using System.Text.Json;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.FlightData.Auxiliary;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Generated;

namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>A versioned, declarative vehicle automation document.</summary>
public sealed record VehicleScriptDocument(int Version, string Name, IReadOnlyList<VehicleScriptStep> Steps);

/// <summary>An allow-listed action and its bounded string arguments.</summary>
public sealed record VehicleScriptStep(string Action, IReadOnlyDictionary<string, string> Arguments, int TimeoutSeconds = 15);

/// <summary>Reports complete-document validation.</summary>
public sealed record VehicleScriptValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>Describes script execution progress.</summary>
public enum VehicleScriptExecutionState { Validating, DryRun, Running, Succeeded, Failed, Cancelled }

/// <summary>Records one ordered script-step result.</summary>
public sealed record VehicleScriptStepResult(int Index, string Action, bool Succeeded, string Message, DateTimeOffset CompletedAt);

/// <summary>Parses the safe JSON script format.</summary>
public interface IVehicleScriptParser
{
    /// <summary>Parses a document or throws <see cref="JsonException"/> for invalid JSON.</summary>
    VehicleScriptDocument Parse(string json);
}

/// <summary>Validates an entire script before execution.</summary>
public interface IVehicleScriptValidator
{
    /// <summary>Validates schema, limits, and allow-listed actions.</summary>
    VehicleScriptValidationResult Validate(VehicleScriptDocument document);
}

/// <summary>Lists the only actions scripts may invoke.</summary>
public interface IVehicleScriptActionRegistry
{
    /// <summary>Gets the stable allow-listed action names.</summary>
    IReadOnlySet<string> Actions { get; }
}

/// <summary>Executes validated scripts sequentially through typed services.</summary>
public interface IVehicleScriptExecutor
{
    /// <summary>Dry-runs or executes a script, producing an ordered complete log.</summary>
    Task<IReadOnlyList<VehicleScriptStepResult>> ExecuteAsync(VehicleScriptDocument document, bool dryRun,
        CancellationToken cancellationToken);
}

/// <summary>System.Text.Json parser for version 1 scripts.</summary>
public sealed class VehicleScriptParser : IVehicleScriptParser
{
    private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    /// <inheritdoc />
    public VehicleScriptDocument Parse(string json) => JsonSerializer.Deserialize<VehicleScriptDocument>(json, options)
        ?? throw new JsonException("The script document is empty.");
}

/// <summary>Reviewed action registry with no arbitrary command or code escape hatch.</summary>
public sealed class VehicleScriptActionRegistry : IVehicleScriptActionRegistry
{
    /// <inheritdoc />
    public IReadOnlySet<string> Actions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "notify", "delay", "waitForConnection", "arm", "disarm", "land", "rtl", "hold", "auxFunction" };
}

/// <summary>Applies strict schema and bounded-execution rules.</summary>
public sealed class VehicleScriptValidator(IVehicleScriptActionRegistry registry) : IVehicleScriptValidator
{
    /// <inheritdoc />
    public VehicleScriptValidationResult Validate(VehicleScriptDocument document)
    {
        var errors = new List<string>();
        if (document.Version != 1) errors.Add($"Unsupported script version {document.Version}; expected 1.");
        if (string.IsNullOrWhiteSpace(document.Name)) errors.Add("Script name is required.");
        if (document.Steps.Count is 0 or > 100) errors.Add("A script must contain 1 to 100 steps.");
        for (var index = 0; index < document.Steps.Count; index++)
        {
            var step = document.Steps[index];
            if (!registry.Actions.Contains(step.Action)) errors.Add($"Step {index + 1}: action '{step.Action}' is not allowed.");
            if (step.TimeoutSeconds is < 1 or > 300) errors.Add($"Step {index + 1}: timeout must be 1 to 300 seconds.");
            if (step.Action.Equals("delay", StringComparison.OrdinalIgnoreCase) &&
                (!step.Arguments.TryGetValue("milliseconds", out var delay) || !int.TryParse(delay, out var milliseconds) || milliseconds is < 0 or > 60000))
                errors.Add($"Step {index + 1}: delay milliseconds must be 0 to 60000.");
        }
        return new(errors.Count == 0, errors);
    }
}

/// <summary>Sequential constrained script engine.</summary>
public sealed class VehicleScriptExecutor(IVehicleScriptValidator validator, IActiveVehicleContext active,
    IVehicleCommandService commands, IAuxiliaryFunctionCatalog auxiliaryCatalog, IAuxiliaryFunctionService auxiliary)
    : IVehicleScriptExecutor
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<VehicleScriptStepResult>> ExecuteAsync(VehicleScriptDocument document, bool dryRun,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(document);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        var results = new List<VehicleScriptStepResult>(document.Steps.Count);
        for (var index = 0; index < document.Steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = document.Steps[index];
            if (dryRun) { results.Add(new(index, step.Action, true, "Validated; no action executed.", DateTimeOffset.UtcNow)); continue; }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, active.ConnectionCancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds));
            var result = await ExecuteStepAsync(step, timeout.Token).ConfigureAwait(false);
            results.Add(new(index, step.Action, result.Success, result.Message, DateTimeOffset.UtcNow));
            if (!result.Success) break;
        }
        return results;
    }

    private async Task<(bool Success, string Message)> ExecuteStepAsync(VehicleScriptStep step, CancellationToken token)
    {
        if (step.Action.Equals("notify", StringComparison.OrdinalIgnoreCase))
            return (true, step.Arguments.GetValueOrDefault("message", "Script notification"));
        if (step.Action.Equals("delay", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(int.Parse(step.Arguments["milliseconds"], System.Globalization.CultureInfo.InvariantCulture), token);
            return (true, "Delay completed.");
        }
        if (step.Action.Equals("waitForConnection", StringComparison.OrdinalIgnoreCase))
        {
            while (!active.IsOnline) await Task.Delay(100, token);
            return (true, "Vehicle is online.");
        }
        if (active.State is not { } state) return (false, "No active vehicle.");
        VehicleCommandResponse response;
        if (step.Action.Equals("arm", StringComparison.OrdinalIgnoreCase)) response = await commands.ArmAsync(state.VehicleId, token);
        else if (step.Action.Equals("disarm", StringComparison.OrdinalIgnoreCase)) response = await commands.DisarmAsync(state.VehicleId, true, token);
        else if (step.Action.Equals("land", StringComparison.OrdinalIgnoreCase)) response = await commands.LandAsync(state.VehicleId, token);
        else if (step.Action.Equals("rtl", StringComparison.OrdinalIgnoreCase)) response = await commands.ReturnToLaunchAsync(state.VehicleId, token);
        else if (step.Action.Equals("hold", StringComparison.OrdinalIgnoreCase)) response = await commands.HoldAsync(state.VehicleId, token);
        else if (step.Action.Equals("auxFunction", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(step.Arguments.GetValueOrDefault("id"), out var id)) return (false, "Auxiliary function ID is required.");
            var descriptor = auxiliaryCatalog.GetFunctions(state).FirstOrDefault(item => item.Id == id) ?? auxiliaryCatalog.DescribeUnknown(id);
            var result = await auxiliary.ExecuteAsync(new(state, descriptor, MavCmdDoAuxFunctionSwitchLevel.High, true), token);
            return (result.IsAccepted, result.Summary);
        }
        else return (false, $"Action '{step.Action}' is unavailable.");
        return (response.Result == VehicleCommandResult.Accepted, response.Message ?? response.Result.ToString());
    }
}
