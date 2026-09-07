using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.Advanced.Output;
using MissionPlanner.Core.Setup.Advanced.Nmea;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Nmea;

/// <summary>Coordinates NMEA settings, shared endpoint output and coalesced preview panels.</summary>
public sealed partial class NmeaViewModel(NmeaOptionsViewModel settings, NmeaPreviewViewModel preview,
    OutputEndpointViewModel endpoint, OutputStatusViewModel status, NmeaSession session, TimeProvider clock,
    ILogger<NmeaViewModel> logger, IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? lifetime;
    private CancellationTokenSource? run;
    private Task updates = Task.CompletedTask;
    private Task starting = Task.CompletedTask;
    /// <summary>Gets sentence settings.</summary>
    public NmeaOptionsViewModel Settings => settings;
    /// <summary>Gets the live preview.</summary>
    public NmeaPreviewViewModel Preview => preview;
    /// <summary>Gets the shared endpoint editor.</summary>
    public OutputEndpointViewModel Endpoint => endpoint;
    /// <summary>Gets shared endpoint diagnostics.</summary>
    public OutputStatusViewModel Status => status;
    /// <summary>Gets whether this page owns a running or pending output session.</summary>
    [ObservableProperty] public partial bool Running { get; private set; }
    /// <summary>Gets an actionable status message.</summary>
    [ObservableProperty] public partial string Message { get; private set; } = "Select GGA/RMC sentences and an output-only endpoint.";

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (lifetime is not null) { return; }
            lifetime = new();
            settings.OptionsChanged += OptionsChanged;
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
            settings.OptionsChanged -= OptionsChanged;
            endpoint.ProfileChanged -= ProfileChanged;
            await starting;
            await session.StopAsync();
            await updates;
            run?.Dispose();
            run = null;
            lifetime.Dispose();
            lifetime = null;
            Running = settings.Locked = endpoint.Locked = false;
            Refresh();
        }
        finally { gate.Release(); }
    }

    private void OptionsChanged(NmeaOptions options)
    {
        try { options.Validate(); Message = "Sentence settings ready; start to apply them."; }
        catch (ArgumentException) { Message = "Select at least one sentence and a rate from 1 to 10 Hz."; }
    }
    private void ProfileChanged(OutputEndpoint profile) => Message = endpoint.Validate() ?? $"Output endpoint ready: {profile.Summary}.";

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
        Running = settings.Locked = endpoint.Locked = true;
        try
        {
            if (endpoint.Validate() is { } error) { throw new InvalidOperationException(error); }
            await session.StartAsync(endpoint.Profile(), endpoint.Options(), settings.Options(), token);
            Message = "NMEA output started. Unknown fields remain blank and stale fixes are explicitly invalid.";
        }
        catch (Exception exception)
        {
            Logger.LogWarning("NMEA start did not complete ({FailureType}).", exception.GetType().Name);
            Message = "NMEA could not start. Check sentence selection, rate, connection and endpoint permissions.";
            Running = settings.Locked = endpoint.Locked = false;
        }
    }
    [RelayCommand] private async Task StopAsync()
    {
        run?.Cancel();
        await starting;
        await session.StopAsync();
        run?.Dispose();
        run = null;
        Running = settings.Locked = endpoint.Locked = false;
        Refresh();
        Message = "NMEA output stopped and endpoint released.";
    }
    private void Refresh()
    {
        preview.Batch = session.Preview;
        preview.Counters = $"Sentences scheduled: {session.ScheduledSentences} · written: {session.WrittenSentences} · dropped: {session.DroppedSentences}";
        status.Snapshot = session.Snapshot();
    }
    private async Task UpdateAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250), clock);
            do { await Dispatcher.DispatchAsync(() => { if (!token.IsCancellationRequested) { Refresh(); } }); }
            while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    /// <inheritdoc />
    public override void Dispose() { lifetime?.Cancel(); _ = CloseAsync(); base.Dispose(); }
    private async Task CloseAsync()
    {
        try { await DeactivateAsync(); }
        catch (Exception exception) { Logger.LogWarning("NMEA cleanup failed ({FailureType}).", exception.GetType().Name); }
    }
}
