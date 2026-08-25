using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// Provides the searchable full parameter list through the shared safe editing session.
/// </summary>
public partial class VehicleConnectionViewModel : BaseViewModel
{
    private const string DefaultStatusMessage = "Connect a vehicle.";
    private readonly IVehicleConnectionSession connectionSession;
    private readonly IActiveVehicleContext activeVehicle;
    //private readonly IDomainEventHub domainEventHub;
    //private readonly IExtendedDialogService dialogService;
    //private readonly IDomainFactory domainFactory;
    private readonly ILogger logger;

    private readonly IDispatcher dispatcher;
    private bool disposed;
    private bool activated;


    /// <summary>
    /// Gets whether the active vehicle is disconnected.
    /// </summary>
    [ObservableProperty]
    public virtual partial bool ShowVehicleDisconnected
    {
        get; set;
    }

    /// <summary>Gets whether an active vehicle connection is available.</summary>
    [ObservableProperty]
    public virtual partial bool HasConnection
    {
        get; set;
    }

    /// <summary>Initializes the Full Parameters List tab.</summary>
    /// <param name="connectionSession">The current connection-scoped services.</param>
    /// <param name="activeVehicle">The application active-vehicle context.</param>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="logger">The logger.</param>
    protected VehicleConnectionViewModel(
        IVehicleConnectionSession connectionSession,
        IActiveVehicleContext activeVehicle,
        IDispatcher dispatcher,
        ILogger logger) : base(logger)
    {
        this.connectionSession = connectionSession;
        this.activeVehicle = activeVehicle;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleChangedEventArgs"></param>
    protected virtual Task OnActiveVehicleChanged(ActiveVehicleChangedEventArgs vehicleChangedEventArgs)
    {
        var scopeChanged =
            vehicleChangedEventArgs.Previous.VehicleId != vehicleChangedEventArgs.Current.VehicleId ||
            vehicleChangedEventArgs.Previous.IsOnline != vehicleChangedEventArgs.Current.IsOnline ||
            vehicleChangedEventArgs.Previous.State?.Identity.Firmware != vehicleChangedEventArgs.Current.State?.Identity.Firmware;
        if (!scopeChanged)
        {
            return Task.CompletedTask;
        }

        var changed = vehicleChangedEventArgs.Current.IsOnline;

        dispatcher.Dispatch(() =>
        {
            HasConnection = changed;
            ShowVehicleDisconnected = !changed;
            var statusMessage = changed ? "Vehicle changed." : null;
            var errorMessage = changed ? null : "The vehicle is disconnected.";
            Debug.Assert(statusMessage is null || errorMessage is null);
            SetMessages(statusMessage, errorMessage);
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DeactivateAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (disposed)
        {
            return Task.CompletedTask;
        }
        if (activated)
        {
            return Task.CompletedTask;
        }

        activated = true;
        ErrorMessage = null;
        activeVehicle.Changed += VehicleChanged;
        HasConnection = activeVehicle.IsOnline;
        ShowVehicleDisconnected = !HasConnection;
        StatusMessage = HasConnection ? null : DefaultStatusMessage;
        return Task.CompletedTask;
    }

    private void VehicleChanged(ActiveVehicleChangedEventArgs e)
    {
        OnActiveVehicleChanged(e).GetAwaiter().GetResult();
    }


    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        if (!activated)
        {
            return Task.CompletedTask;
        }

        activated = false;
        activeVehicle.Changed -= VehicleChanged;
        return Task.CompletedTask;
    }
}
