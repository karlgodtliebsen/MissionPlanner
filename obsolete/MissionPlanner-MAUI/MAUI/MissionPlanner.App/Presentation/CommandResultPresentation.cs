namespace MissionPlanner.App.Presentation;

/// <summary>
/// Describes the concise result shown after a vehicle command completes.
/// </summary>
/// <param name="Title">The result title.</param>
/// <param name="Message">The result detail.</param>
/// <param name="Status">The semantic result status.</param>
/// <param name="CanRetry">Whether the UI should offer a retry action.</param>
public sealed record CommandResultPresentation(
    string Title,
    string Message,
    AsyncOperationStatus Status,
    bool CanRetry = false)
{
    /// <summary>
    /// Creates a command result from an asynchronous operation state.
    /// </summary>
    /// <param name="title">The command title.</param>
    /// <param name="state">The completed operation state.</param>
    /// <returns>A concise command result presentation.</returns>
    public static CommandResultPresentation FromOperation(string title, AsyncOperationState state) =>
        new(title, state.Message ?? state.Status.ToString(), state.Status,
            state.Status is AsyncOperationStatus.Warning or AsyncOperationStatus.Error or AsyncOperationStatus.Timeout);
}
