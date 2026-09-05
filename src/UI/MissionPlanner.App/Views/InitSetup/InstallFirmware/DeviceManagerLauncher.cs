using System.Diagnostics;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Implements the Windows-only Device Manager host action.</summary>
public sealed class DeviceManagerLauncher : IDeviceManagerLauncher
{
    /// <inheritdoc />
    public bool IsAvailable => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAvailable)
        {
            throw new PlatformNotSupportedException("Device Manager is available only on Windows.");
        }

        Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
        return Task.CompletedTask;
    }
}

