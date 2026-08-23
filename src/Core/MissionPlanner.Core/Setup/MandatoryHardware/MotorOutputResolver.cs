using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Derives logical-motor assignments from the live SERVOx_FUNCTION parameter registry.</summary>
public sealed class MotorOutputResolver(IVehicleParameterRegistry parameterRegistry) : IMotorOutputResolver
{
    /// <inheritdoc />
    public MotorOutputResolution Resolve(VehicleId vehicleId, int motorNumber)
    {
        var functionValue = MotorFunction(motorNumber);
        var outputs = parameterRegistry.GetAllParameters(vehicleId)
            .Where(pair => Math.Abs(pair.Value.Value - functionValue) <= 0.5f)
            .Select(pair => TryParseOutputChannel(pair.Key, out var channel) ? channel : (int?)null)
            .Where(channel => channel.HasValue)
            .Select(channel => channel!.Value)
            .OrderBy(channel => channel)
            .ToArray();

        var status = outputs.Length switch
        {
            0 => MotorOutputResolutionStatus.NotAssigned,
            1 => MotorOutputResolutionStatus.Resolved,
            var _ => MotorOutputResolutionStatus.Ambiguous
        };
        return new MotorOutputResolution(motorNumber, status, outputs);
    }

    private static int MotorFunction(int motorNumber)
    {
        return motorNumber switch
        {
            >= 1 and <= 8 => 32 + motorNumber,
            >= 9 and <= 12 => 73 + motorNumber,
            >= 13 and <= 32 => 147 + motorNumber,
            var _ => throw new ArgumentOutOfRangeException(nameof(motorNumber), motorNumber, "Motor number must be from 1 through 32.")
        };
    }

    private static bool TryParseOutputChannel(string parameterName, out int channel)
    {
        const string prefix = "SERVO";
        const string suffix = "_FUNCTION";
        if (parameterName.StartsWith(prefix, StringComparison.Ordinal) &&
            parameterName.EndsWith(suffix, StringComparison.Ordinal) &&
            int.TryParse(parameterName.AsSpan(prefix.Length, parameterName.Length - prefix.Length - suffix.Length), out channel) &&
            channel > 0)
        {
            return true;
        }

        channel = 0;
        return false;
    }
}
