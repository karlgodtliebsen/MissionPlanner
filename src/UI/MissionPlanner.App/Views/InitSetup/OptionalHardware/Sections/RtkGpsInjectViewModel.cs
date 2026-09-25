using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Firmware.Devices;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Configures an RTCM source and displays active-vehicle injection health.</summary>
public sealed partial class RtkGpsInjectViewModel : OptionalHardwareBaseViewModel
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
        Show(injection.Current);
        return RefreshPortsAsync();
    }

    /// <summary>Opens the shared Full Parameters workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await navigation.NavigateAsync(MissionPlannerRoutes.ConfigFullParameters);
    }

    private readonly INavigationService navigation;

    private readonly IRtkInjectionService injection;
    private readonly IFirmwareSerialDeviceCatalog devices;
    private CancellationTokenSource lifetime = new();
    private bool active;

    public RtkGpsInjectViewModel(IRtkInjectionService injection, IFirmwareSerialDeviceCatalog devices, ILogger<RtkGpsInjectViewModel> logger, INavigationService navigation)
        : base(logger)
    {
        this.navigation = navigation;
        this.injection = injection;
        this.devices = devices;
    }

    public ObservableRangeCollection<string> Ports { get; } = [];
    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<RtkSourceKind> SourceKinds { get; } = Enum.GetValues<RtkSourceKind>();

    [ObservableProperty]
    public partial RtkSourceKind SourceKind
    {
        get; set;
    }
    [ObservableProperty] public partial string Endpoint { get; set; } = string.Empty;
    [ObservableProperty] public partial int PortOrBaud { get; set; } = 2101;
    [ObservableProperty] public partial string MountPoint { get; set; } = string.Empty;
    [ObservableProperty] public partial string Username { get; set; } = string.Empty;
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool UseTls
    {
        get; set;
    }

    [ObservableProperty] public partial string SourceStatus { get; private set; } = string.Empty;
    [ObservableProperty] public partial string TargetStatus { get; private set; } = string.Empty;
    [ObservableProperty] public partial string Statistics { get; private set; } = string.Empty;

    [RelayCommand]
    private async Task RefreshPortsAsync()
    {
        var token = lifetime.Token;
        try
        {
            var snapshot = await devices.GetDevicesAsync(token);
            token.ThrowIfCancellationRequested();
            Ports.ReplaceRange(snapshot.Select(s => s.PortName));
            if (SourceKind == RtkSourceKind.Serial && string.IsNullOrEmpty(Endpoint))
            {
                Endpoint = Ports.FirstOrDefault() ?? string.Empty;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        SetBusy();
        try
        {
            await injection.StartAsync(new RtkSourceOptions(SourceKind, Endpoint, PortOrBaud, MountPoint, Username, Password, UseTls), lifetime.Token);
            Password = string.Empty;
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { SetMessages(exception); }
        finally { ResetBusy(); }
    }

    [RelayCommand]
    private Task DisconnectAsync()
    {
        return injection.StopAsync();
    }

    private void OnChanged(object? sender, RtkInjectionSnapshot snapshot)
    {
        Dispatcher.Dispatch(() => Show(snapshot));
    }

    private void Show(RtkInjectionSnapshot snapshot)
    {
        SourceStatus = snapshot.SourceStatus;
        TargetStatus = snapshot.TargetStatus;
        Statistics = $"RTCM frames: {snapshot.FramesSeen} · MAVLink packets: {snapshot.PacketsSent} · Last correction: {snapshot.LastCorrection?.ToLocalTime():T}";
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        lifetime = new();
        active = true;
        injection.Changed += OnChanged;
        Show(injection.Current);
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        Deactivate();
        await injection.StopAsync();
        await base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Deactivate();
        injection.Dispose();
        base.Dispose();
    }

    private void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        injection.Changed -= OnChanged;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}

