using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Session-local tracker-home implementation.</summary>
public sealed class TrackerHomeService : ITrackerHomeService
{
    /// <inheritdoc />
    public event Action? Changed; /// <inheritdoc />
    public TrackerHomeSnapshot? Snapshot
    {
        get; private set;
    }
    /// <inheritdoc />
    public void Set(GeoPosition position, double? altitudeMeters, DateTimeOffset updatedAt, string source)
    {
        if (!position.IsValid)
        {
            throw new ArgumentException("Tracker-home coordinate is invalid.");
        }

        Snapshot = new(position, altitudeMeters, updatedAt, source);
        Changed?.Invoke();
    }
}
