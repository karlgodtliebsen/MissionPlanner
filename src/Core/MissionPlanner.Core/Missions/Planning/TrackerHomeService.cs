using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;
/// <summary>Local antenna-tracker planning position; it does not represent hardware state.</summary>
public sealed record TrackerHomeSnapshot(GeoPosition Position, double? AltitudeMeters, DateTimeOffset UpdatedAt, string Source);
/// <summary>Owns session-local tracker home state.</summary>
public interface ITrackerHomeService { /// <summary>Raised when state changes.</summary>
    event EventHandler? Changed; /// <summary>Gets current state.</summary>
    TrackerHomeSnapshot? Snapshot { get; } /// <summary>Updates local state only.</summary>
    void Set(GeoPosition position, double? altitudeMeters, DateTimeOffset updatedAt, string source); }
/// <summary>Session-local tracker-home implementation.</summary>
public sealed class TrackerHomeService : ITrackerHomeService { /// <inheritdoc />
    public event EventHandler? Changed; /// <inheritdoc />
    public TrackerHomeSnapshot? Snapshot { get; private set; } /// <inheritdoc />
    public void Set(GeoPosition position, double? altitudeMeters, DateTimeOffset updatedAt, string source) { if (!position.IsValid) throw new ArgumentException("Tracker-home coordinate is invalid."); Snapshot=new(position,altitudeMeters,updatedAt,source); Changed?.Invoke(this,EventArgs.Empty); } }
