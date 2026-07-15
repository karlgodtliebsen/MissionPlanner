namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Represents a message pump that reads messages from a vehicle connection and handles them.
/// </summary>
public interface IVehicleMessagePump : IAsyncDisposable
{
    /// <summary>
    /// Starts pumping messages from the vehicle connection.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task StartAsync(CancellationToken cancellationToken);
}
