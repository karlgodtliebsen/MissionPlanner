using System.Collections.Concurrent;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Commands;

/// <inheritdoc />
public sealed class CommandAckTracker : ICommandAckTracker
{
    private readonly ConcurrentDictionary<CommandAckKey, TaskCompletionSource<CommandAckMessage>> pending = [];

    /// <inheritdoc />
    public Task<CommandAckMessage> WaitForAckAsync(VehicleId vehicleId, ushort command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var key = new CommandAckKey(vehicleId, command);

        var completion = new TaskCompletionSource<CommandAckMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!pending.TryAdd(key, completion))
        {
            throw new InvalidOperationException($"A command '{command}' is already pending for vehicle '{vehicleId}'.");
        }

        _ = CompleteOnTimeoutAsync(key, completion, timeout, cancellationToken);
        return completion.Task;
    }


    /// <inheritdoc />
    public void Handle(CommandAckMessage message)
    {
        var key = new CommandAckKey(
            new VehicleId(message.SystemId, message.ComponentId),
            message.Command);

        if (pending.TryRemove(key, out var completion))
        {
            completion.TrySetResult(message);
        }
    }

    private async Task CompleteOnTimeoutAsync(CommandAckKey key, TaskCompletionSource<CommandAckMessage> completion, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);

            if (pending.TryRemove(key, out var removed))
            {
                removed.TrySetException(new TimeoutException($"Timed out waiting for ACK for command '{key.Command}' from vehicle '{key.VehicleId}'."));
            }
        }
        catch (OperationCanceledException ex)
        {
            if (pending.TryRemove(key, out var removed))
            {
                removed.TrySetCanceled(ex.CancellationToken);
            }
        }
    }

    private readonly record struct CommandAckKey(VehicleId VehicleId, ushort Command);
}
