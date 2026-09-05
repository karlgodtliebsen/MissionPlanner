using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents reported autopilot and peripheral hardware identifiers.</summary>
public sealed partial class HwIdViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IHwIdService service;
    private CancellationTokenSource? cancellation;

    /// <summary>Initializes the HW ID ViewModel.</summary>
    public HwIdViewModel(IActiveVehicleContext activeVehicle, IHwIdService service, ILogger<HwIdViewModel> logger)
        : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.service = service;
    }

    /// <summary>Gets reported peripheral identifiers.</summary>
    public ObservableRangeCollection<HwIdItem> Items
    {
        get;
    } = [];

    /// <summary>Gets the board summary.</summary>
    [ObservableProperty]
    public partial string Board
    {
        get;
        private set;
    } = "Unavailable";

    /// <summary>Gets the firmware summary.</summary>
    [ObservableProperty]
    public partial string Firmware
    {
        get;
        private set;
    } = "Unavailable";


    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        SetMessages("Connect a vehicle to inspect hardware identifiers.");
        activeVehicle.Changed += OnVehicleChanged;
        RefreshAsync().SafeFireAndForget();

        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        activeVehicle.Changed -= OnVehicleChanged;
        Cancel();
        return base.DeactivateAsync();
    }

    /// <inheritdoc />
    public void Cancel()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        Cancel();
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Board = Firmware = "Unavailable";
            SetMessages("Connect a vehicle to inspect hardware identifiers.");
            return;
        }

        cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        SetBusy();
        try
        {
            var snapshot = await service.GetAsync(vehicleId, cancellation.Token);
            Show(snapshot);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
        finally
        {
            ResetBusy();
        }
    }

    private void Show(HwIdSnapshot snapshot)
    {
        Dispatcher.DispatchAsync(() =>
        {
            Board = snapshot.Board;
            Firmware = snapshot.Firmware;
            Items.ReplaceRange(snapshot.Items);
        }
    );
        SetMessages(Items.Count == 0 ? "No peripheral hardware identifiers were reported." : $"{Items.Count} hardware identifier(s) reported.");
    }

    private void OnVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.DispatchAsync(RefreshAsync);
    }
}

