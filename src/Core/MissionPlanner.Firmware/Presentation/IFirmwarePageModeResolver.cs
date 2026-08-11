namespace MissionPlanner.Firmware.Presentation;

/// <summary>Resolves firmware-page mode and capabilities from application state.</summary>
public interface IFirmwarePageModeResolver
{
    /// <summary>Resolves the complete page policy for <paramref name="context"/>.</summary>
    FirmwarePageState Resolve(FirmwarePageContext context);
}
