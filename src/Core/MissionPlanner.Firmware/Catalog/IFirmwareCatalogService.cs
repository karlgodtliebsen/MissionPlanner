namespace MissionPlanner.Firmware.Catalog;

/// <summary>Loads and filters the firmware release catalogue.</summary>
public interface IFirmwareCatalogService
{
    /// <summary>Gets a catalogue matching the request.</summary>
    Task<FirmwareCatalog> GetCatalogAsync(FirmwareCatalogRequest request, CancellationToken cancellationToken = default);
}
