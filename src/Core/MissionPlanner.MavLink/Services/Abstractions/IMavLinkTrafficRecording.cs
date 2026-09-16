namespace MissionPlanner.MavLink.Services.Abstractions;

/// <summary>Optional connection-boundary recording, independent of message decoding.</summary>
public interface IMavLinkTrafficRecording
{
    /// <summary>Starts a recording observer before transport reception begins.</summary>
    /// <param name="tap">The connection-owned traffic fan-out.</param>
    /// <returns>A lease that drains and finalizes recording when disposed.</returns>
    IAsyncDisposable Start(MavLinkInspectionTap tap);
}
