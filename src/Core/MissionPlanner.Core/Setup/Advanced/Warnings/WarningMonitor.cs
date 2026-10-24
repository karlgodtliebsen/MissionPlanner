using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Advanced.Warnings;

/// <summary>Owns warning evaluation for the application lifetime, independently of navigation.</summary>
public sealed class WarningMonitor(WarningRuleRepository repository, WarningSources sources,
    IActiveVehicleContext vehicle, TimeProvider clock, IUserNotificationService notifications,
    ILogger<WarningMonitor> logger) : IDisposable, IAsyncDisposable
{
    private readonly Lock sync = new();
    private readonly SemaphoreSlim startGate = new(1, 1);
    private readonly WarningEngine engine = new(sources, clock);
    private readonly CancellationTokenSource lifetime = new();
    private IReadOnlyList<WarningRule> rules = [];
    private IReadOnlyList<WarningSnapshot> snapshots = [];
    private Task? polling;
    private VehicleId? evaluatedVehicle;
    private CancellationToken evaluatedConnection;

    /// <summary>Gets the latest immutable results, including unavailable and acknowledged rules.</summary>
    public IReadOnlyList<WarningSnapshot> Snapshots
    {
        get
        {
            lock (sync)
            {
                return snapshots;
            }
        }
    }

    /// <summary>Loads saved rules once and starts an owned, cancellable evaluation loop.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (polling is not null)
            {
                return;
            }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            var loaded = await repository.LoadAsync(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            UpdateRules(loaded.Rules);
            repository.Saved += UpdateRules;
            polling = PollAsync(lifetime.Token);
            if (!string.IsNullOrWhiteSpace(loaded.Diagnostic))
            {
                await notifications.NotifyAsync(new(loaded.Diagnostic, "Warning rules", UserNotificationSeverity.Warning), linked.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            startGate.Release();
        }
    }

    /// <summary>Acknowledges the live rule without depending on whether its editor is open.</summary>
    public void Acknowledge(Guid id)
    {
        lock (sync)
        {
            engine.Acknowledge(id);
        }
    }

    /// <summary>Stops monitoring and detaches persistence events.</summary>
    public void Dispose()
    {
        repository.Saved -= UpdateRules;
        lifetime.Cancel();
    }

    /// <summary>Stops monitoring and waits until pending delivery has ended.</summary>
    public async ValueTask DisposeAsync()
    {
        Dispose();
        if (polling is not null)
        {
            await polling.ConfigureAwait(false);
        }
    }

    private void UpdateRules(IReadOnlyList<WarningRule> updated)
    {
        lock (sync)
        {
            rules = updated.ToArray();
            engine.SetRules(rules);
        }
    }

    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250), clock);
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                List<UserNotification> delivery = [];
                lock (sync)
                {
                    var state = vehicle.IsOnline ? vehicle.State : null;
                    if (evaluatedVehicle != state?.VehicleId || evaluatedConnection != vehicle.ConnectionCancellationToken)
                    {
                        evaluatedVehicle = state?.VehicleId;
                        evaluatedConnection = vehicle.ConnectionCancellationToken;
                        engine.SetRules([]);
                        engine.SetRules(rules);
                    }
                    snapshots = engine.Evaluate(key => sources.Read(key, state));
                    foreach (var snapshot in snapshots.Where(item => item.Notify))
                    {
                        var rule = rules.First(item => item.Id == snapshot.RuleId);
                        if (rule.NotifyGlobally)
                        {
                            var severity = rule.Severity switch
                            {
                                WarningSeverity.Critical => UserNotificationSeverity.Error,
                                WarningSeverity.Warning => UserNotificationSeverity.Warning,
                                _ => UserNotificationSeverity.Information
                            };
                            delivery.Add(new(snapshot.Message, rule.Name, severity, UserNotificationPresentation.Banner, evaluatedVehicle));
                        }
                    }
                }
                foreach (var notification in delivery)
                {
                    try
                    {
                        await notifications.NotifyAsync(notification, token).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogWarning(exception, "A warning notification could not be delivered.");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Background warning evaluation stopped.");
        }
    }
}
