using MissionPlanner.Firmware;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Simulation.ArduPilot;

/// <summary>Provides conservative direct-SITL model identifiers for supported ArduPilot families.</summary>
public sealed class ArduPilotFrameCatalog : IArduPilotFrameCatalog
{
    private static readonly IReadOnlyDictionary<FirmwareFamily, IReadOnlyList<string>> frames =
        new Dictionary<FirmwareFamily, IReadOnlyList<string>> { [FirmwareFamily.ArduCopter] = ["quad", "hexa", "octa", "octa-quad", "tri", "y6", "heli"], [FirmwareFamily.ArduPlane] = ["plane", "quadplane"], [FirmwareFamily.Rover] = ["rover", "balancebot"], [FirmwareFamily.ArduSub] = ["vectored", "vectored_6dof"] };

    /// <inheritdoc />
    public IReadOnlyList<string> GetFrames(FirmwareFamily family)
    {
        return frames.TryGetValue(family, out var result) ? result : [];
    }

    /// <inheritdoc />
    public bool IsSupported(FirmwareFamily family, string frameModel)
    {
        return !string.IsNullOrWhiteSpace(frameModel) &&
               GetFrames(family).Contains(frameModel.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
