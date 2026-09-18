namespace MissionPlanner.Library.Logging;

/// <summary>Host-selected logging capabilities, also usable by browser composition tests on desktop.</summary>
/// <param name="IsBrowser">True for session storage without native filesystem sinks.</param>
public sealed record LogStoragePlatform(bool IsBrowser);
