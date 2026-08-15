namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Provides the curated firmware-support link catalogue.</summary>
public interface IFirmwareSupportLinkProvider
{
    /// <summary>Gets all supported links in presentation order.</summary>
    IReadOnlyList<FirmwareSupportLink> GetLinks();
}
