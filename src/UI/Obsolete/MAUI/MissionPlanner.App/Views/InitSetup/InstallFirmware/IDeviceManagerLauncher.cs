namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Opens Windows Device Manager through a host-specific boundary.</summary>
public interface IDeviceManagerLauncher
{
    /// <summary>Gets whether Device Manager is available on this host.</summary>
    bool IsAvailable { get; }

    /// <summary>Opens Device Manager.</summary>
    Task OpenAsync(CancellationToken cancellationToken = default);
}
