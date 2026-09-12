using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class InstallFirmwareViewModel
{
    /// <summary>Gets or sets the active tab; zero is the landing information page.</summary>
    [ObservableProperty]
    public partial int SelectedTabIndex
    {
        get;
        set;
    }

    private async Task ReturnToLandingAfterInstallationAsync()
    {
        if (!active)
        {
            return;
        }

        // Release the installation interlock before asking the shared discovery models to scan.
        ClearFirmwareSelection();
        DfuModel.CorrelatedHandoff = null;
        SelectedTabIndex = 0;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime?.Token ?? CancellationToken.None);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await Task.WhenAll(DevicesModel.RefreshAsync(cancellation.Token),
                DfuModel.RefreshAfterInstallationAsync(cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested();
            UpdatePanelCapabilities();
            SetMessages("Firmware installation completed. Controller discovery refreshed.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (active)
            {
                SetMessages("Firmware installation completed. Refresh devices to check the reconnected controller.");
            }
        }
    }
}
