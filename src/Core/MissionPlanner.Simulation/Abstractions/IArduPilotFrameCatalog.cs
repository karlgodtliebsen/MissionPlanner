using MissionPlanner.Firmware;

namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Provides supported ArduPilot frames/models by firmware family.</summary>
public interface IArduPilotFrameCatalog
{
    /// <summary>Gets the supported direct-SITL model identifiers for a family.</summary>
    /// <param name="family">Firmware family.</param>
    /// <returns>Supported model identifiers.</returns>
    IReadOnlyList<string> GetFrames(FirmwareFamily family);

    /// <summary>Determines whether a frame/model is supported by the direct SITL adapter.</summary>
    /// <param name="family">Firmware family.</param>
    /// <param name="frameModel">Frame/model identifier.</param>
    /// <returns><see langword="true"/> when supported.</returns>
    bool IsSupported(FirmwareFamily family, string frameModel);
}
