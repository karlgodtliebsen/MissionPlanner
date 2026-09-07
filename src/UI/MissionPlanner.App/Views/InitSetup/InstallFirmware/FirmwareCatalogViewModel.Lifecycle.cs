using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

public sealed partial class FirmwareCatalogViewModel
{
    private readonly IFirmwareCatalogService catalogService;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IDialogService dialogService;
    private readonly FirmwareDialogCoordinator firmwareDialogs;
    private readonly FirmwarePanelLoader loader = new();

    /// <summary>Gets whether catalogue loading owns the progress dialog.</summary>
    [ObservableProperty] public partial bool IsRefreshing { get; private set; }
    /// <summary>Gets or sets the parent's exclusive installation interlock.</summary>
    [ObservableProperty] public partial bool InstallationRunning { get; set; }
    /// <summary>Notifies the active parent about read-operation ownership.</summary>
    public event Action<bool>? RefreshStateChanged;

    partial void OnIsRefreshingChanged(bool value) => RefreshStateChanged?.Invoke(value);
    partial void OnInstallationRunningChanged(bool value)
    {
        if (value) { loader.Cancel(); }
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (!await loader.ActivateAsync()) { return; }
        activeVehicle.Changed += VehicleChanged;
        Devices.DevicesChanged += DevicesChanged;
        IsVehicleConnected = activeVehicle.IsOnline;
        availableDevices = Devices.Descriptors;
        await RefreshAsync(false);
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        activeVehicle.Changed -= VehicleChanged;
        Devices.DevicesChanged -= DevicesChanged;
        await loader.DeactivateAsync();
    }

    /// <summary>Refreshes this panel only; serial/DFU panels own their own discovery.</summary>
    public Task RefreshAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        if (InstallationRunning || activeVehicle.IsOnline) { return Task.CompletedTask; }
        return loader.RunAsync(token => LoadCatalogueAsync(forceRefresh, token), cancellationToken);
    }

    /// <summary>Cancels the panel-owned refresh without clearing the last usable catalogue.</summary>
    public void CancelRefresh() => loader.Cancel();

    private async Task LoadCatalogueAsync(bool forceRefresh, CancellationToken token)
    {
        if (InstallationRunning || activeVehicle.IsOnline) { return; }
        IDisposable? progress = null;
        var channel = SelectedChannel;
        var allOptions = showingAllOptions;
        try
        {
            IsRefreshing = true;
            SetBusy();
            SetMessages("Loading firmware catalogue…");
            if (!OperatingSystem.IsBrowser())
            {
                progress = await firmwareDialogs.BeginAsync(() => dialogService.DisplayProgressCancellableAsync(
                    () => StatusMessage ?? "Loading firmware catalogue…",
                    new DialogOptions { Title = "Loading firmware catalogue" }, token), false, token);
            }
            var catalog = await Task.Run(() => catalogService.GetCatalogAsync(
                new FirmwareCatalogRequest(Channel: allOptions ? null : channel, ForceRefresh: forceRefresh), token), token);
            token.ThrowIfCancellationRequested();
            var entries = catalog.Entries.Where(entry => entry.Target.VehicleType != FirmwareVehicleType.Unknown
                && entry.Artifact.Format is FirmwareImageFormat.Apj or FirmwareImageFormat.Px4).ToArray();
            await Dispatcher.DispatchAsync(() =>
            {
                if (token.IsCancellationRequested) { return; }
                SetCatalogue(entries, Devices.Descriptors, allOptions);
                Devices.SetCatalogue(entries);
                SetMessages(catalog.IsStale ? "Showing cached firmware catalogue" : $"{FirmwareChoices.Count} vehicle firmware choices available");
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Firmware catalogue refresh failed.");
            await Dispatcher.DispatchAsync(() =>
            {
                if (!token.IsCancellationRequested) { SetMessages(exception); }
            });
        }
        finally
        {
            await Dispatcher.DispatchAsync(() =>
            {
                progress?.Dispose();
                IsRefreshing = false;
                ResetBusy();
            });
        }
    }

    private async Task ReloadChannelAsync()
    {
        await loader.CancelAndWaitAsync();
        if (loader.IsActive) { await RefreshAsync(false); }
    }

    private void DevicesChanged(IReadOnlyList<SerialDeviceDescriptor> devices)
    {
        availableDevices = devices;
        ApplyTargetQuery();
    }

    private void VehicleChanged(ActiveVehicleChangedEventArgs args) => Dispatcher.Dispatch(() =>
    {
        if (!loader.IsActive) { return; }
        IsVehicleConnected = args.Current.IsOnline;
        if (IsVehicleConnected) { loader.Cancel(); }
        else { _ = RefreshAsync(false); }
    });

    /// <inheritdoc />
    public override void Dispose()
    {
        _ = DeactivateAsync();
        base.Dispose();
    }
}
