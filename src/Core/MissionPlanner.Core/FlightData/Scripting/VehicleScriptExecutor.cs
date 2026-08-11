using MissionPlanner.Core.Commands;
using MissionPlanner.Core.FlightData.Auxiliary;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Generated;

namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Sequential constrained script engine.</summary>
public sealed class VehicleScriptExecutor(
    IVehicleScriptValidator validator,
    IActiveVehicleContext active,
    IVehicleCommandService commands,
    IAuxiliaryFunctionCatalog auxiliaryCatalog,
    IAuxiliaryFunctionService auxiliary,
    IReplaySessionManager? replay = null)
    : IVehicleScriptExecutor
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<VehicleScriptStepResult>> ExecuteAsync(VehicleScriptDocument document, bool dryRun,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(document);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        }

        var results = new List<VehicleScriptStepResult>(document.Steps.Count);
        for (var index = 0; index < document.Steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = document.Steps[index];
            if (dryRun)
            {
                results.Add(new VehicleScriptStepResult(index, step.Action, true, "Validated; no action executed.", DateTimeOffset.UtcNow));
                continue;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, active.ConnectionCancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds));
            var result = await ExecuteStepAsync(step, timeout.Token).ConfigureAwait(false);
            results.Add(new VehicleScriptStepResult(index, step.Action, result.Success, result.Message, DateTimeOffset.UtcNow));
            if (!result.Success)
            {
                break;
            }
        }

        return results;
    }

    private async Task<(bool Success, string Message)> ExecuteStepAsync(VehicleScriptStep step, CancellationToken token)
    {
        if (step.Action.Equals("notify", StringComparison.OrdinalIgnoreCase))
        {
            return (true, step.Arguments.GetValueOrDefault("message", "Script notification"));
        }

        if (step.Action.Equals("delay", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(int.Parse(step.Arguments["milliseconds"], System.Globalization.CultureInfo.InvariantCulture), token);
            return (true, "Delay completed.");
        }

        if (step.Action.Equals("waitForConnection", StringComparison.OrdinalIgnoreCase))
        {
            while (!active.IsOnline)
            {
                await Task.Delay(100, token);
            }

            return (true, "Vehicle is online.");
        }

        if (replay is not null && replay.Snapshot.State != ReplaySessionState.Unloaded)
        {
            return (false, "Vehicle-changing script steps are blocked during replay.");
        }

        if (active.State is not { } state)
        {
            return (false, "No active vehicle.");
        }

        VehicleCommandResponse response;
        if (step.Action.Equals("arm", StringComparison.OrdinalIgnoreCase))
        {
            response = await commands.ArmAsync(state.VehicleId, token);
        }
        else if (step.Action.Equals("disarm", StringComparison.OrdinalIgnoreCase))
        {
            response = await commands.DisarmAsync(state.VehicleId, true, token);
        }
        else if (step.Action.Equals("land", StringComparison.OrdinalIgnoreCase))
        {
            response = await commands.LandAsync(state.VehicleId, token);
        }
        else if (step.Action.Equals("rtl", StringComparison.OrdinalIgnoreCase))
        {
            response = await commands.ReturnToLaunchAsync(state.VehicleId, token);
        }
        else if (step.Action.Equals("hold", StringComparison.OrdinalIgnoreCase))
        {
            response = await commands.HoldAsync(state.VehicleId, token);
        }
        else if (step.Action.Equals("auxFunction", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(step.Arguments.GetValueOrDefault("id"), out var id))
            {
                return (false, "Auxiliary function ID is required.");
            }

            var descriptor = auxiliaryCatalog.GetFunctions(state).FirstOrDefault(item => item.Id == id) ?? auxiliaryCatalog.DescribeUnknown(id);
            var result = await auxiliary.ExecuteAsync(new AuxiliaryFunctionRequest(state, descriptor, MavCmdDoAuxFunctionSwitchLevel.High, true), token);
            return (result.IsAccepted, result.Summary);
        }
        else
        {
            return (false, $"Action '{step.Action}' is unavailable.");
        }

        return (response.Result == VehicleCommandResult.Accepted, response.Message ?? response.Result.ToString());
    }
}
