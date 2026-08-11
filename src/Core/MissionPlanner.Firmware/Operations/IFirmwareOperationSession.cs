using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Operations;

/// <summary>Owns the legal transitions and cancellation policy of one firmware operation.</summary>
public interface IFirmwareOperationSession : IDisposable
{
    /// <summary>Gets the unique operation ID.</summary>
    Guid OperationId { get; }

    /// <summary>Gets the operation use case.</summary>
    FirmwareOperationKind Kind { get; }

    /// <summary>Gets the current lifecycle state.</summary>
    FirmwareOperationState State { get; }

    /// <summary>Gets whether cancellation was requested after destructive work began.</summary>
    bool CancellationRequested { get; }

    /// <summary>Publishes each accepted state transition in order.</summary>
    event EventHandler<FirmwareProgress>? ProgressChanged;

    /// <summary>Moves to a legal next state and publishes progress.</summary>
    void Transition(FirmwareProgress progress);

    /// <summary>Requests cancellation, completing immediately only when the current state is safe.</summary>
    /// <returns><see langword="true"/> when the session entered Cancelled; otherwise <see langword="false"/>.</returns>
    bool RequestCancellation(string messageCode = "operation.cancelled");
}
