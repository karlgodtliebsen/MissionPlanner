namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>
/// Identifies the current presentation state of an asynchronous operation.
/// </summary>
public enum AsyncOperationStatus
{
    /// <summary>The operation has not started.</summary>
    Idle,

    /// <summary>The operation is in progress.</summary>
    Busy,

    /// <summary>The operation completed successfully.</summary>
    Success,

    /// <summary>The operation completed with a warning.</summary>
    Warning,

    /// <summary>The operation failed.</summary>
    Error,

    /// <summary>The operation exceeded its allowed duration.</summary>
    Timeout,

    /// <summary>The operation cannot continue because its vehicle disconnected.</summary>
    Disconnected
}