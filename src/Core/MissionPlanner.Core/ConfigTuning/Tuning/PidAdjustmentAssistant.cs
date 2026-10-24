using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>An immutable local PID preview bound to its original editing session and connection.</summary>
public sealed record PidAdjustmentReview(IParameterEditSession Session, CancellationToken Connection, double Percent, IReadOnlyList<ReviewedParameterEdit> Changes);

/// <summary>Previews modest coupled P/I scaling without choosing new gain ratios, modifying D, or transmitting parameters.</summary>
public sealed class PidAdjustmentAssistant(IActiveVehicleContext active, IParameterEditSessionFactory sessions, IVehicleParameterRegistry parameters)
{
    /// <summary>Builds a before/after preview from a disarmed Copter's reported controller generation.</summary>
    public async Task<PidAdjustmentReview> PreviewAsync(bool rollPitch, bool climb, double percent, CancellationToken token)
    {
        RequireCopter();
        if ((!rollPitch && !climb) || !double.IsFinite(percent) || percent is < 90 or > 110) throw new ArgumentException("Select roll/pitch and/or climb P/I with a scale of 90–110 percent.");
        var id = active.VehicleId!.Value; var boundary = active.ConnectionCancellationToken;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, boundary);
        var session = sessions.Create(id); var live = parameters.GetAllParameters(id);
        var names = new List<string>();
        if (rollPitch)
        {
            var prefix = new[] { "ATC_RAT_", "RATE_" }.FirstOrDefault(candidate => new[] { "RLL_P", "RLL_I", "PIT_P", "PIT_I" }.All(suffix => live.ContainsKey(candidate + suffix)))
                ?? throw new InvalidOperationException("A complete reported roll/pitch P/I controller set is required.");
            names.AddRange(new[] { "RLL_P", "RLL_I", "PIT_P", "PIT_I" }.Select(suffix => prefix + suffix));
        }
        if (climb)
        {
            var prefix = new[] { "PSC_D_ACC_", "PSC_ACCZ_", "ACCEL_Z_", "THR_ACCEL_" }.FirstOrDefault(candidate => live.ContainsKey(candidate + "P") && live.ContainsKey(candidate + "I"))
                ?? throw new InvalidOperationException("A complete reported vertical-acceleration P/I controller pair is required.");
            names.Add(prefix + "P"); names.Add(prefix + "I");
        }
        await session.LoadDefinitionsAsync(names.Select(name => new ParameterFieldDefinition(name, [name])).ToArray(), linked.Token).ConfigureAwait(false);
        RequireCopter();
        if (active.VehicleId != id || active.ConnectionCancellationToken != boundary || !session.IsValid) throw new InvalidOperationException("The vehicle connection changed during preview.");
        var proposals = names.Select(name =>
        {
            var field = session.GetField(name) ?? throw new InvalidOperationException($"{name} is unavailable.");
            if (field.IsModified || !field.IsValid || field.Type != MavParamType.Real32 || field.Metadata.ReadOnly ||
                !double.IsFinite(field.LiveValue) || field.LiveValue < 0 || name.EndsWith("_P", StringComparison.Ordinal) && field.LiveValue == 0)
                throw new InvalidOperationException($"{name} must have a valid, nonnegative unmodified floating-point gain (P must be positive). Existing pending edits are preserved.");
            var value = field.LiveValue * percent / 100;
            if (percent != 100 && field.Metadata.Increment is > 0 and var step)
            {
                var origin = field.Metadata.Minimum ?? 0;
                value = origin + Math.Round((value - origin) / step) * step;
            }
            value = (float)value;
            if (!double.IsFinite(value) || value < 0 || field.Metadata.Minimum is { } minimum && value < minimum || field.Metadata.Maximum is { } maximum && value > maximum)
                throw new InvalidOperationException($"{name} would exceed firmware limits. No values were staged.");
            return new ReviewedParameterEdit(name, field.LiveValue, field.PendingValue, value);
        }).ToArray();
        return new(session, boundary, percent, Array.AsReadOnly(proposals));
    }
    /// <summary>Stages the reviewed batch atomically; actual vehicle application remains a separate editor action.</summary>
    public void Stage(PidAdjustmentReview review)
    {
        RequireCopter();
        if (active.VehicleId != review.Session.VehicleId || active.ConnectionCancellationToken != review.Connection || review.Connection.IsCancellationRequested || !review.Session.IsValid)
            throw new InvalidOperationException("The reviewed vehicle connection changed. Preview again.");
        if (!review.Session.TryStageReviewed(review.Changes, out var error)) throw new InvalidOperationException(error);
    }
    private void RequireCopter()
    {
        var now = DateTimeOffset.UtcNow;
        if (!active.IsOnline || active.VehicleId is null || active.State is not { IsArmed: false, Autopilot: 3 } state || state.Identity.Firmware.Family != FirmwareFamily.ArduCopter ||
            state.LastHeartbeatAt > now || now - state.LastHeartbeatAt > TimeSpan.FromSeconds(3)) throw new InvalidOperationException("Select a fresh, disarmed ArduCopter for this optional P/I assistant.");
    }
}
