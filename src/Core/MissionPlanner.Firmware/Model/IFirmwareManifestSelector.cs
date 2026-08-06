namespace MissionPlanner.Firmware.Model;

/// <summary>Selects releases by exact protocol-reported board identity.</summary>
public interface IFirmwareManifestSelector
{
    /// <summary>Selects compatible releases without inferring a target from a marketing name.</summary>
    /// <param name="releases">The manifest releases.</param>
    /// <param name="identity">The protocol-reported vehicle firmware and board identity.</param>
    /// <param name="channel">The requested release channel.</param>
    /// <returns>Compatible releases ordered newest first.</returns>
    IReadOnlyList<FirmwareManifestEntryRecord> Select(IReadOnlyList<FirmwareManifestEntryRecord> releases, VehicleFirmwareIdentity identity, FirmwareReleaseChannel channel);
}
