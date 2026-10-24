namespace MissionPlanner.Core.Notifications;

/// <summary>Local, optional speech output with a bounded notification queue.</summary>
public interface ISpeechService
{
    /// <summary>Gets whether a local speech backend is installed.</summary>
    bool IsAvailable { get; }
    /// <summary>Gets the selected backend or its unavailability explanation.</summary>
    string Status { get; }
    /// <summary>Queues a short notification without blocking its visual delivery.</summary>
    bool TryEnqueue(string text);
    /// <summary>Speaks an explicit test, awaiting completion or cancellation.</summary>
    Task TestAsync(CancellationToken cancellationToken);
}

/// <summary>Explicit fallback for hosts without a local speech adapter.</summary>
public sealed class UnavailableSpeechService : ISpeechService
{
    /// <inheritdoc />
    public bool IsAvailable => false;
    /// <inheritdoc />
    public string Status => "Speech is unavailable on this host.";
    /// <inheritdoc />
    public bool TryEnqueue(string text) => false;
    /// <inheritdoc />
    public Task TestAsync(CancellationToken cancellationToken) => throw new NotSupportedException(Status);
}
