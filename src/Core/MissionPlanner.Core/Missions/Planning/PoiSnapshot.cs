namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Immutable POI collection state.</summary>
public sealed record PoiSnapshot(IReadOnlyList<PointOfInterest> Items, PointOfInterestId? SelectedId);