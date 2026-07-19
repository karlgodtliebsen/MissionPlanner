namespace MissionPlanner.MavLink.Configuration;

public sealed class MavFtpOptions
{
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(1.5);
    public int MaximumRequestAttempts { get; init; } = 3;
    public int ReadChunkSize { get; init; } = 200;
    public bool PreferBurstRead { get; init; } = true;
    public int BurstWindowSize { get; init; } = 3824;
    public TimeSpan CleanupTimeout { get; init; } = TimeSpan.FromMilliseconds(500);
    public int CleanupAttempts { get; init; } = 1;
    public int ResponseQueueCapacity { get; init; } = 128;

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
