using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Represents one selectable servo output function.</summary>
/// <param name="Value">The stored function value.</param>
/// <param name="Name">The human-readable function name.</param>
public sealed record ServoFunctionOption(int Value, string Name);

/// <summary>Projects one servo output channel with its function and live PWM.</summary>
/// <param name="Output">The one-based servo output number.</param>
/// <param name="FunctionValue">The configured output function value.</param>
/// <param name="FunctionName">The human-readable function name.</param>
/// <param name="LivePwm">The latest raw output PWM, when reported.</param>
/// <param name="IsStale">Whether the live output telemetry is stale.</param>
public sealed record ServoOutputInfo(
    int Output,
    int FunctionValue,
    string FunctionName,
    int? LivePwm,
    bool IsStale);

/// <summary>Represents the immutable servo output configuration projected by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the configuration belongs to.</param>
/// <param name="Outputs">The discovered servo outputs in ascending order.</param>
/// <param name="FunctionOptions">The available function assignments from metadata.</param>
public sealed record ServoOutputConfiguration(
    VehicleId VehicleId,
    IReadOnlyList<ServoOutputInfo> Outputs,
    IReadOnlyList<ServoFunctionOption> FunctionOptions)
{
    /// <summary>Creates an empty configuration for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An empty configuration.</returns>
    public static ServoOutputConfiguration Empty(VehicleId vehicleId) => new(vehicleId, [], []);
}

/// <summary>Represents the outcome of a confirmed servo function write.</summary>
/// <param name="Success">Whether the vehicle confirmed the new function by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record ServoOutputApplyResult(bool Success, string Message);
