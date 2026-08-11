namespace MissionPlanner.Firmware.Dfu;

/// <summary>Locates and validates an installed DFU provider tool.</summary>
public interface IDfuToolLocator
{
    /// <summary>Locates and validates the configured or installed tool.</summary>
    Task<DfuToolStatus> LocateAsync(CancellationToken cancellationToken = default);
}
