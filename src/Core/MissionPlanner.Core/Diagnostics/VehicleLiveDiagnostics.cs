using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

/// <summary>Collects normalized live evidence for the application lifetime, independent of Inspector visibility.</summary>
public sealed partial class VehicleLiveDiagnostics : IVehicleLiveDiagnostics, IDisposable
{
    private readonly Lock sync = new();
    private readonly Dictionary<VehicleId, Entry> vehicles = [];
    private readonly List<IDisposable> subscriptions = [];
    private readonly TimeProvider clock;
    private readonly MissionPlanner.Core.Vehicles.Abstractions.IVehicleParameterRegistry? parameters;
    private readonly VehicleLiveDiagnosticOptions options;

    /// <summary>Starts isolated telemetry delivery and bridges existing authoritative domain events.</summary>
    public VehicleLiveDiagnostics(IVehicleTelemetryEventHub telemetry, IDomainEventHub domain,
        TimeProvider clock, IOptions<VehicleLiveDiagnosticOptions> options,
        MissionPlanner.Core.Vehicles.Abstractions.IVehicleParameterRegistry? parameters = null)
    {
        this.clock = clock;
        this.parameters = parameters;
        this.options = options.Value;
        if (this.options.JournalCapacity < 1 || this.options.RawCapacity < 1 || this.options.ReasonLifetime <= TimeSpan.Zero ||
            this.options.OutputSampleLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        // One ordered envelope keeps connection, state and significant evidence in arrival order.
        subscriptions.Add(telemetry.SubscribeAsync<DiagnosticInput>((input, _) =>
        {
            Accept(input);
            return Task.CompletedTask;
        }));
        subscriptions.Add(telemetry.SubscribeAsync<VehicleRawDiagnostic>((item, _) =>
        {
            lock (sync)
            {
                var entry = Get(item.VehicleId);
                if (entry.Raw.Count == this.options.RawCapacity)
                {
                    entry.Raw.Dequeue();
                }
                entry.Raw.Enqueue(item);
            }
            return Task.CompletedTask;
        }));
        subscriptions.Add(telemetry.SubscribeAsync<VehicleCommandDiagnostic>((item, _) =>
        {
            AcceptCommand(item);
            return Task.CompletedTask;
        }));
        subscriptions.Add(telemetry.SubscribeAsync<VehicleDiagnosticEvent>((item, _) =>
        {
            lock (sync)
            {
                Add(Get(item.VehicleId), item);
            }
            return Task.CompletedTask;
        }));
        subscriptions.Add(domain.SubscribeDomainEventAsync<VehicleStateUpdated>((item, token) =>
            telemetry.PublishAsync(new DiagnosticInput(item.VehicleId, item.VehicleState), token)));
        subscriptions.Add(domain.SubscribeDomainEventAsync<VehicleConnected>((item, token) =>
            telemetry.PublishAsync(new DiagnosticInput(item.VehicleId, Connected: item), token)));
        subscriptions.Add(domain.SubscribeDomainEventAsync<VehicleDisconnected>((item, token) =>
            telemetry.PublishAsync(new DiagnosticInput(item.VehicleId, Disconnected: item), token)));
        subscriptions.Add(domain.SubscribeDomainEventAsync<VehicleStatusTextReceived>((item, token) =>
            item.Payload is VehicleStatusText text
                ? telemetry.PublishAsync(new DiagnosticInput(text.VehicleId, Text: text), token)
                : Task.CompletedTask));
    }

    /// <inheritdoc />
    public IReadOnlyList<VehicleDiagnosticChannel> GetRcChannels(VehicleId vehicleId)
    {
        return VehicleDiagnosticPanels.Rc(GetSnapshot(vehicleId).State, name => parameters?.GetParameter(vehicleId, name)?.Value);
    }

    /// <inheritdoc />
    public IReadOnlyList<VehicleId> Vehicles
    {
        get
        {
            lock (sync)
            {
                return vehicles.Keys.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public VehicleLiveDiagnosticSnapshot GetSnapshot(VehicleId vehicleId)
    {
        lock (sync)
        {
            var entry = Get(vehicleId);
            return new(vehicleId, entry.State, entry.Transport, entry.Endpoint, entry.Disconnected, entry.Version, entry.UpdatedAt);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<VehicleDiagnosticEvent> GetRecentEvents(VehicleId vehicleId, int maxCount = 1000)
    {
        lock (sync)
        {
            return Get(vehicleId).Journal.Reverse().OrderByDescending(item => item.At).Take(Math.Clamp(maxCount, 0, options.JournalCapacity)).ToArray();
        }
    }

    /// <inheritdoc />
    public void ClearEvents(VehicleId vehicleId)
    {
        lock (sync)
        {
            var entry = Get(vehicleId);
            entry.Journal.Clear();
            entry.Version++;
        }
    }

    /// <inheritdoc />
    public void AddMarker(VehicleId vehicleId, string? text)
    {
        lock (sync)
        {
            var message = string.IsNullOrWhiteSpace(text) ? "User marker" : text.Trim();
            Add(Get(vehicleId), new(vehicleId, clock.GetUtcNow(), "Marker", message[..Math.Min(message.Length, 256)]));
        }
    }

    private void Accept(DiagnosticInput input)
    {
        lock (sync)
        {
            var entry = Get(input.VehicleId);
            var now = clock.GetUtcNow();
            if (input.Connected is { } connected)
            {
                if (entry.Disconnected)
                {
                    // Retain the journal, but never present the previous session's
                    // telemetry as current while waiting for the new state stream.
                    entry.State = null;
                    entry.LastArmAttemptAt = null;
                    entry.ArmCorrelation = null;
                    entry.LastArmAck = null;
                    entry.LastArmCommandSource = null;
                    entry.LastArmResult = null;
                    entry.Version++;
                }
                entry.Disconnected = false;
                entry.Reasons.Clear();
                entry.Transport = connected.ConnectionType;
                entry.Endpoint = connected.Endpoint;
                Add(entry, new(input.VehicleId, connected.ConnectedAt, "Connection", $"Connected via {entry.Transport} {entry.Endpoint}"));
            }
            if (input.Disconnected is { } disconnected)
            {
                entry.Disconnected = true;
                Add(entry, new(input.VehicleId, disconnected.DisconnectedAt, "Connection", $"Disconnected: {disconnected.Reason}"));
            }
            if (input.State is { } state && !entry.Disconnected)
            {
                var previous = entry.State;
                ObserveArmingInput(entry, previous, state, now);
                entry.State = state;
                if (state.IsArmed || state.Arming.State == VehicleArmingState.DisarmedReady)
                {
                    entry.Reasons.Clear();
                }
                entry.Version++;
                entry.UpdatedAt = now;
                if (previous?.Connection.State != state.Connection.State)
                {
                    Add(entry, new(input.VehicleId, now, "Connection", state.Connection.State.ToString()));
                }
                if (previous?.IsArmed != state.IsArmed || previous?.Arming.State != state.Arming.State)
                {
                    Add(entry, new(input.VehicleId, now, "Arming", state.IsArmed ? "Armed heartbeat observed" : state.Arming.State.ToString(), entry.ArmCorrelation));
                }
                if (previous is not null && SensorFailed(previous, 1u << 25, now) != SensorFailed(state, 1u << 25, now))
                {
                    Add(entry, new(input.VehicleId, now, "Warning", SensorFailed(state, 1u << 25, now)
                        ? "Battery health failure reported" : "Battery health recovered or evidence unavailable"));
                }
                if (previous?.OnboardLogging.Healthy != state.OnboardLogging.Healthy && state.OnboardLogging.Healthy is not null)
                {
                    Add(entry, new(input.VehicleId, now, "Status", $"Onboard logging: {state.OnboardLogging.DisplayState}"));
                }
                var rcPresent = state.Radio.ChannelCount is > 0 && state.Radio.RssiPercent != 0;
                if (previous is not null && state.Radio.ObservedAt is not null &&
                    rcPresent != (previous.Radio.ChannelCount is > 0 && previous.Radio.RssiPercent != 0))
                {
                    Add(entry, new(input.VehicleId, now, "RC", rcPresent ? "RC signal restored" : "RC signal lost"));
                }
            }
            if (input.Text is { } text)
            {
                var correlation = text.Text.Contains("motor test", StringComparison.OrdinalIgnoreCase) ? entry.MotorCorrelation :
                    entry.LastArmAttemptAt is { } attempt && text.ReceivedAt >= attempt && text.ReceivedAt - attempt < TimeSpan.FromSeconds(10)
                        ? entry.ArmCorrelation : null;
                Add(entry, new(input.VehicleId, text.ReceivedAt, "STATUSTEXT", text.Text, correlation));
                if (text.SourceComponentId == input.VehicleId.ComponentId && !text.IsTruncated)
                {
                    if (text.Text.StartsWith("PreArm:", StringComparison.OrdinalIgnoreCase) && text.Text.Length > 7)
                    {
                        if (entry.Reasons.Count >= 32)
                        {
                            entry.Reasons.Remove(entry.Reasons.MinBy(pair => pair.Value).Key);
                        }
                        entry.Reasons[text.Text[7..].Trim()] = text.ReceivedAt;
                        entry.LastPreArmReason = text.Text[7..].Trim();
                        entry.LastPreArmReasonAt = text.ReceivedAt;
                    }
                    if (text.Text.StartsWith("Arm:", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.LastArmFailure = text.Text[4..].Trim();
                    }
                }
            }
        }
    }

    private Entry Get(VehicleId id)
    {
        if (!vehicles.TryGetValue(id, out var entry))
        {
            entry = new Entry();
            vehicles.Add(id, entry);
        }
        return entry;
    }

    private void Add(Entry entry, VehicleDiagnosticEvent item)
    {
        if (entry.Journal.Count == options.JournalCapacity)
        {
            entry.Journal.Dequeue();
        }
        entry.Journal.Enqueue(item);
        entry.Version++;
        entry.UpdatedAt = item.At;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
        subscriptions.Clear();
    }

    private sealed record DiagnosticInput(VehicleId VehicleId, VehicleState? State = null,
        VehicleConnected? Connected = null, VehicleDisconnected? Disconnected = null, VehicleStatusText? Text = null);

    private sealed class Entry
    {
        internal VehicleState? State;
        internal string? Transport;
        internal string? Endpoint;
        internal bool Disconnected;
        internal long Version;
        internal DateTimeOffset UpdatedAt;
        internal readonly Queue<VehicleDiagnosticEvent> Journal = new();
        internal readonly Queue<VehicleRawDiagnostic> Raw = new();
        internal readonly Dictionary<string, DateTimeOffset> Reasons = new(StringComparer.OrdinalIgnoreCase);
        internal string? LastArmFailure;
        internal DateTimeOffset? LastArmAttemptAt;
        internal string? LastArmResult;
        internal string? LastArmCommandSource;
        internal byte? LastArmAck;
        internal bool DisarmRequested;
        internal string? LastPreArmReason;
        internal DateTimeOffset? LastPreArmReasonAt;
        internal Guid? ArmCorrelation;
        internal Guid? MotorCorrelation;
    }
}
