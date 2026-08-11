namespace MissionPlanner.Firmware.Exceptions;

/// <summary>Indicates that a normal vehicle connection conflicts with firmware serial ownership.</summary>
public sealed class FirmwareConnectionConflictException : FirmwareException
{
    /// <summary>Creates a conflict without operation context for compatibility with gateway callers.</summary>
    public FirmwareConnectionConflictException(string message, Exception? innerException = null) : base(message, innerException) { }

    /// <summary>Creates a conflict carrying the failed operation identity and state.</summary>
    public FirmwareConnectionConflictException(
        string message,
        Guid operationId,
        Model.FirmwareOperationState state,
        Exception? innerException = null)
        : base($"{message} Operation: {operationId}; state: {state}.", innerException)
    {
        OperationId = operationId;
        State = state;
    }

    /// <summary>Gets the rejected operation identity when available.</summary>
    public Guid? OperationId { get; }

    /// <summary>Gets the operation state at rejection when available.</summary>
    public Model.FirmwareOperationState? State { get; }
}
