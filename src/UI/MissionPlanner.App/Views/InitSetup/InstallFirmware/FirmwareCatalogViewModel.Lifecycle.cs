using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class FirmwareCatalogueViewModel
{
    private readonly IFirmwareCatalogService catalogService;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly FirmwarePanelLoader loader = new();

    /// <summary>Gets whether the inline catalogue progress indicator is active.</summary>
    [ObservableProperty]
    public partial bool IsRefreshing
    {
        get; private set;
    }
    /// <summary>Gets or sets the parent's exclusive installation interlock.</summary>
    [ObservableProperty]
    public partial bool InstallationRunning
    {
        get; set;
    }
    /// <summary>Notifies the active parent about read-operation ownership.</summary>
    public event Action<bool>? RefreshStateChanged;

    partial void OnIsRefreshingChanged(bool value) => RefreshStateChanged?.Invoke(value);
    partial void OnInstallationRunningChanged(bool value)
    {
        if (value)
        {
            loader.Cancel();
        }
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (!await loader.ActivateAsync())
        {
            return;
        }
        activeVehicle.Changed += VehicleChanged;
        DevicesModel.DevicesChanged += DevicesModelChanged;
        IsVehicleConnected = activeVehicle.IsOnline;
        availableDevices = DevicesModel.Descriptors;
        await RefreshAsync(false);
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        activeVehicle.Changed -= VehicleChanged;
        DevicesModel.DevicesChanged -= DevicesModelChanged;
        await loader.DeactivateAsync();
    }

    /// <summary>Refreshes this panel only; serial/DFU panels own their own discovery.</summary>
    public Task RefreshAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        return InstallationRunning
            ? Task.CompletedTask
            : loader.RunAsync(token => LoadCatalogueAsync(forceRefresh, token), cancellationToken);
    }

    /// <summary>Loads platform choices independently of the online selector's view lifetime.</summary>
    public async Task EnsureKnownPlatformsAsync(CancellationToken cancellationToken = default)
    {
        if (KnownPlatforms.Count > 0 || IsRefreshing || InstallationRunning)
        {
            return;
        }
        try
        {
            IsRefreshing = true;
            SetMessages("Loading controller platform choices…");
            var catalogue = await catalogService.GetCatalogAsync(new FirmwareCatalogRequest(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            KnownPlatforms = catalogue.Entries.Select(entry => entry.Target.Platform)
                .Where(platform => !string.IsNullOrWhiteSpace(platform)).Distinct(StringComparer.Ordinal)
                .OrderBy(platform => platform, StringComparer.Ordinal).ToArray();
            OnPropertyChanged(nameof(KnownPlatforms));
            SetMessages(KnownPlatforms.Count > 0 ? "Controller platform choices loaded." : "No controller platforms were found. Retry loading the catalogue.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetMessages("Loading controller platforms cancelled. Retry when ready.");
        }
        catch (Exception exception)
        {
            SetMessages("Unable to load controller platforms. Check the connection and retry.", exception.Message);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>Cancels the panel-owned refresh without clearing the last usable catalogue.</summary>
    public void CancelRefresh()
    {
        loader.Cancel();
    }

    private async Task LoadCatalogueAsync(bool forceRefresh, CancellationToken token)
    {
        if (InstallationRunning)
        {
            return;
        }

        var channel = SelectedChannel;
        var allOptions = showingAllOptions;
        try
        {
            IsRefreshing = true;
            SetBusy();
            SetMessages("Loading firmware catalogue…");

            var catalog = await Task.Run(() => catalogService.GetCatalogAsync(
                new FirmwareCatalogRequest(Channel: allOptions ? null : channel, ForceRefresh: forceRefresh), token), token);
            token.ThrowIfCancellationRequested();
            var entries = catalog.Entries.Where(entry => entry.Target.VehicleType != FirmwareVehicleType.Unknown
                && entry.Artifact.Format == FirmwareImageFormat.Apj).ToArray();
            await Dispatcher.DispatchAsync(() =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                SetCatalogue(entries, DevicesModel.Descriptors, allOptions);
                DevicesModel.SetCatalogue(entries);
                SetMessages(catalog.IsStale ? "Showing cached firmware catalogue" : $"{FirmwareChoices.Count} vehicle firmware choices available");
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Firmware catalogue refresh failed.");
            await Dispatcher.DispatchAsync(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    SetMessages(exception);
                }
            });
        }
        finally
        {
            await Dispatcher.DispatchAsync(() =>
            {

                IsRefreshing = false;
                ResetBusy();
            });
        }
    }

    private async Task ReloadChannelAsync()
    {
        await loader.CancelAndWaitAsync();
        if (loader.IsActive)
        {
            await RefreshAsync(false);
        }
    }

    private void DevicesModelChanged(IReadOnlyList<SerialDeviceDescriptor> devices)
    {
        availableDevices = devices;
        ApplyTargetQuery();
    }

    private void VehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.Dispatch(() =>
    {
        if (!loader.IsActive)
        {
            return;
        }
        IsVehicleConnected = args.Current.IsOnline;

    });
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _ = DeactivateAsync();
        base.Dispose();
    }
}
