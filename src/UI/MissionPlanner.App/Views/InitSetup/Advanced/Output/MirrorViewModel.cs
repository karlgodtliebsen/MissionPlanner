using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Output;

/// <summary>Coordinates one explicitly started mirror and its reusable endpoint/status panels.</summary>
public sealed partial class MirrorViewModel(OutputEndpointViewModel endpoint, OutputStatusViewModel status,
    MirrorSession session, IDialogService dialogs, TimeProvider clock, ILogger<MirrorViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? lifetime;
    private CancellationTokenSource? run;
    private Task updates = Task.CompletedTask;
    private Task starting = Task.CompletedTask;
    /// <summary>Gets the endpoint editor.</summary>
    public OutputEndpointViewModel Endpoint => endpoint;
    /// <summary>Gets the coalesced status panel.</summary>
    public OutputStatusViewModel Status => status;
    /// <summary>Gets selectable directions.</summary>
    public IReadOnlyList<MirrorDirection> Directions { get; } = Enum.GetValues<MirrorDirection>();
    /// <summary>Gets or sets which authoritative frames to forward.</summary>
    [ObservableProperty] public partial MirrorDirection Direction { get; set; }
    /// <summary>Gets the current operation explanation.</summary>
    [ObservableProperty] public partial string Message { get; private set; } = "Choose a separate output endpoint. Forwarded telemetry may contain sensitive information.";
    /// <summary>Gets whether start is pending or a session has been started.</summary>
    [ObservableProperty] public partial bool Running { get; private set; }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (lifetime is not null) { return; }
            lifetime = new();
            endpoint.ProfileChanged += ProfileChanged;
            updates = UpdateAsync(lifetime.Token);
        }
        finally { gate.Release(); }
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime?.Cancel();
        await gate.WaitAsync();
        try
        {
            if (lifetime is null) { return; }
            endpoint.ProfileChanged -= ProfileChanged;
            await starting;
            await session.StopAsync();
            run?.Dispose();
            run = null;
            await updates;
            lifetime.Dispose();
            lifetime = null;
            Running = endpoint.Locked = false;
            status.Snapshot = session.Snapshot();
        }
        finally { gate.Release(); }
    }

    private void ProfileChanged(OutputEndpoint profile) => Message = endpoint.Validate() ?? $"Ready to open {profile.Summary}.";
    [RelayCommand] private Task StartAsync()
    {
        if (Running || lifetime is null) { return Task.CompletedTask; }
        run?.Dispose();
        run = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        starting = StartCoreAsync(run.Token);
        return starting;
    }

    private async Task StartCoreAsync(CancellationToken token)
    {
        Running = endpoint.Locked = true;
        try
        {
            if (endpoint.Validate() is { } error) { Message = error; Running = endpoint.Locked = false; return; }
            var profile = endpoint.Profile();
            var options = endpoint.Options();
            var direction = Direction;
            var confirmed = direction != MirrorDirection.Both || await dialogs.ConfirmAsync(
                dialogs.CreateOptions("Mirror both directions", "Start mirror", "Cancel"),
                "Do not route this output back to the vehicle input. Recent byte-identical frames are suppressed to interrupt reflection loops. Both directions may expose sent commands and telemetry. Continue?", token);
            token.ThrowIfCancellationRequested();
            if (!confirmed) { Running = endpoint.Locked = false; Message = "Mirror start cancelled."; return; }
            await session.StartAsync(profile, options, direction, confirmed, token);
            Message = "Mirroring started. No inbound endpoint reader or vehicle return path is created.";
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Mirror start did not complete ({FailureType}).", exception.GetType().Name);
            Message = "Mirror could not start. Check endpoint ownership, connection and platform permissions.";
            Running = endpoint.Locked = false;
        }
    }

    [RelayCommand] private async Task StopAsync()
    {
        run?.Cancel();
        await starting;
        await session.StopAsync();
        run?.Dispose();
        run = null;
        Running = endpoint.Locked = false;
        status.Snapshot = session.Snapshot();
        Message = "Mirror stopped and output handle released.";
    }

    private async Task UpdateAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250), clock);
            do
            {
                var snapshot = session.Snapshot();
                await Dispatcher.DispatchAsync(() => { if (!token.IsCancellationRequested) { status.Snapshot = snapshot; } });
            } while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    /// <inheritdoc />
    public override void Dispose() { lifetime?.Cancel(); _ = CloseAsync(); base.Dispose(); }
    private async Task CloseAsync()
    {
        try { await DeactivateAsync(); }
        catch (Exception exception) { Logger.LogWarning("Mirror cleanup failed ({FailureType}).", exception.GetType().Name); }
    }
}
