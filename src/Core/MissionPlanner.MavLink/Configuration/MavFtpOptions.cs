namespace MissionPlanner.MavLink.Configuration;

/// <summary>
/// Provides the public API for MavFtpOptions.
/// </summary>
public sealed class MavFtpOptions
{
    /// <summary>
    /// Provides the public API for RequestTimeout.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(1.5);
    /// <summary>
    /// Provides the public API for MaximumRequestAttempts.
    /// </summary>
    public int MaximumRequestAttempts { get; init; } = 3;
    /// <summary>
    /// Provides the public API for ReadChunkSize.
    /// </summary>
    public int ReadChunkSize { get; init; } = 200;
    /// <summary>
    /// Provides the public API for PreferBurstRead.
    /// </summary>
    public bool PreferBurstRead { get; init; } = true;
    /// <summary>
    /// Provides the public API for BurstWindowSize.
    /// </summary>
    public int BurstWindowSize { get; init; } = 3824;
    /// <summary>
    /// Provides the public API for CleanupTimeout.
    /// </summary>
    public TimeSpan CleanupTimeout { get; init; } = TimeSpan.FromMilliseconds(500);
    /// <summary>
    /// Provides the public API for CleanupAttempts.
    /// </summary>
    public int CleanupAttempts { get; init; } = 1;
    /// <summary>
    /// Provides the public API for ResponseQueueCapacity.
    /// </summary>
    public int ResponseQueueCapacity { get; init; } = 128;

    /// <summary>
    /// Provides the public API for Validate.
    /// </summary>
    public void Validate()
    {
        if (RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        }

        if (MaximumRequestAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumRequestAttempts));
        }

        if (ReadChunkSize is < 1 or > 239)
        {
            throw new ArgumentOutOfRangeException(nameof(ReadChunkSize));
        }

        if (BurstWindowSize < ReadChunkSize || BurstWindowSize > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(BurstWindowSize));
        }

        if (CleanupTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(CleanupTimeout));
        }

        if (CleanupAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(CleanupAttempts));
        }

        if (ResponseQueueCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ResponseQueueCapacity));
        }
    }
}
