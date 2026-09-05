using System.ComponentModel;
using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities;
using MissionPlanner.Core.ConfigTuning.VendorDevices;
using MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Coordinates CubeLAN discovery, read-before-edit, confirmed apply, rollback, and export.</summary>
public sealed partial class CubeLan8PortSwitchTabViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVendorDeviceAdapter<CubeLanConfiguration> adapter;
    private readonly ParametersFileHandler fileHandler;
    private readonly IUserConfirmationService confirmation;
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
    /// <param name="logger">The logger.</param>
    public CubeLan8PortSwitchTabViewModel(
        IActiveVehicleContext activeVehicle,
        IVendorDeviceAdapter<CubeLanConfiguration> adapter,
        ParametersFileHandler fileHandler,
        IUserConfirmationService confirmation, ILogger<CubeLan8PortSwitchTabViewModel> logger) : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.adapter = adapter;
        this.fileHandler = fileHandler;
        this.confirmation = confirmation;
    }

    /// <summary>Gets the eight port editors after successful discovery.</summary>
    public ObservableRangeCollection<CubeLanPortViewModel> Ports { get; } = [];

    /// <summary>Gets the current workflow status.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(IsUnavailable))]
    public partial VendorDeviceStatus Status { get; private set; } = VendorDeviceStatus.NotDiscovered;



    /// <summary>Gets the active vehicle heading.</summary>
    [ObservableProperty]
    public partial string VehicleHeading
    {
        get;
        private set;
    } = "No connected vehicle";

    /// <summary>Gets whether the local editor differs from the read-before-edit snapshot.</summary>
    [ObservableProperty]
    public partial bool IsDirty
    {
        get;
        private set;
    }

    /// <summary>Gets whether verified settings are available for editing.</summary>
    public bool CanEdit => Status == VendorDeviceStatus.Available && !IsBusy;

    /// <summary>Gets whether no editable device is currently available.</summary>
    public bool IsUnavailable => Status is not VendorDeviceStatus.Available and not VendorDeviceStatus.Busy;

    /// <summary>Discovers and reads CubeLAN for the current active vehicle.</summary>
    /// <returns>A task representing discovery.</returns>
    [RelayCommand]
    public Task RefreshAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            var snapshot = activeVehicle.Current;
            if (!snapshot.IsOnline || snapshot.VehicleId is null)
            {
                Clear(VendorDeviceStatus.NotConnected, "Connect a vehicle before discovering CubeLAN.");
                return;
            }

            Status = VendorDeviceStatus.Discovering;
            SetMessages("Reading the documented CubeLAN configuration at I²C address 0x50…");
            var result = await adapter.DiscoverAsync(snapshot.VehicleId.Value, null, cancellationToken);
            if (result.Status != VendorDeviceStatus.Available || result.Snapshot is null)
            {
                Clear(result.Status, result.Message);
                return;
            }

            original = result.Snapshot;
            Load(result.Snapshot.Configuration);
            Status = VendorDeviceStatus.Available;
            SetMessages(result.Message);
            NotificationManager?.Show(StatusMessage!);
        });
    }

    /// <summary>Applies the edited settings and requires confirmed readback.</summary>
    /// <returns>A task representing the apply operation.</returns>
    [RelayCommand]
    public Task ApplyAsync()
    {
        return RunAsync(async cancellationToken =>
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
                SetMessages(string.Join(" ", issues.Select(issue => issue.Message)));
                NotificationManager!.Show(StatusMessage!);
                return;
            }

            if (!await confirmation.ConfirmAsync(
                    "Apply CubeLAN configuration?",
                    "Only the verified COS, EEE, VLAN tagging, and VLAN membership bits will be written. Every byte is read back; failure triggers rollback.",
                    "Apply and verify",
                    cancellationToken))
            {
                SetMessages("CubeLAN apply cancelled.");
                NotificationManager?.Show(StatusMessage!);
                return;
            }

            Status = VendorDeviceStatus.Busy;
            var result = await adapter.ApplyAsync(original.VehicleId, original, desired, null, cancellationToken);
            if (!result.Success || result.ConfirmedSnapshot is null)
            {
                Status = VendorDeviceStatus.Error;
                SetMessages(result.Message);
                return;
            }

            original = result.ConfirmedSnapshot;
            Load(result.ConfirmedSnapshot.Configuration);
            Status = result.ConfirmedSnapshot.RequiresReconnect
                ? VendorDeviceStatus.ReconnectRequired
                : VendorDeviceStatus.Available;
            SetMessages(result.Message);
            NotificationManager?.Show(StatusMessage!);
        });
    }

    /// <summary>Reverts local edits to the last confirmed device snapshot.</summary>
    [RelayCommand]
    public void Revert()
    {
        if (original is null)
        {
            return;
        }

        Load(original.Configuration);
        SetMessages("Local CubeLAN edits reverted to the last confirmed readback.");
        NotificationManager?.Show(StatusMessage!);
    }

    /// <summary>Exports the current verified subset without credentials or raw registers.</summary>
    /// <returns>A task representing the file export.</returns>
    [RelayCommand]
    public Task ExportAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            if (original is null)
            {
                SetMessages("Discover CubeLAN before exporting configuration.");
                NotificationManager?.Show(StatusMessage!);
                return;
            }

            var path = await fileHandler.SaveTextFileAsync("cubelan-switch-config.json", adapter.Export(CreateConfiguration()), cancellationToken);
            SetMessages(path is null
                ? "CubeLAN export cancelled."
                : $"CubeLAN configuration exported to {path}. Authentication secrets and raw registers are excluded.");
            NotificationManager?.Show(StatusMessage!);
        });
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Deactivate();
        disposed = true;
        operationGate.Dispose();
        base.Dispose();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (active)
        {
            return Task.CompletedTask;
        }

        active = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        RefreshForActiveVehicle(true);
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
        if (!active)
        {
            return;
        }

        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        CancelOperation();
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs e)
    {
        Dispatcher.Dispatch(() => RefreshForActiveVehicle(false));
    }

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

        RefreshAsync().SafeFireAndForget();
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        await operationGate.WaitAsync();

        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        SetBusy();
        try
        {
            await operation(operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            SetMessages("CubeLAN operation cancelled because the connection changed.");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "CubeLAN workflow failed.");
            Status = VendorDeviceStatus.Error;
            SetMessages(errorMessage: $"CubeLAN operation failed: {exception.Message}");
            NotificationManager?.Show(ErrorMessage!);
        }
        finally
        {
            operationCancellation?.Dispose();
            operationCancellation = null;
            ResetBusy();
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

            var ports = new List<CubeLanPortViewModel>();
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

                ports.Add(port);
            }

            Ports.ReplaceRange(ports);

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

    private CubeLanConfiguration CreateConfiguration()
    {
        return new CubeLanConfiguration(
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
    }

    private static bool Equivalent(CubeLanConfiguration first, CubeLanConfiguration second)
    {
        return first.Ports.SequenceEqual(second.Ports) && first.VlanMembership.SequenceEqual(second.VlanMembership);
    }

    private void Clear(VendorDeviceStatus status, string message)
    {
        original = null;
        Ports.Clear();
        IsDirty = false;
        Status = status;
        SetMessages(message);
        NotificationManager!.Show(StatusMessage!);
    }

    private void CancelOperation()
    {
        operationCancellation?.Cancel();
    }

    private readonly record struct ActiveKey(VehicleId? VehicleId, bool IsOnline);
}
