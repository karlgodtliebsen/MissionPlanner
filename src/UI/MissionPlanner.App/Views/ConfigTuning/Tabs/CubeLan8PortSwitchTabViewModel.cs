using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Configuration.VendorDevices;
using MissionPlanner.Core.Configuration.VendorDevices.CubeLan;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Edits one CubeLAN VLAN membership destination.</summary>
public sealed partial class CubeLanMembershipViewModel : ObservableObject
{
    /// <summary>Initializes a membership editor.</summary>
    /// <param name="configuration">The confirmed membership value.</param>
    public CubeLanMembershipViewModel(CubeLanVlanMembership configuration)
    {
        SourcePort = configuration.SourcePort;
        DestinationPort = configuration.DestinationPort;
        IsMember = configuration.IsMember;
    }

    /// <summary>Gets the source hardware port.</summary>
    public byte SourcePort { get; }

    /// <summary>Gets the destination hardware port.</summary>
    public byte DestinationPort { get; }

    /// <summary>Gets the destination label.</summary>
    public string Label => $"To port {DestinationPort}";

    /// <summary>Gets whether this membership is enabled.</summary>
    [ObservableProperty]
    public partial bool IsMember { get; set; }
}

/// <summary>Edits the verified settings for one CubeLAN hardware port.</summary>
public sealed partial class CubeLanPortViewModel : ObservableObject
{
    /// <summary>Initializes a port editor.</summary>
    /// <param name="configuration">The confirmed port configuration.</param>
    /// <param name="memberships">The port's VLAN destination memberships.</param>
    public CubeLanPortViewModel(
        CubeLanPortConfiguration configuration,
        IEnumerable<CubeLanVlanMembership> memberships)
    {
        PortIndex = configuration.PortIndex;
        ClassOfServiceEnabled = configuration.ClassOfServiceEnabled;
        ClassOfServiceHighPriority = configuration.ClassOfServiceHighPriority;
        EnergyEfficientEthernetEnabled = configuration.EnergyEfficientEthernetEnabled;
        VlanTagged = configuration.VlanTagged;
        Memberships = new ObservableCollection<CubeLanMembershipViewModel>(
            memberships.OrderBy(item => item.DestinationPort).Select(item => new CubeLanMembershipViewModel(item)));
    }

    /// <summary>Gets the zero-based hardware port index.</summary>
    public byte PortIndex { get; }

    /// <summary>Gets the protocol-faithful port label.</summary>
    public string DisplayName => $"Port {PortIndex}";

    /// <summary>Gets whether class-of-service processing is enabled.</summary>
    [ObservableProperty]
    public partial bool ClassOfServiceEnabled { get; set; }

    /// <summary>Gets whether class-of-service high priority is enabled.</summary>
    [ObservableProperty]
    public partial bool ClassOfServiceHighPriority { get; set; }

    /// <summary>Gets whether Energy Efficient Ethernet is enabled.</summary>
    [ObservableProperty]
    public partial bool EnergyEfficientEthernetEnabled { get; set; }

    /// <summary>Gets whether VLAN egress is tagged.</summary>
    [ObservableProperty]
    public partial bool VlanTagged { get; set; }

    /// <summary>Gets the eight VLAN destination memberships.</summary>
    public ObservableCollection<CubeLanMembershipViewModel> Memberships { get; }
}

/// <summary>Coordinates CubeLAN discovery, read-before-edit, confirmed apply, rollback, and export.</summary>
public sealed partial class CubeLan8PortSwitchTabViewModel : ObservableObject, IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVendorDeviceAdapter<CubeLanConfiguration> adapter;
    private readonly ParametersFileHandler fileHandler;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<CubeLan8PortSwitchTabViewModel> logger;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private VendorDeviceSnapshot<CubeLanConfiguration>? original;
    private CancellationTokenSource? operationCancellation;
    private ActiveKey activeKey;
    private bool active;
    private bool loading;
    private bool disposed;

    /// <summary>Initializes the CubeLAN page.</summary>
    /// <param name="activeVehicle">The active-vehicle context.</param>
    /// <param name="adapter">The isolated CubeLAN vendor-device adapter.</param>
    /// <param name="fileHandler">The Config file helper.</param>
    /// <param name="confirmation">The apply confirmation service.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public CubeLan8PortSwitchTabViewModel(
        IActiveVehicleContext activeVehicle,
        IVendorDeviceAdapter<CubeLanConfiguration> adapter,
        ParametersFileHandler fileHandler,
        IUserConfirmationService confirmation,
        IDispatcher dispatcher,
        ILogger<CubeLan8PortSwitchTabViewModel> logger)
    {
        this.activeVehicle = activeVehicle;
        this.adapter = adapter;
        this.fileHandler = fileHandler;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>Gets the eight port editors after successful discovery.</summary>
    public ObservableCollection<CubeLanPortViewModel> Ports { get; } = [];

    /// <summary>Gets the current workflow status.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(IsUnavailable))]
    public partial VendorDeviceStatus Status { get; private set; } = VendorDeviceStatus.NotDiscovered;

    /// <summary>Gets a user-facing discovery or operation message.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; private set; } =
        "Connect a vehicle to discover CubeLAN through the documented MAVLink I²C proxy.";

    /// <summary>Gets the active vehicle heading.</summary>
    [ObservableProperty]
    public partial string VehicleHeading { get; private set; } = "No connected vehicle";

    /// <summary>Gets whether an operation is running.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets whether the local editor differs from the read-before-edit snapshot.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>Gets whether verified settings are available for editing.</summary>
    public bool CanEdit => Status == VendorDeviceStatus.Available && !IsBusy;

    /// <summary>Gets whether no editable device is currently available.</summary>
    public bool IsUnavailable => Status is not VendorDeviceStatus.Available and not VendorDeviceStatus.Busy;

    /// <summary>Begins connection-aware discovery while the page is visible.</summary>
    public void Activate()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (active)
        {
            return;
        }

        active = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        RefreshForActiveVehicle(force: true);
    }

    /// <summary>Stops page observation and cancels its current operation.</summary>
    public void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        CancelOperation();
    }

    /// <summary>Discovers and reads CubeLAN for the current active vehicle.</summary>
    /// <returns>A task representing discovery.</returns>
    [RelayCommand]
    public Task RefreshAsync() => RunAsync(async cancellationToken =>
    {
        var snapshot = activeVehicle.Current;
        if (!snapshot.IsOnline || snapshot.VehicleId is null)
        {
            Clear(VendorDeviceStatus.NotConnected, "Connect a vehicle before discovering CubeLAN.");
            return;
        }

        Status = VendorDeviceStatus.Discovering;
        StatusMessage = "Reading the documented CubeLAN configuration at I²C address 0x50…";
        var result = await adapter.DiscoverAsync(snapshot.VehicleId.Value, null, cancellationToken);
        if (result.Status != VendorDeviceStatus.Available || result.Snapshot is null)
        {
            Clear(result.Status, result.Message);
            return;
        }

        original = result.Snapshot;
        Load(result.Snapshot.Configuration);
        Status = VendorDeviceStatus.Available;
        StatusMessage = result.Message;
    });

    /// <summary>Applies the edited settings and requires confirmed readback.</summary>
    /// <returns>A task representing the apply operation.</returns>
    [RelayCommand]
    public Task ApplyAsync() => RunAsync(async cancellationToken =>
    {
        if (original is null || activeVehicle.VehicleId != original.VehicleId || !activeVehicle.IsOnline)
        {
            Clear(VendorDeviceStatus.NotConnected, "The read-before-edit CubeLAN snapshot is no longer current. Discover again.");
            return;
        }

        var desired = CreateConfiguration();
        var issues = adapter.Validate(desired);
        if (issues.Count != 0)
        {
            StatusMessage = string.Join(" ", issues.Select(issue => issue.Message));
            return;
        }

        if (!await confirmation.ConfirmAsync(
                "Apply CubeLAN configuration?",
                "Only the verified COS, EEE, VLAN tagging, and VLAN membership bits will be written. Every byte is read back; failure triggers rollback.",
                "Apply and verify",
                cancellationToken))
        {
            StatusMessage = "CubeLAN apply cancelled.";
            return;
        }

        Status = VendorDeviceStatus.Busy;
        var result = await adapter.ApplyAsync(original.VehicleId, original, desired, null, cancellationToken);
        if (!result.Success || result.ConfirmedSnapshot is null)
        {
            Status = VendorDeviceStatus.Error;
            StatusMessage = result.Message;
            return;
        }

        original = result.ConfirmedSnapshot;
        Load(result.ConfirmedSnapshot.Configuration);
        Status = result.ConfirmedSnapshot.RequiresReconnect
            ? VendorDeviceStatus.ReconnectRequired
            : VendorDeviceStatus.Available;
        StatusMessage = result.Message;
    });

    /// <summary>Reverts local edits to the last confirmed device snapshot.</summary>
    [RelayCommand]
    public void Revert()
    {
        if (original is null)
        {
            return;
        }

        Load(original.Configuration);
        StatusMessage = "Local CubeLAN edits reverted to the last confirmed readback.";
    }

    /// <summary>Exports the current verified subset without credentials or raw registers.</summary>
    /// <returns>A task representing the file export.</returns>
    [RelayCommand]
    public Task ExportAsync() => RunAsync(async cancellationToken =>
    {
        if (original is null)
        {
            StatusMessage = "Discover CubeLAN before exporting configuration.";
            return;
        }

        var path = await fileHandler.SaveTextFileAsync(
            "cubelan-switch-config.json",
            adapter.Export(CreateConfiguration()),
            cancellationToken);
        StatusMessage = path is null
            ? "CubeLAN export cancelled."
            : $"CubeLAN configuration exported to {path}. Authentication secrets and raw registers are excluded.";
    });

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Deactivate();
        operationGate.Dispose();
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs e) =>
        dispatcher.Dispatch(() => RefreshForActiveVehicle(force: false));

    private void RefreshForActiveVehicle(bool force)
    {
        var nextKey = new ActiveKey(activeVehicle.VehicleId, activeVehicle.IsOnline);
        if (!force && nextKey == activeKey)
        {
            return;
        }

        activeKey = nextKey;
        CancelOperation();
        VehicleHeading = activeVehicle.IsOnline && activeVehicle.State is { } state
            ? $"{state.DisplayName} — CubeLAN via MAVLink DEVICE_OP"
            : "No connected vehicle";
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId is null)
        {
            Clear(VendorDeviceStatus.NotConnected, "Connect a vehicle before discovering CubeLAN.");
            return;
        }

        _ = RefreshAsync();
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        await operationGate.WaitAsync();

        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            activeVehicle.ConnectionCancellationToken);
        IsBusy = true;
        try
        {
            await operation(operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "CubeLAN operation cancelled because the connection changed.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "CubeLAN workflow failed.");
            Status = VendorDeviceStatus.Error;
            StatusMessage = $"CubeLAN operation failed: {exception.Message}";
        }
        finally
        {
            operationCancellation?.Dispose();
            operationCancellation = null;
            IsBusy = false;
            operationGate.Release();
        }
    }

    private void Load(CubeLanConfiguration configuration)
    {
        loading = true;
        try
        {
            foreach (var port in Ports)
            {
                port.PropertyChanged -= OnEditorChanged;
                foreach (var membership in port.Memberships)
                {
                    membership.PropertyChanged -= OnEditorChanged;
                }
            }

            Ports.Clear();
            foreach (var portConfiguration in configuration.Ports.OrderBy(port => port.PortIndex))
            {
                var port = new CubeLanPortViewModel(
                    portConfiguration,
                    configuration.VlanMembership.Where(item => item.SourcePort == portConfiguration.PortIndex));
                port.PropertyChanged += OnEditorChanged;
                foreach (var membership in port.Memberships)
                {
                    membership.PropertyChanged += OnEditorChanged;
                }

                Ports.Add(port);
            }

            IsDirty = false;
        }
        finally
        {
            loading = false;
        }
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!loading)
        {
            IsDirty = original is not null && !Equivalent(CreateConfiguration(), original.Configuration);
        }
    }

    private CubeLanConfiguration CreateConfiguration() => new(
        Ports.Select(port => new CubeLanPortConfiguration(
            port.PortIndex,
            port.ClassOfServiceEnabled,
            port.ClassOfServiceHighPriority,
            port.EnergyEfficientEthernetEnabled,
            port.VlanTagged)).ToArray(),
        Ports.SelectMany(port => port.Memberships.Select(membership => new CubeLanVlanMembership(
            membership.SourcePort,
            membership.DestinationPort,
            membership.IsMember))).ToArray(),
        original?.Configuration.Registers ?? []);

    private static bool Equivalent(CubeLanConfiguration first, CubeLanConfiguration second) =>
        first.Ports.SequenceEqual(second.Ports) && first.VlanMembership.SequenceEqual(second.VlanMembership);

    private void Clear(VendorDeviceStatus status, string message)
    {
        original = null;
        Ports.Clear();
        IsDirty = false;
        Status = status;
        StatusMessage = message;
    }

    private void CancelOperation()
    {
        operationCancellation?.Cancel();
    }

    private readonly record struct ActiveKey(VehicleId? VehicleId, bool IsOnline);
}
