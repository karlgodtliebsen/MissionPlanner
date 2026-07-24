using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Creates and tracks the shared active-vehicle parameter editing session.</summary>
public sealed class ParameterEditSessionFactory : IParameterEditSessionFactory, IDisposable
{
    private readonly object sync = new();
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IDomainFactory factory;

    private readonly ILogger<ParameterEditSessionFactory> logger;
    private IParameterEditSession? current;
    private bool disposed;

    /// <summary>Initializes a shared parameter-editing session factory.</summary>
    /// <param name="activeVehicle">The application active-vehicle context.</param>
    /// <param name="factory"></param>
    /// <param name="logger">The logger.</param>
    public ParameterEditSessionFactory(IActiveVehicleContext activeVehicle, IDomainFactory factory, ILogger<ParameterEditSessionFactory> logger)
    {
        this.activeVehicle = activeVehicle;
        this.factory = factory;
        this.logger = logger;
        activeVehicle.Changed += OnActiveVehicleChanged;
    }

    /// <inheritdoc />
    public bool HasUnappliedChanges
    {
        get
        {
            lock (sync)
            {
                return current?.IsDirty == true;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IParameterEditSession Create(VehicleId vehicleId)
    {
        IParameterEditSession? replaced = null;
        IParameterEditSession result;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var snapshot = activeVehicle.Current;
            if (!snapshot.IsOnline || snapshot.VehicleId != vehicleId || snapshot.State is null)
            {
                throw new InvalidOperationException("A parameter editing session requires the target vehicle to be active and online.");
            }

            var scope = new ParameterEditScope(vehicleId, snapshot.State.Identity.Firmware);
            if (current is { IsValid: true } && current.Scope == scope)
            {
                return current;
            }

            if (current?.IsDirty == true)
            {
                throw new InvalidOperationException("The previous parameter session has unapplied changes. Revert them before opening a different vehicle or firmware session.");
            }

            replaced = current;
            replaced?.Changed -= OnSessionChanged;

            result = factory.Create<IParameterEditSession, ParameterEditScope>(scope);
            result.Changed += OnSessionChanged;
            current = result;
        }

        replaced?.Dispose();
        Changed?.Invoke(this, EventArgs.Empty);
        return result;
    }

    /// <inheritdoc />
    public void DiscardPendingChanges()
    {
        IParameterEditSession? session;
        lock (sync)
        {
            session = current;
        }

        session?.RevertAll();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IParameterEditSession? session;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            session = current;
            current = null;
            session?.Changed -= OnSessionChanged;
        }

        activeVehicle.Changed -= OnActiveVehicleChanged;
        session?.Dispose();
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        IParameterEditSession? session;
        lock (sync)
        {
            session = current;
        }

        if (session is null ||
            (args.Current.IsOnline &&
             args.Current.VehicleId == session.VehicleId &&
             args.Current.State?.Identity.Firmware == session.Scope.FirmwareIdentity))
        {
            return;
        }

        logger.LogWarning("The active vehicle connection or firmware identity changed. Revert these stale edits and reload before writing.");
        session.Invalidate("The active vehicle connection or firmware identity changed. Revert these stale edits and reload before writing.");
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
