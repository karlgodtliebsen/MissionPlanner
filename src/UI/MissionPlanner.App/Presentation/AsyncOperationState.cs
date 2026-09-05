namespace MissionPlanner.App.Presentation;

/// <summary>
/// Provides reusable, bindable feedback for a long-running UI operation.
/// </summary>
/// <param name="Status">The semantic operation status.</param>
/// <param name="Message">The user-facing status message.</param>
/// <param name="Exception">The optional exception retained for diagnostics.</param>
public sealed record AsyncOperationState(AsyncOperationStatus Status, string? Message = null, Exception? Exception = null)
{
    /// <summary>Gets the initial operation state.</summary>
    public static AsyncOperationState Idle { get; } = new(AsyncOperationStatus.Idle);

    /// <summary>Creates a busy operation state.</summary>
    /// <param name="message">The progress message.</param>
    /// <returns>The busy state.</returns>
    public static AsyncOperationState Busy(string? message = null)
    {
        return new(AsyncOperationStatus.Busy, message);
    }

    /// <summary>Creates a successful operation state.</summary>
    /// <param name="message">The completion message.</param>
    /// <returns>The successful state.</returns>
    public static AsyncOperationState Success(string? message = null)
    {
        return new(AsyncOperationStatus.Success, message);
    }

    /// <summary>Creates a warning operation state.</summary>
    /// <param name="message">The warning message.</param>
    /// <returns>The warning state.</returns>
    public static AsyncOperationState Warning(string message)
    {
        return new(AsyncOperationStatus.Warning, message);
    }

    /// <summary>Creates a failed operation state.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="exception">The optional underlying exception.</param>
    /// <returns>The failed state.</returns>
    public static AsyncOperationState Error(string message, Exception? exception = null)
    {
        return new(AsyncOperationStatus.Error, message, exception);
    }

    /// <summary>Creates a timed-out operation state.</summary>
    /// <param name="message">The timeout message.</param>
    /// <returns>The timed-out state.</returns>
    public static AsyncOperationState Timeout(string message)
    {
        return new(AsyncOperationStatus.Timeout, message);
    }

    /// <summary>Creates a disconnected operation state.</summary>
    /// <param name="message">The disconnect message.</param>
    /// <returns>The disconnected state.</returns>
    public static AsyncOperationState Disconnected(string message = "Vehicle disconnected.")
    {
        return new(AsyncOperationStatus.Disconnected, message);
    }

    /// <summary>Gets a value indicating whether an operation is in progress.</summary>
    public bool IsBusy => Status == AsyncOperationStatus.Busy;
}
