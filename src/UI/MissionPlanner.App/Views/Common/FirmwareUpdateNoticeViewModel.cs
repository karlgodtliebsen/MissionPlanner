using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Common;

/// <summary>Presents an advisory firmware notice without interrupting vehicle navigation.</summary>
public sealed partial class FirmwareUpdateNoticeViewModel : ViewModelBase
{
    private readonly VehicleFirmwareUpdateService updates;
    private readonly FirmwareUpgradeSelection selection;
    private readonly INavigationService navigation;
    private readonly IExternalLinkLauncher links;
    private readonly IDisposable changed;
    private readonly IDisposable disconnected;

    /// <summary>Initializes the application-lifetime notice and its domain subscriptions.</summary>
    public FirmwareUpdateNoticeViewModel(VehicleFirmwareUpdateService updates, FirmwareUpgradeSelection selection,
        INavigationService navigation, IExternalLinkLauncher links, IUiDispatcher dispatcher,
        IDomainEventHub events, ILogger<FirmwareUpdateNoticeViewModel> logger) : base(logger, dispatcher, events)
    {
        this.updates = updates;
        this.selection = selection;
        this.navigation = navigation;
        this.links = links;
        changed = events.SubscribeDomainEventAsync<VehicleFirmwareUpdateChanged>((_, _) =>
            Dispatcher.DispatchAsync(Refresh));
        disconnected = events.SubscribeDomainEventAsync<VehicleDisconnected>((_, _) =>
            Dispatcher.DispatchAsync(() => IsVisible = false));
        Refresh();
    }

    /// <summary>Gets whether an update is available.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    /// <summary>Gets the vehicle, exact target, and compared versions.</summary>
    [ObservableProperty]
    public partial string Details { get; private set; } = "";

    private void Refresh()
    {
        var update = updates.Current;
        IsVisible = update is not null;
        if (update is not null)
        {
            var version = update.Identity.FlightVersion!;
            Details = $"SysID {update.VehicleId.SystemId} · {update.Available.Target.Platform} · " +
                $"{update.Identity.Family} {version.Major}.{version.Minor}.{version.Patch} → {update.Available.Version.Value} Stable";
        }
    }

    [RelayCommand]
    private async Task ViewUpgradeAsync()
    {
        if (updates.Current is not { } update)
        {
            return;
        }
        selection.Pending = update.Available;
        await navigation.NavigateAsync(MissionPlannerRoutes.SetupInstallFirmware);
        await updates.DismissAsync();
        IsVisible = false;
    }

    [RelayCommand]
    private async Task LaterAsync()
    {
        await updates.DismissAsync();
        IsVisible = false;
    }

    [RelayCommand]
    private Task ReleaseNotesAsync()
    {
        var family = updates.Current?.Identity.Family.ToString() switch
        {
            "ArduCopter" => "ArduCopter",
            "ArduPlane" => "ArduPlane",
            "Rover" => "Rover",
            "ArduSub" => "ArduSub",
            "AntennaTracker" => "AntennaTracker",
            "Blimp" => "Blimp",
            _ => "ArduCopter"
        };
        return links.OpenAsync(new Uri($"https://github.com/ArduPilot/ardupilot/blob/master/{family}/ReleaseNotes.txt"));
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        changed.Dispose();
        disconnected.Dispose();
        base.Dispose();
    }
}
