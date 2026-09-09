namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Resolves one exact, reviewed board identity without using MCU-family shortcuts.</summary>
public interface IBetaflightArduPilotCompatibilityProvider
{
    /// <summary>Returns the single exact match, or null for unsupported/ambiguous identities.</summary>
    BetaflightArduPilotMapping? Resolve(BetaflightDeviceInfo identity);
}