namespace MissionPlanner.Simulation;

/// <summary>Describes current SITL host capabilities.</summary>
/// <param name="Platform">Detected host/runtime platform.</param>
/// <param name="Architecture">Detected process architecture.</param>
/// <param name="CanExecuteNative">Whether native SITL execution is supported.</param>
/// <param name="Message">Capability explanation.</param>
public sealed record SitlPlatformCapability(
    SitlPlatform Platform,
    SitlArchitecture Architecture,
    bool CanExecuteNative,
    string Message);
