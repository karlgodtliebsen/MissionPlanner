using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Firmware;

/// <summary>Selects releases by exact protocol-reported board identity.</summary>
public interface IFirmwareManifestSelector
{
    /// <summary>Selects compatible releases without inferring a target from a marketing name.</summary>
    /// <param name="releases">The manifest releases.</param>
    /// <param name="identity">The protocol-reported vehicle firmware and board identity.</param>
    /// <param name="channel">The requested release channel.</param>
    /// <returns>Compatible releases ordered newest first.</returns>
    IReadOnlyList<FirmwareManifestEntry> Select(
        IReadOnlyList<FirmwareManifestEntry> releases,
        VehicleFirmwareIdentity identity,
        FirmwareReleaseChannel channel);
}
