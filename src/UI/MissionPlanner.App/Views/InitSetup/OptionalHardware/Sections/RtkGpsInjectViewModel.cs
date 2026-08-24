using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Configures an RTCM source and displays active-vehicle injection health.</summary>
public sealed partial class RtkGpsInjectViewModel : OptionalHardwareBaseViewModel
{
    private readonly IRtkInjectionService injection;
    private readonly IFirmwareSerialDeviceCatalog devices;
    private readonly IDispatcher dispatcher;
    private CancellationTokenSource lifetime = new();

    public RtkGpsInjectViewModel(IRtkInjectionService injection, IFirmwareSerialDeviceCatalog devices, IDispatcher dispatcher, ILogger<RtkGpsInjectViewModel> logger)
        : base(logger)
    {
        this.injection = injection;
        this.devices = devices;
        this.dispatcher = dispatcher;
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
        var snapshot = await devices.GetDevicesAsync(lifetime.Token);
        Ports.ReplaceRange(snapshot.Select(s => s.PortName));
        if (SourceKind == RtkSourceKind.Serial && string.IsNullOrEmpty(Endpoint))
        {
            Endpoint = Ports.FirstOrDefault() ?? string.Empty;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        try
        {
            await injection.StartAsync(new RtkSourceOptions(SourceKind, Endpoint, PortOrBaud, MountPoint, Username, Password, UseTls), lifetime.Token);
            Password = string.Empty;
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task DisconnectAsync()
    {
        return injection.StopAsync();
    }

    private void OnChanged(object? sender, RtkInjectionSnapshot snapshot)
    {
        dispatcher.Dispatch(() => Show(snapshot));
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
        injection.Changed += OnChanged;
        Show(injection.Current);
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        injection.Changed -= OnChanged;
        lifetime.Cancel();
        lifetime.Dispose();
        return base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        injection.StopAsync().GetAwaiter().GetResult();
        injection.Dispose();
        base.Dispose();
    }
}
