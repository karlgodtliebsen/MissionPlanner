using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Service for streaming all parameters from a vehicle with progress tracking.
/// </summary>
public sealed class VehicleParameterStreamService(
    IVehicleParameterService parameterService,
    IVehicleParameterRegistry parameterRegistry,
    IDomainEventHub eventHub,
    ILogger<VehicleParameterStreamService> logger)
    : IVehicleParameterStreamService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    /// <inheritdoc/>
    public async Task<ParameterStreamResult> StreamAllParametersAsync(
        VehicleId vehicleId,
        IProgress<ParameterStreamProgress>? progress = null,
        Action<VehicleParameter>? onParameterReceived = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var actualTimeout = timeout ?? DefaultTimeout;

        logger.LogInformation("Starting parameter stream for vehicle {VehicleId} with timeout {Timeout}s", vehicleId, actualTimeout.TotalSeconds);

        // Clear any existing parameters for fresh start
        parameterRegistry.ClearParameters(vehicleId);

        var receivedIndices = new HashSet<ushort>();
        var totalCount = 0;
        var completed = false;
        IDisposable? subscription = null;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(actualTimeout);

            // Subscribe to parameter events
            subscription = eventHub.SubscribeDomainEventAsync<VehicleParameterReceived>(async (@event, ct) =>
            {
                if (@event.VehicleId != vehicleId)
                {
                    return;
                }

                var param = @event.Parameter;

                // Track which parameter indices we've received (handles duplicates correctly)
                receivedIndices.Add(param.Index);
                totalCount = param.Count;

                logger.LogDebug("Received parameter {Index}/{Count}: {Name} = {Value}", param.Index + 1, param.Count, param.Name, param.Value);

                // Invoke callback
                onParameterReceived?.Invoke(param);

                // Report progress
                if (totalCount > 0)
                {
                    var receivedCount = receivedIndices.Count;
                    var percentComplete = (int)(receivedCount / (double)totalCount * 100);
                    var isComplete = receivedCount >= totalCount;

                    progress?.Report(new ParameterStreamProgress(receivedCount, totalCount, percentComplete, isComplete));

                    if (isComplete)
                    {
                        completed = true;
                    }
                }

                await Task.CompletedTask;
            });

            // Send the request
            var requestSent = await parameterService.RequestParameterListAsync(vehicleId, cts.Token);
            if (!requestSent)
            {
                return ParameterStreamResult.CreateFailure("Failed to send parameter request", stopwatch.Elapsed);
            }

            // Wait for all parameters to arrive or timeout
            var pollInterval = TimeSpan.FromMilliseconds(100);
            while (!completed && !cts.Token.IsCancellationRequested)
            {
                await Task.Delay(pollInterval, cts.Token);

                // Double-check completion in case event was missed
                if (totalCount > 0 && receivedIndices.Count >= totalCount)
                {
                    completed = true;
                }
            }

            if (!completed)
            {
                if (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    var receivedCount = receivedIndices.Count;
                    logger.LogWarning("Parameter stream timeout after {Duration}s. Received {Received}/{Total}", stopwatch.Elapsed.TotalSeconds, receivedCount, totalCount);

                    // Log missing parameter indices for debugging
                    if (totalCount > 0 && receivedCount < totalCount)
                    {
                        var missingIndices = Enumerable.Range(0, totalCount)
                            .Select(i => (ushort)i)
                            .Where(i => !receivedIndices.Contains(i))
                            .Take(10) // Show first 10 missing
                            .ToList();

                        if (missingIndices.Any())
                        {
                            logger.LogWarning("Missing parameter indices: {MissingIndices}", string.Join(", ", missingIndices));
                        }
                    }

                    return ParameterStreamResult.CreateFailure($"Timeout after {actualTimeout.TotalSeconds}s. Received {receivedCount}/{totalCount} parameters.", stopwatch.Elapsed);
                }

                return ParameterStreamResult.CreateFailure("Operation cancelled", stopwatch.Elapsed);
            }

            var allParameters = parameterRegistry.GetAllParameters(vehicleId);

            logger.LogInformation("Successfully received {Count} parameters in {Duration}s", allParameters.Count, stopwatch.Elapsed.TotalSeconds);

            return ParameterStreamResult.CreateSuccess(allParameters, totalCount, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming parameters for vehicle {VehicleId}", vehicleId);

            return ParameterStreamResult.CreateFailure($"Error: {ex.Message}", stopwatch.Elapsed);
        }
        finally
        {
            subscription?.Dispose();
        }
    }

    /// <inheritdoc/>
    public async Task<ParameterStreamResult> StreamAllParametersWithRetryAsync(VehicleId vehicleId, IProgress<ParameterStreamProgress>? progress = null, int maxRetries = 3, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var actualTimeout = timeout ?? DefaultTimeout;
        var retryDelay = TimeSpan.FromSeconds(2);

        HashSet<ushort>? receivedIndices = null;
        ushort totalCount = 0;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ParameterStreamResult.CreateFailure($"Cancelled after {maxRetries + 1} attempts", TimeSpan.Zero);
            }

            if (attempt > 0)
            {
                logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} for vehicle {VehicleId}", attempt, maxRetries, vehicleId);
                await Task.Delay(retryDelay, cancellationToken);
            }

            // For first attempt, request all parameters
            // For subsequent attempts, request only missing parameters
            var result = await StreamAllParametersAsync(vehicleId, progress, null, actualTimeout, cancellationToken);

            if (result.Success)
            {
                return result;
            }

            // If we have partial data, try to request missing parameters individually
            if (attempt < maxRetries)
            {
                logger.LogWarning("Parameter stream failed on attempt {Attempt}: {Error}. Analyzing gaps...", attempt + 1, result.ErrorMessage);

                // Get the current state from the registry
                var currentParams = parameterRegistry.GetAllParameters(vehicleId);
                totalCount = parameterRegistry.GetParameterCount(vehicleId) ?? 0;

                if (totalCount > 0 && currentParams.Count > 0 && currentParams.Count < totalCount)
                {
                    // Build set of received indices
                    receivedIndices = currentParams.Values.Select(p => p.Index).ToHashSet();

                    // Find missing indices
                    var missingIndices = Enumerable.Range(0, totalCount)
                        .Select(i => (ushort)i)
                        .Where(i => !receivedIndices.Contains(i))
                        .ToList();

                    logger.LogInformation("Found {Missing} missing parameters out of {Total}. Requesting individually...",
                        missingIndices.Count, totalCount);

                    // Request each missing parameter individually
                    foreach (var missingIndex in missingIndices)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        await parameterService.RequestParameterByIndexAsync(vehicleId, missingIndex, cancellationToken);
                        await Task.Delay(50, cancellationToken); // Small delay between requests
                    }

                    // Give some time for responses to arrive
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            else
            {
                logger.LogError("Parameter stream failed after {MaxRetries} attempts: {Error}", maxRetries + 1, result.ErrorMessage);
                return result;
            }
        }

        return ParameterStreamResult.CreateFailure($"Failed after {maxRetries + 1} attempts", TimeSpan.Zero);
    }
}
