using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.Models;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using UraniumUI.Extensions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents reported autopilot and peripheral hardware identifiers.</summary>
public sealed partial class HwIdViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IHwIdService service;
    private readonly IDispatcher dispatcher;
    private CancellationTokenSource? cancellation;

    /// <summary>Initializes the HW ID ViewModel.</summary>
    public HwIdViewModel(ISetupWorkflowCatalog catalog, IActiveVehicleContext activeVehicle, IHwIdService service, IDispatcher dispatcher)
        : base(catalog.Workflows.First(workflow => workflow.Key == SetupWorkflowKey.HwId))
    {
        this.activeVehicle = activeVehicle;
        this.service = service;
        this.dispatcher = dispatcher;
        activeVehicle.Changed += OnVehicleChanged;
        RefreshAsync().FireAndForget();
    }

    /// <summary>Gets reported peripheral identifiers.</summary>
    public ObservableCollection<HwIdItem> Items { get; } = [];

    /// <summary>Gets the board summary.</summary>
    [ObservableProperty]
    public partial string Board { get; private set; } = "Unavailable";

    /// <summary>Gets the firmware summary.</summary>
    [ObservableProperty]
    public partial string Firmware { get; private set; } = "Unavailable";

    /// <summary>Gets the current diagnostic status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Connect a vehicle to inspect hardware identifiers.";

    /// <inheritdoc />
    public override void Cancel()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= OnVehicleChanged;
        Cancel();
        base.Dispose();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Cancel();
        Items.Clear();
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Board = Firmware = "Unavailable";
            Status = "Connect a vehicle to inspect hardware identifiers.";
            return;
        }

        cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        IsBusy = true;
        try
        {
            var snapshot = await service.GetAsync(vehicleId, cancellation.Token);
            dispatcher.Dispatch(() => Show(snapshot));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Error = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Show(HwIdSnapshot snapshot)
    {
        Board = snapshot.Board;
        Firmware = snapshot.Firmware;
        Items.Clear();
        foreach (var item in snapshot.Items)
        {
            Items.Add(item);
        }

        Status = Items.Count == 0 ? "No peripheral hardware identifiers were reported." : $"{Items.Count} hardware identifier(s) reported.";
    }

    private void OnVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(() => RefreshAsync().FireAndForget());
    }
}
