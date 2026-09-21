using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Commands;

/// <summary>Receiver bind command and subsequent RC input observations.</summary>
public enum ReceiverBindState
{
    /// <summary>No request in progress.</summary>
    Idle,
    /// <summary>Waiting for the command acknowledgement.</summary>
    Requested,
    /// <summary>The flight controller accepted the request.</summary>
    CommandAccepted,
    /// <summary>The flight controller does not support the command.</summary>
    Unsupported,
    /// <summary>The command failed or could not be completed.</summary>
    Failed,
    /// <summary>Waiting for evidence of RC input recovery.</summary>
    WaitingForLink,
    /// <summary>Fresh RC input returned after an observed loss.</summary>
    LinkRestored
}

/// <summary>
/// Bounded RC input recovery observation. It never treats ACK acceptance or continuous
/// pre-existing RC traffic as proof of successful physical receiver pairing.
/// </summary>
public sealed class ReceiverBindProgress
{
    private DateTimeOffset acceptedAt;
    private bool sawLoss;

    /// <summary>Gets the command or observation state.</summary>
    public ReceiverBindState State { get; private set; } = ReceiverBindState.Idle;

    /// <summary>Gets the user-facing evidence summary.</summary>
    public string Message { get; private set; } = "Use Bind in the ExpressLRS Lua script if FC-initiated binding is unavailable.";

    /// <summary>Gets whether this attempt no longer needs observations.</summary>
    public bool IsComplete { get; private set; }

    /// <summary>Starts an attempt before sending the command.</summary>
    public void Request()
    {
        State = ReceiverBindState.Requested;
        Message = "Requesting receiver bind…";
        IsComplete = false;
        sawLoss = false;
    }

    /// <summary>Applies the acknowledged command result.</summary>
    /// <param name="response">Command evidence.</param>
    public void Apply(VehicleCommandResponse response)
    {
        acceptedAt = response.CompletedAt;
        State = response.Result switch
        {
            VehicleCommandResult.Accepted => ReceiverBindState.CommandAccepted,
            VehicleCommandResult.Unsupported => ReceiverBindState.Unsupported,
            _ => ReceiverBindState.Failed
        };
        IsComplete = State != ReceiverBindState.CommandAccepted;
        Message = State switch
        {
            ReceiverBindState.CommandAccepted => "Bind command accepted/sent. Receiver bind mode is not confirmed.",
            ReceiverBindState.Unsupported => "FC-initiated binding is unsupported. Use Bind in the ExpressLRS Lua script.",
            _ => $"Bind request did not complete: {response.Message ?? response.Result.ToString()}"
        };
    }

    /// <summary>Observes current RC input while retaining command acceptance separately.</summary>
    /// <param name="vehicle">Latest target state, or absent after disconnect.</param>
    /// <param name="now">Current observation time.</param>
    /// <returns>True only when the displayed evidence changes.</returns>
    public bool Observe(VehicleState? vehicle, DateTimeOffset now)
    {
        if (IsComplete || State is not (ReceiverBindState.CommandAccepted or ReceiverBindState.WaitingForLink))
        {
            return false;
        }

        var previous = (State, Message, IsComplete);
        State = ReceiverBindState.WaitingForLink;
        Message = "Bind command accepted. Waiting for RC input loss and recovery…";
        if (vehicle is null || vehicle.ConnectionState != VehicleConnectionState.Online || vehicle.IsArmed)
        {
            IsComplete = true;
            Message = "Bind command was accepted. RC monitoring stopped because the vehicle disconnected or armed.";
        }
        else
        {
            var radio = vehicle.Radio;
            var live = !radio.IsStale(now, TimeSpan.FromSeconds(2)) &&
                radio.RssiPercent is not 0 && radio.ChannelsRaw.Any(value => value is >= 800 and <= 2200);
            if (!live)
            {
                sawLoss = true;
            }
            if (sawLoss)
            {
                Message = "Bind command accepted. RC input lost; waiting for RC input recovery…";
            }
            if (sawLoss && live && radio.ObservedAt > acceptedAt)
            {
                State = ReceiverBindState.LinkRestored;
                Message = "RC input restored after the bind request. Check transmitter controls before flight.";
                IsComplete = true;
            }
            else if (now - acceptedAt >= TimeSpan.FromSeconds(20))
            {
                FinishWaiting();
            }
        }
        return previous != (State, Message, IsComplete);
    }

    /// <summary>Ends the bounded observation without changing an accepted command into a failure.</summary>
    public void FinishWaiting()
    {
        State = ReceiverBindState.WaitingForLink;
        IsComplete = true;
        Message = "Bind command accepted; RC link recovery was not confirmed. Use Bind in the ExpressLRS Lua script.";
    }
}
