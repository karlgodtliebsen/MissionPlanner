using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.FlightData.Components;
using MissionPlanner.Core.FlightData.Payload;
using MissionPlanner.Core.Vehicles.Abstractions;
using BaseViewModel = MissionPlanner.App.Helpers.BaseViewModel;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Presents discovered, component-targeted camera and gimbal controls.</summary>
public partial class PayloadControlTabViewModel : BaseViewModel
{
    private readonly IActiveVehicleContext active;
    private readonly IVehicleComponentRegistry components;
    private readonly ICameraProtocolService cameras;
    private readonly IGimbalProtocolService gimbals;
    private readonly IDispatcher dispatcher;

    /// <summary>Initializes payload controls for the transient tab lifetime.</summary>
    public PayloadControlTabViewModel(IActiveVehicleContext active, IVehicleComponentRegistry components,
        ICameraProtocolService cameras, IGimbalProtocolService gimbals, IDispatcher dispatcher, ILogger<PayloadControlTabViewModel> logger)
        : base(logger)
    {
        this.active = active;
        this.components = components;
        this.cameras = cameras;
        this.gimbals = gimbals;
        this.dispatcher = dispatcher;
        SetMessages("No payload discovered.");
    }

    /// <summary>Gets discovered payload components.</summary>
    public ObservableRangeCollection<PayloadComponentSelection> Payloads
    {
        get;
    } = [];

    /// <summary>Gets or sets the exact target component.</summary>
    [ObservableProperty]
    public partial PayloadComponentSelection? SelectedPayload
    {
        get;
        set;
    }

    /// <summary>Gets or sets gimbal pitch.</summary>
    [ObservableProperty]
    public partial double Pitch
    {
        get;
        set;
    }

    /// <summary>Gets or sets gimbal yaw.</summary>
    [ObservableProperty]
    public partial double Yaw
    {
        get;
        set;
    }

    /// <summary>Gets or sets earth-frame yaw lock.</summary>
    [ObservableProperty]
    public partial bool YawLock
    {
        get;
        set;
    }

    /// <summary>Refreshes component discovery without assuming fixed IDs.</summary>
    public void Refresh()
    {
        if (active.VehicleId is not { } vehicle)
        {
            return;
        }

        var selected = SelectedPayload?.Key;
        Payloads.Clear();

        foreach (var camera in cameras.GetCameras(vehicle.SystemId))
        {
            Payloads.Add(camera.Component);
        }

        foreach (var gimbal in gimbals.GetGimbals(vehicle.SystemId))
        {
            Payloads.Add(gimbal.Component);
        }

        SelectedPayload = Payloads.FirstOrDefault(item => item.Key == selected) ?? Payloads.FirstOrDefault();
        SetMessages(Payloads.Count == 0 ? "No supported camera or gimbal heartbeat has been discovered." : $"{Payloads.Count} payload component(s) discovered.");
    }

    [RelayCommand]
    private async Task CaptureAsync(CancellationToken token)
    {
        SetMessages(active.VehicleId is { } vehicle && SelectedPayload is { Kind: "Camera" } payload
            ? (await cameras.CaptureImageAsync(vehicle, payload.Key.ComponentId, token)).Summary
            : "Select a camera component.");
    }

    [RelayCommand]
    private async Task StartVideoAsync(CancellationToken token)
    {
        await SetVideoAsync(true, token);
    }

    [RelayCommand]
    private async Task StopVideoAsync(CancellationToken token)
    {
        await SetVideoAsync(false, token);
    }

    [RelayCommand]
    private async Task MoveGimbalAsync(CancellationToken token)
    {
        SetMessages(active.VehicleId is { } vehicle && SelectedPayload is { Kind: "Gimbal" } payload
            ? (await gimbals.SetPitchYawAsync(vehicle, payload.Key.ComponentId, (float)Pitch, (float)Yaw, YawLock, token)).Summary
            : "Select a gimbal component.");
    }

    private async Task SetVideoAsync(bool start, CancellationToken token)
    {
        SetMessages(active.VehicleId is { } vehicle && SelectedPayload is { Kind: "Camera" } payload
            ? (await cameras.SetVideoAsync(vehicle, payload.Key.ComponentId, start, token)).Summary
            : "Select a camera component.");
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Deactivate();
        base.Dispose();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        active.Changed += OnChanged;
        components.Changed += OnChanged;
        Refresh();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Deactivate();
        return Task.CompletedTask;
    }

    private void Deactivate()
    {
        active.Changed -= OnChanged;
        components.Changed -= OnChanged;
    }
    private void OnChanged(EventArgs e)
    {
        dispatcher.Dispatch(Refresh);
    }
    private void OnChanged()
    {
        dispatcher.Dispatch(Refresh);
    }
}
