using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Images;

/// <summary>Reads validated APJ and PX4 JSON firmware packages.</summary>
public interface IFirmwarePackageReader
{
    /// <summary>Reads and validates a package without unbounded allocation.</summary>
    Task<ApjFirmwarePackage> ReadAsync(Stream stream, CancellationToken cancellationToken = default);
}
