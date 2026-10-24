using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>A complete parameter backup bound to the exact active connection and session.</summary>
public sealed record ParameterResetSnapshot(VehicleSession Session, CancellationToken Connection,
    IReadOnlyList<VehicleParameter> Parameters);

/// <summary>Prepares the documented ArduPilot parameter reset only after backup and explicit confirmation.</summary>
public sealed class ParameterMaintenanceService(IActiveVehicleContext active, IVehicleRegistry vehicles,
    IVehicleParameterRegistry parameters, IVehicleConnectionSession connection, IVehicleOperationGate operations, IParameterEditSessionFactory edits)
{
    /// <summary>Captures a complete live inventory; no vehicle changes are made.</summary>
    public ParameterResetSnapshot Capture()
    {
        if (edits.HasUnappliedChanges) throw new InvalidOperationException("Apply or discard all pending parameter edits before preparing a reset.");
        if (!active.IsOnline || active.VehicleId is not { } id || active.State is not { Autopilot: 3, IsArmed: false } state ||
            state.LastHeartbeatAt > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow - state.LastHeartbeatAt > TimeSpan.FromSeconds(3))
            throw new InvalidOperationException("A fresh, disarmed ArduPilot connection is required.");
        var expected = parameters.GetParameterCount(id);
        var all = parameters.GetAllParameters(id);
        if (expected is not > 0 || all.Count < expected || !all.TryGetValue("FORMAT_VERSION", out var version) || version.Value <= 0 ||
            all.Values.Any(p => !float.IsFinite(p.Value)))
            throw new InvalidOperationException("Download the complete parameter list, including FORMAT_VERSION, before making a reset backup.");
        return new(vehicles.GetRequired(id) ?? throw new InvalidOperationException("Vehicle session unavailable."),
            active.ConnectionCancellationToken, all.Values.OrderBy(p => p.Name, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Writes and verifies FORMAT_VERSION=0; reset occurs at the next manually requested reboot.</summary>
    public async Task PrepareResetAsync(ParameterResetSnapshot backup, bool backupSavedAndConfirmed, CancellationToken token)
    {
        if (!backupSavedAndConfirmed) throw new InvalidOperationException("Save a complete backup and confirm the reset first.");
        var current = Capture();
        if (!ReferenceEquals(current.Session, backup.Session) || current.Connection != backup.Connection || backup.Connection.IsCancellationRequested ||
            current.Parameters.Count != backup.Parameters.Count || !current.Parameters.Zip(backup.Parameters).All(pair => pair.First.Name == pair.Second.Name &&
                pair.First.Type == pair.Second.Type && BitConverter.SingleToInt32Bits(pair.First.Value) == BitConverter.SingleToInt32Bits(pair.Second.Value)))
            throw new InvalidOperationException("The target, connection or live parameters changed after backup. Create a new backup.");
        if (!operations.TryAcquire(backup.Session.Id, "Prepare complete parameter reset", out var lease)) throw new InvalidOperationException("Another vehicle operation is in progress.");
        using (lease)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, backup.Connection))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Changed(VehicleParameterChangedEventArgs change)
            {
                if (change.VehicleId == backup.Session.Id && change.Parameter is { Name: "FORMAT_VERSION", Value: 0 }) complete.TrySetResult();
            }
            parameters.Changed += Changed;
            try
            {
                timeout.Token.ThrowIfCancellationRequested();
                var finalReview = Capture();
                if (!ReferenceEquals(finalReview.Session, backup.Session) || finalReview.Connection != backup.Connection ||
                    finalReview.Parameters.Count != backup.Parameters.Count || !finalReview.Parameters.SequenceEqual(backup.Parameters))
                    throw new InvalidOperationException("The target or parameters changed before the reset write. Create a new backup.");
                var version = backup.Parameters.Single(p => p.Name == "FORMAT_VERSION");
                if (!await connection.ParameterService.SetParameterAsync(backup.Session.Id, version.Name, 0, version.Type, timeout.Token).ConfigureAwait(false))
                    throw new IOException("The reset preparation request could not be sent.");
                await connection.ParameterService.RequestParameterAsync(backup.Session.Id, version.Name, timeout.Token).ConfigureAwait(false);
                await complete.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                edits.Create(backup.Session.Id).Invalidate("Parameter reset prepared. Reconnect after the manual power-cycle and download the complete defaults.");
            }
            finally { parameters.Changed -= Changed; }
        }
    }
}
