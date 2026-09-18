using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.EventHub.Events;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Firmware;

/// <summary>An exact, advisory update recommendation. Installation remains a separate explicit workflow.</summary>
/// <param name="VehicleId">Connected vehicle.</param>
/// <param name="Identity">Installed firmware identity.</param>
/// <param name="Available">Exact compatible stable release.</param>
public sealed record VehicleFirmwareUpdate(VehicleId VehicleId, VehicleFirmwareIdentity Identity, FirmwareManifestEntry Available);

/// <summary>Announces a change to the non-blocking update notice, including dismissal.</summary>
public sealed class VehicleFirmwareUpdateChanged(VehicleFirmwareUpdate? update)
    : DomainEvent<VehicleFirmwareUpdate?>(nameof(VehicleFirmwareUpdateChanged), update);

/// <summary>Owns one cancellable background check per successful connection and session notification suppression.</summary>
public sealed class VehicleFirmwareUpdateService(
    IVehicleRegistry registry,
    IVehicleMessageStore messages,
    IFirmwareCatalogService catalog,
    IDomainEventHub events,
    IUserNotificationService notifications,
    TimeProvider clock,
    ILogger<VehicleFirmwareUpdateService> logger) : IAsyncDisposable
{
    private readonly HashSet<string> notified = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? lifetime;
    private readonly List<Task> tasks = [];

    /// <summary>Gets the current advisory notice.</summary>
    public VehicleFirmwareUpdate? Current { get; private set; }

    /// <summary>Queues a check without delaying the caller or connection establishment.</summary>
    public Task Start(VehicleId vehicleId, DateTimeOffset connectedSince)
    {
        lifetime?.Cancel();
        var previous = lifetime;
        lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(45), clock);
        var token = lifetime.Token;
        tasks.RemoveAll(task => task.IsCompleted);
        var task = Task.Run(() => RunAsync(vehicleId, connectedSince, token, previous));
        tasks.Add(task);
        return task;
    }

    /// <summary>Cancels the current check immediately when the connection ends.</summary>
    public void Cancel()
    {
        lifetime?.Cancel();
        Current = null;
    }

    /// <summary>Dismisses the banner without forgetting session suppression.</summary>
    public async Task DismissAsync()
    {
        Current = null;
        await events.PublishDomainEventAsync(new VehicleFirmwareUpdateChanged(null));
    }

    private async Task RunAsync(VehicleId vehicleId, DateTimeOffset connectedSince, CancellationToken token, CancellationTokenSource? previous)
    {
        await Task.Yield();
        try
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                previous?.Dispose();
                // AUTOPILOT_VERSION and startup banner may arrive after connection completion.
                var start = clock.GetTimestamp();
                while (registry.GetRequired(vehicleId)?.State.Identity.Firmware.FlightVersion is null &&
                    clock.GetElapsedTime(start) < TimeSpan.FromSeconds(15))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), clock, token).ConfigureAwait(false);
                }
                var identity = registry.GetRequired(vehicleId)?.State.Identity.Firmware;
                if (identity is null || identity.Autopilot != 3 || identity.FlightVersion is not { ReleaseType: FirmwareReleaseType.Official })
                {
                    logger.LogDebug("Firmware check skipped for unsupported or non-official identity {VehicleId}", vehicleId);
                    return;
                }

                logger.LogInformation("Firmware update check started for {VehicleId}, installed {InstalledVersion}", vehicleId, identity.FlightVersion);
                var releases = await catalog.GetCatalogAsync(new FirmwareCatalogRequest(Channel: FirmwareReleaseChannel.Stable), token).ConfigureAwait(false);
                logger.LogDebug("Firmware catalog snapshot from {RetrievedAt}, stale {IsStale}", releases.RetrievedAt, releases.IsStale);
                string? platform;
                do
                {
                    platform = ResolvePlatform(vehicleId, connectedSince, releases.Entries);
                    if (platform is not null || clock.GetElapsedTime(start) >= TimeSpan.FromSeconds(15))
                    {
                        break;
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(250), clock, token).ConfigureAwait(false);
                } while (true);

                token.ThrowIfCancellationRequested();
                if (platform is null)
                {
                    logger.LogInformation("Firmware target unresolved for {VehicleId}; no upgrade recommendation", vehicleId);
                    return;
                }
                logger.LogInformation("Firmware target resolved for {VehicleId}: {Platform}", vehicleId, platform);
                var available = FirmwareUpdatePolicy.FindUpdate(identity, platform, releases.Entries);
                if (available is null)
                {
                    logger.LogInformation("No newer unambiguous stable firmware for {VehicleId} on {Platform}", vehicleId, platform);
                    return;
                }

                var key = $"{identity.HardwareUid2 ?? identity.HardwareUid?.ToString() ?? vehicleId.ToString()}|{platform}|{identity.MavType}|{identity.FlightVersion}|{available.Version.Value}";
                if (!notified.Add(key))
                {
                    return;
                }
                Current = new VehicleFirmwareUpdate(vehicleId, identity, available);
                logger.LogInformation("Firmware update available for {VehicleId}: {Platform} {AvailableVersion}", vehicleId, platform, available.Version.Value);
                await events.PublishDomainEventAsync(new VehicleFirmwareUpdateChanged(Current), token).ConfigureAwait(false);
                await notifications.NotifyAsync(new UserNotification(
                    $"{platform}: {available.Version.Value} Stable. Use View Upgrade in the firmware notice.",
                    "Firmware update available", VehicleId: vehicleId), token).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogDebug("Firmware update check cancelled for {VehicleId}", vehicleId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Optional firmware update check failed for {VehicleId}", vehicleId);
        }
    }

    private string? ResolvePlatform(VehicleId vehicleId, DateTimeOffset connectedSince, IReadOnlyList<FirmwareManifestEntry> entries)
    {
        var platforms = entries.Select(entry => entry.Target.Platform).ToHashSet(StringComparer.Ordinal);
        var reported = messages.GetMessages(vehicleId)
            .Where(message => message.ReceivedAt >= connectedSince && !message.IsTruncated && message.SourceComponentId == vehicleId.ComponentId)
            .Select(message => message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            // ArduPilot's board banner is the exact board name followed by hexadecimal hardware identifiers.
            .Where(parts => parts.Length >= 2 && platforms.Contains(parts[0]) &&
                parts.Skip(1).All(part => part.Length >= 8 && part.All(Uri.IsHexDigit)))
            .Select(parts => parts[0]).Distinct(StringComparer.Ordinal).Take(2).ToArray();
        return reported.Length == 1 ? reported[0] : null;
    }

    /// <summary>Cancels and awaits background work during application shutdown.</summary>
    public async ValueTask DisposeAsync()
    {
        Cancel();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        lifetime?.Dispose();
        gate.Dispose();
    }
}
