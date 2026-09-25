using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Arming;

public sealed partial class ArmingConfigurationService
{
    /// <inheritdoc />
    public async Task<ArmingChangeSet> EvaluateChangesAsync(VehicleId id, ArmingConfiguration desired, CancellationToken cancellationToken = default)
    {
        var identity = Require(id).Identity.Firmware;
        var connection = active.ConnectionCancellationToken;
        var state = await ReadAsync(id, cancellationToken).ConfigureAwait(false);
        var errors = new List<string>();
        var warnings = new List<string>();
        var changes = new List<ArmingParameterChange>();
        if (!state.IsSupported || !state.ParametersReady)
        {
            errors.Add(state.Status);
        }
        foreach (var setting in state.Settings)
        {
            var value = desired.Get(setting.Setting);
            if (value == state.Current.Get(setting.Setting))
            {
                continue;
            }
            var valid = setting.CanEdit && value is { } n && double.IsFinite(n) && n == Math.Truncate(n);
            if (setting.Setting == ArmingSetting.Checks && desired.Checks == PreArmCheckMode.Custom)
            {
                var allowed = setting.Bits.Aggregate(0, (mask, bit) => mask | (int)bit.Value);
                valid &= desired.CustomChecks > 0 && (desired.CustomChecks & ~allowed) == 0 && (desired.CustomChecks & 1) == 0;
            }
            else
            {
                valid &= setting.Choices.Any(c => c.Value == value);
            }
            if (!valid || setting.Current is null || value is null)
            {
                errors.Add($"{setting.Label}: {setting.UnavailableReason ?? "Choose a supported metadata value; custom checks require metadata and exclude All."}");
                continue;
            }
            changes.Add(new(setting.ParameterName, setting.Current.Value, value.Value, setting.RequiresReboot));
            if (setting.Setting == ArmingSetting.Checks)
            {
                warnings.Add(desired.Checks == PreArmCheckMode.Disabled
                    ? "WARNING: All pre-arm checks will be disabled. Explicit review is required."
                    : desired.Checks == PreArmCheckMode.Custom ? "A custom subset of checks is not a safety guarantee." : "All firmware pre-arm checks selected.");
            }
        }
        if (desired.ArmSwitch != state.Current.ArmSwitch)
        {
            if (!state.AssignmentsKnown || state.ArmSwitches.Count > 1)
            {
                errors.Add("Multiple or unknown RC assignments: review individual RC options in Full Parameters before reassignment.");
            }
            else if (desired.ArmSwitch is not { } destination || destination is < 0 or > 16)
            {
                errors.Add("Choose None or an RC channel from 1 to 16.");
            }
            else
            {
                if (destination > 0 && radio.Conflict(id, destination) is { } conflict)
                {
                    errors.Add(conflict);
                }
                var descriptions = await metadata.GetAllMetadataAsync(id, cancellationToken).ConfigureAwait(false);
                foreach (var channel in state.ArmSwitches.Concat(destination > 0 ? [destination] : Array.Empty<int>()))
                {
                    var name = $"RC{channel}_OPTION";
                    var next = channel == destination ? RadioArmingConfiguration.ArmDisarmOption : 0;
                    descriptions.TryGetValue(name, out var description);
                    if (!state.Evidence.TryGetValue(name, out var old) || description?.ReadOnly == true ||
                        description is not null && !description.GetValueOptions().ContainsKey(next))
                    {
                        errors.Add($"{name}: firmware metadata does not permit this assignment.");
                        continue;
                    }
                    changes.Add(new(name, old, next, description?.RebootRequired == true));
                }
            }
        }
        connection.ThrowIfCancellationRequested();
        var vehicle = Require(id);
        if (active.ConnectionCancellationToken != connection || vehicle.Identity.Firmware != identity)
        {
            throw new InvalidOperationException("Connection changed during review.");
        }
        // Clear the old switch before assigning the destination; failures stop subsequent writes.
        changes = changes.OrderBy(c => c.Name.StartsWith("RC", StringComparison.Ordinal) && c.NewValue == 0 ? 0 : 1)
            .ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
        return new(new(id, vehicle.Identity.Firmware), connection, state.Current, desired, changes, errors, warnings);
    }

    /// <inheritdoc />
    public async Task<ArmingApplyResult> ApplyAsync(VehicleId id, ArmingChangeSet changes, CancellationToken cancellationToken = default)
    {
        var confirmed = new List<string>();
        var reboot = false;
        void Guard()
        {
            cancellationToken.ThrowIfCancellationRequested();
            changes.Connection.ThrowIfCancellationRequested();
            var vehicle = Require(id);
            if (vehicle.IsArmed || vehicle.Identity.Firmware != changes.Scope.FirmwareIdentity || active.ConnectionCancellationToken != changes.Connection)
            {
                throw new InvalidOperationException("Vehicle, connection, firmware or armed state changed. Reload and review.");
            }
        }
        try
        {
            if (!changes.CanApply || changes.Scope.VehicleId != id)
            {
                return new(false, null, confirmed, false, "No valid reviewed changes.");
            }
            Guard();
            if (!gate.TryAcquire(id, "Arming configuration", out var lease))
            {
                return new(false, null, confirmed, false, "Another vehicle operation is active.");
            }
            using (lease)
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, changes.Connection))
            using (var session = factory.Create<IParameterEditSession, ParameterEditScope>(changes.Scope))
            {
                var fresh = await EvaluateChangesAsync(id, changes.Desired, linked.Token).ConfigureAwait(false);
                if (fresh.Original != changes.Original || !fresh.Changes.SequenceEqual(changes.Changes) || fresh.Errors.Count > 0)
                {
                    return new(false, await ReadAsync(id, linked.Token).ConfigureAwait(false), confirmed, false,
                        "Conflict: confirmed values or capabilities changed since review. Discard and review again.");
                }
                await session.LoadAsync(changes.Changes.Select(c => c.Name).ToArray(), linked.Token).ConfigureAwait(false);
                foreach (var change in changes.Changes)
                {
                    Guard();
                    if (change.Name.StartsWith("RC", StringComparison.Ordinal) && change.NewValue == RadioArmingConfiguration.ArmDisarmOption)
                    {
                        var channel = int.Parse(change.Name.AsSpan(2, change.Name.Length - 9), System.Globalization.CultureInfo.InvariantCulture);
                        if (radio.Conflict(id, channel) is { } conflict)
                        {
                            throw new InvalidOperationException(conflict);
                        }
                    }
                    var field = session.GetField(change.Name);
                    if (field?.LiveValue != change.OldValue || !session.TrySetPending(change.Name, change.NewValue, out _))
                    {
                        throw new InvalidOperationException($"{change.Name}: current value or validation changed.");
                    }
                    var result = await session.ApplyAsync(session.CreateWritePlan([change.Name]), cancellationToken: linked.Token).ConfigureAwait(false);
                    reboot |= result.RebootRequired || result.Success && change.RequiresReboot;
                    if (!result.Success)
                    {
                        return new(false, await ReadAsync(id, linked.Token).ConfigureAwait(false), confirmed, reboot,
                            $"Stopped at {change.Name}: write/readback was not verified. Unconfirmed changes remain pending.");
                    }
                    confirmed.Add(change.Name);
                }
                Guard();
                var actual = await ReadAsync(id, linked.Token).ConfigureAwait(false);
                var success = changes.Changes.All(c => actual.Evidence.TryGetValue(c.Name, out var value) && value == c.NewValue);
                return new(success, actual, confirmed, reboot, success ? "Changes applied and verified." : "Final readback differs; review remaining edits.");
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or IOException or HttpRequestException)
        {
            return new(false, null, confirmed, reboot, $"Apply stopped after {confirmed.Count} confirmed writes: {ex.Message}");
        }
    }
}
