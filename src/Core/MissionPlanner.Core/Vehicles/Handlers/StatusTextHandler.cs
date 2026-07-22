using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>
/// Assembles MAVLink 2 status-text chunks and stores complete messages in a bounded vehicle history.
/// </summary>
public sealed class StatusTextHandler : IStatusTextHandler
{
    private readonly object sync = new();
    private readonly Dictionary<ChunkKey, PendingMessage> pending = [];
    private readonly Dictionary<ChunkKey, CompletedChunk> recentCompletions = [];
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IVehicleMessageStore messageStore;
    private readonly IDomainEventHub domainEventHub;
    private readonly ILogger<StatusTextHandler> logger;
    private readonly TimeSpan chunkTimeout;

    /// <summary>Initializes a status-text handler.</summary>
    /// <param name="vehicleRegistry">The vehicle registry.</param>
    /// <param name="messageStore">The bounded vehicle message store.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="options">The message history and assembly options.</param>
    /// <param name="logger">The logger.</param>
    public StatusTextHandler(
        IVehicleRegistry vehicleRegistry,
        IVehicleMessageStore messageStore,
        IDomainEventHub domainEventHub,
        IOptions<VehicleMessageStoreOptions> options,
        ILogger<StatusTextHandler> logger)
    {
        this.vehicleRegistry = vehicleRegistry;
        this.messageStore = messageStore;
        this.domainEventHub = domainEventHub;
        this.logger = logger;
        chunkTimeout = options.Value.ChunkTimeout > TimeSpan.Zero
            ? options.Value.ChunkTimeout
            : TimeSpan.FromSeconds(2);
    }

    /// <inheritdoc />
    public async Task Handle(StatusTextMessage message, CancellationToken cancellationToken)
    {
        var vehicle = FindVehicle(message.SystemId, message.ComponentId);
        if (vehicle is null)
        {
            logger.LogWarning(
                "No vehicle with system ID {SystemId} is registered for STATUSTEXT from component {ComponentId}.",
                message.SystemId,
                message.ComponentId);
            return;
        }

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace(
                "Handling STATUSTEXT from {SystemId}:{ComponentId}, ID {MessageId}, chunk {ChunkSequence}.",
                message.SystemId,
                message.ComponentId,
                message.Id,
                message.ChunkSequence);
        }

        var completed = Process(vehicle.Id, message);
        foreach (var statusText in completed)
        {
            await PersistAsync(vehicle, statusText, cancellationToken).ConfigureAwait(false);
        }
    }

    private IReadOnlyList<VehicleStatusText> Process(VehicleId vehicleId, StatusTextMessage message)
    {
        if (message.Id is null or 0 || message.ChunkSequence is null)
        {
            return [CreateMessage(vehicleId, message, message.Text, false, !message.IsTextTerminated)];
        }

        var key = new ChunkKey(vehicleId, message.SystemId, message.ComponentId, message.Id.Value);
        var completed = new List<VehicleStatusText>(2);
        lock (sync)
        {
            foreach (var expiredKey in recentCompletions
                         .Where(item => message.ReceivedAt - item.Value.ReceivedAt > chunkTimeout)
                         .Select(item => item.Key)
                         .ToArray())
            {
                recentCompletions.Remove(expiredKey);
            }

            if (recentCompletions.TryGetValue(key, out var recent) &&
                recent.Sequence == message.ChunkSequence &&
                recent.Text == message.Text)
            {
                return completed;
            }

            if (message.ChunkSequence == 0)
            {
                if (pending.TryGetValue(key, out var existing) && existing.IsDuplicateFirst(message))
                {
                    return completed;
                }

                if (pending.Remove(key, out var replaced))
                {
                    completed.Add(replaced.ToMessage(true));
                }

                if (message.IsTextTerminated)
                {
                    completed.Add(CreateMessage(vehicleId, message, message.Text, false, false));
                    recentCompletions[key] = new CompletedChunk(0, message.Text, message.ReceivedAt);
                }
                else
                {
                    var started = new PendingMessage(vehicleId, message);
                    pending[key] = started;
                    ScheduleTimeout(key, started.Generation);
                }

                return completed;
            }

            if (!pending.TryGetValue(key, out var current))
            {
                completed.Add(CreateMessage(vehicleId, message, message.Text, false, true));
                return completed;
            }

            if (message.ChunkSequence < current.ExpectedChunk)
            {
                return completed;
            }

            if (message.ChunkSequence > current.ExpectedChunk)
            {
                pending.Remove(key);
                completed.Add(current.ToMessage(true));
                completed.Add(CreateMessage(vehicleId, message, message.Text, false, true));
                return completed;
            }

            current.Append(message.Text, message.ReceivedAt);
            if (message.IsTextTerminated)
            {
                pending.Remove(key);
                completed.Add(current.ToMessage(false));
                recentCompletions[key] = new CompletedChunk(message.ChunkSequence.Value, message.Text, message.ReceivedAt);
            }
            else
            {
                current.Advance();
                ScheduleTimeout(key, current.Generation);
            }
        }

        return completed;
    }

    private void ScheduleTimeout(ChunkKey key, int generation) =>
        _ = FlushAfterTimeoutAsync(key, generation);

    private async Task FlushAfterTimeoutAsync(ChunkKey key, int generation)
    {
        await Task.Delay(chunkTimeout).ConfigureAwait(false);
        PendingMessage? expired = null;
        lock (sync)
        {
            if (pending.TryGetValue(key, out var candidate) && candidate.Generation == generation)
            {
                pending.Remove(key);
                expired = candidate;
            }
        }

        if (expired is null)
        {
            return;
        }

        try
        {
            var vehicle = vehicleRegistry.GetRequired(expired.VehicleId);
            if (vehicle is not null)
            {
                await PersistAsync(vehicle, expired.ToMessage(true), CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to flush timed-out STATUSTEXT {MessageId} for {VehicleId}.", key.ProtocolId, key.VehicleId);
        }
    }

    private async Task PersistAsync(VehicleSession vehicle, VehicleStatusText message, CancellationToken cancellationToken)
    {
        var stored = messageStore.Add(message);
        vehicle.ApplyStatusText(stored);
        await domainEventHub.PublishDomainEventAsync(new VehicleStatusTextReceived(stored), cancellationToken).ConfigureAwait(false);
    }

    private VehicleSession? FindVehicle(byte systemId, byte componentId) =>
        vehicleRegistry.GetRequired(new VehicleId(systemId, 1)) ??
        vehicleRegistry.GetRequired(new VehicleId(systemId, componentId)) ??
        vehicleRegistry.Vehicles.FirstOrDefault(candidate => candidate.Id.SystemId == systemId);

    private static VehicleStatusText CreateMessage(
        VehicleId vehicleId,
        StatusTextMessage source,
        string text,
        bool assembled,
        bool truncated) =>
        new(
            vehicleId,
            source.SystemId,
            source.ComponentId,
            source.Severity,
            text,
            source.ReceivedAt,
            source.Id,
            assembled,
            truncated);

    private readonly record struct ChunkKey(VehicleId VehicleId, byte SystemId, byte ComponentId, ushort ProtocolId);

    private readonly record struct CompletedChunk(byte Sequence, string Text, DateTimeOffset ReceivedAt);

    private sealed class PendingMessage
    {
        private readonly StringBuilder text;

        public PendingMessage(VehicleId vehicleId, StatusTextMessage first)
        {
            VehicleId = vehicleId;
            SourceSystemId = first.SystemId;
            SourceComponentId = first.ComponentId;
            Severity = first.Severity;
            ProtocolId = first.Id;
            ReceivedAt = first.ReceivedAt;
            LastReceivedAt = first.ReceivedAt;
            text = new StringBuilder(first.Text);
        }

        public VehicleId VehicleId { get; }

        public byte SourceSystemId { get; }

        public byte SourceComponentId { get; }

        public MavSeverity Severity { get; }

        public ushort? ProtocolId { get; }

        public DateTimeOffset ReceivedAt { get; }

        public DateTimeOffset LastReceivedAt { get; private set; }

        public byte ExpectedChunk { get; private set; } = 1;

        public int Generation { get; private set; }

        public int ChunkCount { get; private set; } = 1;

        public bool IsDuplicateFirst(StatusTextMessage message) =>
            ChunkCount == 1 &&
            message.SystemId == SourceSystemId &&
            message.ComponentId == SourceComponentId &&
            message.Severity == Severity &&
            message.Text == text.ToString();

        public void Append(string value, DateTimeOffset receivedAt)
        {
            text.Append(value);
            LastReceivedAt = receivedAt;
            ChunkCount++;
        }

        public void Advance()
        {
            ExpectedChunk++;
            Generation++;
        }

        public VehicleStatusText ToMessage(bool truncated) =>
            new(
                VehicleId,
                SourceSystemId,
                SourceComponentId,
                Severity,
                text.ToString(),
                ReceivedAt,
                ProtocolId,
                ChunkCount > 1,
                truncated);
    }
}
