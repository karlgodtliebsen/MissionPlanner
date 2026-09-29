using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public partial class NamingViewModel
{
    private IDisposable? detailsSubscription;
    private string? detailsKey;

    /// <summary>Gets or sets the locally recorded product name.</summary>
    [ObservableProperty] public partial string ProductName { get; set; } = "";
    /// <summary>Gets or sets the product information URL.</summary>
    [ObservableProperty] public partial string ProductUrl { get; set; } = "";
    /// <summary>Gets or sets the user's vehicle nickname.</summary>
    [ObservableProperty] public partial string Nickname { get; set; } = "";
    /// <summary>Gets the local storage status.</summary>
    [ObservableProperty] public partial string LocalDetailsStatus { get; private set; } = "";
    /// <summary>Gets whether a stable connected vehicle identity is available.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveLocalDetailsCommand))]
    public partial bool CanSaveLocalDetails { get; private set; }

    private Task OnDetailsStateUpdated(VehicleStateUpdated evt, CancellationToken token)
    {
        Dispatcher.Dispatch(() =>
        {
            if (active && evt.VehicleId == vehicle.VehicleId)
            {
                LoadLocalDetails(evt.VehicleState, force: false);
            }
        });
        return Task.CompletedTask;
    }

    private void LoadLocalDetails(VehicleState? state, bool force)
    {
        var key = state is null ? null : VehicleLocalDetails.GetKey(state.Identity.Firmware);
        CanSaveLocalDetails = active && vehicle.IsOnline && key is not null && localDetails is not null;
        if (!force && key == detailsKey)
        {
            return; // Telemetry must not replace pending edits.
        }
        detailsKey = key;
        var details = key is null ? null : localDetails?.Get(key);
        ProductName = details?.ProductName ?? "";
        ProductUrl = details?.ProductUrl ?? "";
        Nickname = details?.Nickname ?? "";
        LocalDetailsStatus = key is null
            ? "Waiting for a hardware UID. Local details cannot be reliably matched using a COM port or system ID alone."
            : "These details are stored locally; Save vehicle details does not change flight-controller parameters.";
    }

    [RelayCommand(CanExecute = nameof(CanSaveLocalDetails))]
    private void SaveLocalDetails()
    {
        if (!CanSaveLocalDetails || !vehicle.IsOnline || vehicle.State is not { } state ||
            detailsKey is null || VehicleLocalDetails.GetKey(state.Identity.Firmware) != detailsKey)
        {
            return;
        }
        var url = ProductUrl.Trim();
        if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(parsed.UserInfo)))
        {
            LocalDetailsStatus = "Enter an absolute http:// or https:// product URL without embedded credentials, or leave it empty.";
            return;
        }
        var details = new VehicleLocalDetails(ProductName.Trim(), url, Nickname.Trim());
        LocalDetailsStatus = localDetails!.Save(detailsKey, details);
        ProductName = details.ProductName;
        ProductUrl = details.ProductUrl;
        Nickname = details.Nickname;
    }
}
