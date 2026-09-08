namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Launches a validated external support destination through the host.</summary>
public interface IExternalLinkLauncher
{
    /// <summary>Opens an absolute HTTPS destination.</summary>
    Task OpenAsync(Uri uri, CancellationToken cancellationToken = default);
}
