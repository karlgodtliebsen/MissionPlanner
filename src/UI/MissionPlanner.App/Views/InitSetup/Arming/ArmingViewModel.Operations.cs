using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.Arming;

public sealed partial class ArmingViewModel
{
    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        if (!CanApply || review is not { } captured || lifetime is null)
        {
            return;
        }
        var version = generation;
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, captured.Connection);
        operation = scope;
        IsBusy = true;
        NotifyAvailability();
        try
        {
            var text = string.Join(Environment.NewLine, PendingChanges.Concat(captured.Warnings)) +
                (captured.RequiresReboot ? "\nReboot required after applying." : string.Empty);
            if (!await confirmation.ConfirmAsync("Apply arming configuration", text, "Apply and verify", scope.Token) ||
                scope.IsCancellationRequested || version != generation || review != captured || replay.Snapshot.IsTransmissionProhibited)
            {
                return;
            }
            var result = await configuration.ApplyAsync(captured.Scope.VehicleId, captured, scope.Token);
            if (scope.IsCancellationRequested || version != generation)
            {
                return;
            }
            RequiresReboot |= result.RequiresReboot;
            OperationMessage = result.Message + (RequiresReboot ? " Reboot required." : string.Empty);
            if (result.Actual is not null)
            {
                current = result.Actual;
            }
            if (result.Success)
            {
                Discard();
            }
            else if (result.Actual is not null)
            {
                HasPendingChanges = desired != current?.Current;
                HasConflict = result.Message.StartsWith("Conflict:", StringComparison.Ordinal);
                ProjectEditors();
                await ReviewAsync(++editVersion);
            }
            else
            {
                HasConflict = true;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (version == generation)
            {
                OperationMessage = ex.Message;
                HasConflict = true;
            }
        }
        finally
        {
            if (operation == scope)
            {
                operation = null;
            }
            if (version == generation)
            {
                IsBusy = false;
                NotifyAvailability();
                UpdateDocuments();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanArm))]
    private Task ArmAsync()
    {
        return ExecuteActionAsync(true);
    }

    [RelayCommand(CanExecute = nameof(CanDisarm))]
    private Task DisarmAsync()
    {
        return ExecuteActionAsync(false);
    }

    private async Task ExecuteActionAsync(bool arm)
    {
        var action = arm ? VehicleAction.Arm : VehicleAction.Disarm;
        if (!CanAct(action) || active.VehicleId is not { } id || lifetime is null || active.State is not { } state)
        {
            return;
        }
        var decision = policy.Evaluate(state, action);
        var identity = state.Identity.Firmware;
        var version = generation;
        var connection = active.ConnectionCancellationToken;
        using var scope = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, connection);
        operation = scope;
        IsBusy = true;
        NotifyAvailability();
        try
        {
            var confirmed = false;
            if (arm || decision.RequiresConfirmation)
            {
                confirmed = await confirmation.ConfirmAsync(arm ? "Arm vehicle?" : "Disarm vehicle?",
                    arm ? "Confirm the vehicle is safe to arm and the motor/propeller area is clear." : decision.Reason ?? "Confirm disarming.",
                    arm ? "Arm vehicle" : "Disarm vehicle", scope.Token);
                if (!confirmed)
                {
                    return;
                }
            }
            if (scope.IsCancellationRequested || version != generation || !active.IsOnline || active.VehicleId != id ||
                active.ConnectionCancellationToken != connection || active.State?.Identity.Firmware != identity || replay.Snapshot.IsTransmissionProhibited)
            {
                return;
            }
            var freshDecision = policy.Evaluate(active.State, action);
            if (!freshDecision.IsAllowed || (freshDecision.RequiresConfirmation && !confirmed))
            {
                OperationMessage = freshDecision.Reason ?? "Command policy changed; review again.";
                return;
            }
            OperationMessage = arm ? "TX: normal arm requested; awaiting command outcome." : "TX: normal disarm requested; awaiting command outcome.";
            UpdateDocuments();
            // The typed command service owns the operation gate, ACK correlation and final-state observation.
            var result = arm ? await commands.ArmAsync(id, scope.Token) : await commands.DisarmAsync(id, confirmed, scope.Token);
            if (scope.IsCancellationRequested || version != generation || active.VehicleId != id || active.State?.Identity.Firmware != identity)
            {
                return;
            }
            OperationMessage = $"{(arm ? "Arm" : "Disarm")} command result: {result.Result}. {result.Message} Actual armed state is determined by heartbeat.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (version == generation)
            {
                OperationMessage = ex.Message;
            }
        }
        finally
        {
            if (operation == scope)
            {
                operation = null;
            }
            if (version == generation)
            {
                IsBusy = false;
                NotifyAvailability();
                UpdateDocuments();
            }
        }
    }

    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        if (HasPendingChanges && !await confirmation.ConfirmAsync("Open Parameters Editor", "Discard local arming edits and open Parameters Editor?", "Discard and open"))
        {
            return;
        }
        Discard();
        await navigation.NavigateAsync(MissionPlannerRoutes.Configuration);
    }
}
