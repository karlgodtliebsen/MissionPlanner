namespace MissionPlanner.MavLink.Configuration;

public sealed class MavFtpOptions
{
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(1.5);
    public int MaximumRequestAttempts { get; init; } = 3;
    public int ReadChunkSize { get; init; } = 200;
    public bool PreferBurstRead { get; init; } = true;

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
    }
}
