using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Configures a user-selected external SITL installation.</summary>
public sealed class ExternalSitlInstallationOptions
{
    /// <summary>Gets or sets the firmware family.</summary>
    public FirmwareFamily Family { get; set; }

    /// <summary>Gets or sets an optional version label used when probing is unavailable.</summary>
    public string? Version { get; set; }

    /// <summary>Gets or sets the absolute executable path.</summary>
    public string ExecutablePath { get; set; } = string.Empty;
}
