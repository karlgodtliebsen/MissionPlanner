using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Edits discovered serial parameters through the shared vehicle-scoped parameter session.</summary>
public sealed partial class SerialPortsViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry registry;
    private readonly IParameterEditSessionFactory sessions;
    private IParameterEditSession? session;
    private CancellationTokenSource? lifetime;
    private bool active;
    private int generation;
    private int reloadScheduled;

    /// <summary>Initializes the serial page with existing parameter, lifecycle and UI services.</summary>
    public SerialPortsViewModel(IActiveVehicleContext activeVehicle, IVehicleParameterRegistry registry,
        IParameterEditSessionFactory sessions, ILogger<SerialPortsViewModel> logger,
        IUiDispatcher dispatcher, IDomainEventHub events) : base(logger, dispatcher, events)
    {
        this.activeVehicle = activeVehicle;
        this.registry = registry;
        this.sessions = sessions;
    }

    /// <summary>Gets sparse serial rows in numeric index order.</summary>
    public ObservableRangeCollection<SerialPortRowViewModel> Ports { get; } = [];

    /// <summary>Gets whether the current session is available for user edits.</summary>
    [ObservableProperty]
    public partial bool CanEdit { get; private set; }

    /// <summary>Gets receiver-configuration advice, including pending edits.</summary>
    [ObservableProperty]
    public partial string Diagnostic { get; private set; } = string.Empty;

    /// <summary>Gets whether a confirmed serial field in the shared session requires reboot.</summary>
    [ObservableProperty]
    public partial bool RebootRequired { get; private set; }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (active)
        {
            return;
        }
        active = true;
        activeVehicle.Changed += VehicleChanged;
        registry.Changed += ParameterChanged;
        await RefreshAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        active = false;
        generation++;
        activeVehicle.Changed -= VehicleChanged;
        registry.Changed -= ParameterChanged;
        DetachSession();
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        CanEdit = false;
        Ports.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _ = DeactivateAsync();
        base.Dispose();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!active)
        {
            return;
        }
        var currentGeneration = ++generation;
        DetachSession();
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        var token = lifetime.Token;
        CanEdit = false;
        Diagnostic = string.Empty;
        RebootRequired = false;
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId is not { } id)
        {
            Ports.Clear();
            ResetBusy();
            SetMessages("Connect a vehicle to configure its serial ports.");
            return;
        }
        try
        {
            SetBusy();
            var nextSession = sessions.Create(id);
            var names = SerialPortConfiguration.Discover(registry.GetAllParameters(id).Keys)
                .SelectMany(port => port.Names).Append("RC_OPTIONS").ToArray();
            await nextSession.LoadAsync(names, token);
            token.ThrowIfCancellationRequested();
            if (!active || generation != currentGeneration || activeVehicle.VehicleId != id || !activeVehicle.IsOnline)
            {
                return;
            }
            session = nextSession;
            session.FieldChanged += SessionChanged;
            Ports.Clear();
            Synchronize();
            SetMessages(Ports.Count == 0 ? "No SERIAL parameter groups have been reported." :
                "Edit configured values, then Apply changes. Current readback values are shown separately.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (generation == currentGeneration)
            {
                Ports.Clear();
                SetMessages(errorMessage: exception.Message);
            }
        }
        finally
        {
            if (generation == currentGeneration)
            {
                ResetBusy();
                CanEdit = session?.IsValid == true && activeVehicle.IsOnline;
                if (session is not null)
                {
                    QueueReloadIfMissing();
                }
            }
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        var current = session;
        var currentGeneration = generation;
        if (!active || IsBusy || !CanEdit || current?.IsValid != true ||
            !activeVehicle.IsOnline || current.VehicleId != activeVehicle.VehicleId || lifetime is null)
        {
            return;
        }
        var token = lifetime.Token;
        try
        {
            SetBusy();
            CanEdit = false;
            var report = await current.ApplyAsync(Ports.SelectMany(port => port.Configuration.Names).ToArray(), token);
            if (!active || generation != currentGeneration || session != current)
            {
                return;
            }
            Synchronize();
            SetMessages(report.Success ? $"Confirmed {report.Confirmed.Count} changed serial parameters." : null,
                report.Success ? null : string.Join(Environment.NewLine, report.Results
                    .Where(result => result.Outcome != ParameterWriteOutcome.Confirmed).Select(result => $"{result.Name}: {result.Message}")));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (generation == currentGeneration)
            {
                SetMessages(errorMessage: exception.Message);
            }
        }
        finally
        {
            if (generation == currentGeneration)
            {
                ResetBusy();
                CanEdit = current.IsValid && activeVehicle.IsOnline;
                QueueReloadIfMissing();
            }
        }
    }

    private void Synchronize()
    {
        if (!active || session is null || activeVehicle.VehicleId != session.VehicleId)
        {
            return;
        }
        var configurations = SerialPortConfiguration.Discover(registry.GetAllParameters(session.VehicleId).Keys);
        if (!Ports.Select(port => port.Configuration).SequenceEqual(configurations))
        {
            Ports.ReplaceRange(configurations.Select(port => new SerialPortRowViewModel(port, session)));
        }
        foreach (var port in Ports)
        {
            port.Synchronize();
        }
        Diagnostic = SerialPortConfiguration.Diagnose(session.Fields.Where(field =>
            registry.GetParameter(session.VehicleId, field.Name) is not null));
        RebootRequired = session.Fields.Any(field => SerialPortConfiguration.TryParseIndex(field.Name, out _) &&
            field.Metadata.RebootRequired && field.WriteStatus == ParameterEditWriteStatus.Confirmed);
    }

    private void SessionChanged(string? name) => Dispatcher.Dispatch(Synchronize);

    private void VehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.Dispatch(() =>
        {
            Ports.Clear();
            _ = RefreshAsync();
        });
    }

    private void ParameterChanged(VehicleParameterChangedEventArgs args)
    {
        if (active && args.VehicleId == activeVehicle.VehicleId)
        {
            Dispatcher.Dispatch(QueueReloadIfMissing);
        }
    }

    private void QueueReloadIfMissing()
    {
        if (IsBusy || !active || !activeVehicle.IsOnline || activeVehicle.VehicleId is not { } id)
        {
            return;
        }
        var names = registry.GetAllParameters(id).Keys;
        if (session is null || !Ports.Select(port => port.Configuration).SequenceEqual(SerialPortConfiguration.Discover(names)) ||
            SerialPortConfiguration.Discover(names).SelectMany(port => port.Names)
            .Append("RC_OPTIONS").Any(name => registry.GetParameter(id, name) is not null && session.GetField(name) is null))
        {
            QueueRefresh();
        }
    }

    private void QueueRefresh()
    {
        if (Interlocked.Exchange(ref reloadScheduled, 1) == 0)
        {
            _ = RefreshQueuedAsync();
        }
    }

    private async Task RefreshQueuedAsync()
    {
        try
        {
            await RefreshAsync();
        }
        finally
        {
            Interlocked.Exchange(ref reloadScheduled, 0);
            if (session is not null)
            {
                QueueReloadIfMissing();
            }
        }
    }

    private void DetachSession()
    {
        if (session is not null)
        {
            session.FieldChanged -= SessionChanged;
            session = null;
        }
    }
}
