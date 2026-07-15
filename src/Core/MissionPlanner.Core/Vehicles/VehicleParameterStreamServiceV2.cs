using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Simplified parameter streaming service that mimics original MissionPlanner's proven approach.
/// Uses direct polling of the parameter registry instead of event subscriptions.
/// </summary>
public sealed class VehicleParameterStreamServiceV2(
    IVehicleParameterService parameterService,
    IVehicleParameterRegistry parameterRegistry,
    ILogger<VehicleParameterStreamServiceV2> logger)
    : IVehicleParameterStreamService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public async Task<ParameterStreamResult> StreamAllParametersAsync(
        VehicleId vehicleId,
        IProgress<ParameterStreamProgress>? progress = null,
        Action<VehicleParameter>? onParameterReceived = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var actualTimeout = timeout ?? DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Starting parameter stream for vehicle {VehicleId} with timeout {Timeout}s", vehicleId, actualTimeout.TotalSeconds);

        // Clear existing parameters
        parameterRegistry.ClearParameters(vehicleId);

        // Send initial request
        var requestSent = await parameterService.RequestParameterListAsync(vehicleId, cancellationToken);
        if (!requestSent)
        {
            return ParameterStreamResult.CreateFailure("Failed to send parameter request", stopwatch.Elapsed);
        }

        // Active polling loop (like original MissionPlanner)
        var lastValidPacketTime = DateTime.Now;
        var lastParameterCount = 0;
        var stableCount = 0;

        while (stopwatch.Elapsed < actualTimeout && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(100, cancellationToken); // Poll every 100ms

            // Get current parameters from registry
            var currentParams = parameterRegistry.GetAllParameters(vehicleId);
            var totalCount = parameterRegistry.GetParameterCount(vehicleId) ?? 0;

            // Check if we received new parameters
            if (currentParams.Count > lastParameterCount)
            {
                lastValidPacketTime = DateTime.Now;
                lastParameterCount = currentParams.Count;
                stableCount = 0;

                // Report progress
                if (progress != null && totalCount > 0)
                {
                    var percentComplete = (int)(currentParams.Count / (double)totalCount * 100);
                    progress.Report(new ParameterStreamProgress(currentParams.Count, totalCount, percentComplete, currentParams.Count >= totalCount));
                }

                // Invoke callback for new parameters (if provided)
                if (onParameterReceived != null)
                {
                    foreach (var param in currentParams.Values)
                    {
                        onParameterReceived(param);
                    }
                }
            }
            else
            {
                stableCount++;
            }

            // Check if complete
            if (totalCount > 0 && currentParams.Count >= totalCount)
            {
                // Wait for stability (no new params for 5 poll cycles = 500ms)
                if (stableCount >= 5)
                {
                    logger.LogInformation("Successfully received {Count} parameters in {Duration}s", currentParams.Count, stopwatch.Elapsed.TotalSeconds);
                    return ParameterStreamResult.CreateSuccess(currentParams, totalCount, stopwatch.Elapsed);
                }
            }

            // Check for stall (no new params for 4 seconds, like original MissionPlanner)
            if ((DateTime.Now - lastValidPacketTime).TotalSeconds >= 4 && totalCount > 0)
            {
                logger.LogWarning("No new parameters for 4 seconds. Received {Received}/{Total}", currentParams.Count, totalCount);
                break; // Exit to retry logic
            }
        }

        // Timeout or incomplete
        var finalParams = parameterRegistry.GetAllParameters(vehicleId);
        var finalCount = parameterRegistry.GetParameterCount(vehicleId) ?? 0;

        if (finalParams.Count >= finalCount && finalCount > 0)
        {
            // Got all parameters but took too long
            logger.LogInformation("Received all {Count} parameters (slow connection)", finalParams.Count);
            return ParameterStreamResult.CreateSuccess(finalParams, finalCount, stopwatch.Elapsed);
        }

        logger.LogWarning("Parameter stream incomplete after {Duration}s. Received {Received}/{Total}", stopwatch.Elapsed.TotalSeconds, finalParams.Count, finalCount);

        return ParameterStreamResult.CreateFailure($"Incomplete: received {finalParams.Count}/{finalCount} parameters", stopwatch.Elapsed);
    }

    /// <inheritdoc/>
    public async Task<ParameterStreamResult> StreamAllParametersWithRetryAsync(
        VehicleId vehicleId,
        IProgress<ParameterStreamProgress>? progress = null,
        int maxRetries = 3,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();
        var retry = 0;
        var lastReceivedCount = 0;

        while (retry <= maxRetries && !cancellationToken.IsCancellationRequested)
        {
            if (retry > 0)
            {
                logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} for vehicle {VehicleId}", retry, maxRetries, vehicleId);
            }

            // Get current state
            var currentParams = parameterRegistry.GetAllParameters(vehicleId);
            var totalCount = parameterRegistry.GetParameterCount(vehicleId) ?? 0;

            // If we have params from previous attempt, try targeted recovery
            if (retry > 0 && totalCount > 0 && currentParams.Count > 0)
            {
                var percentReceived = currentParams.Count * 100 / totalCount;
                logger.LogInformation("Current: {Received}/{Total} ({Percent}%)", currentParams.Count, totalCount, percentReceived);

                // If less than 75%, request full list again (like original MissionPlanner)
                if (percentReceived < 75 && retry <= 2)
                {
                    logger.LogInformation("Less than 75% received. Re-requesting full list...");
                    await parameterService.RequestParameterListAsync(vehicleId, cancellationToken);
                    await Task.Delay(500, cancellationToken);
                }
                else
                {
                    // Request missing parameters individually
                    var receivedIndices = currentParams.Values
                        .Where(p => p.Index != 65535) // Exclude invalid index!
                        .Select(p => p.Index)
                        .ToHashSet();

                    var missingIndices = Enumerable.Range(0, totalCount)
                        .Select(i => (ushort)i)
                        .Where(i => !receivedIndices.Contains(i))
                        .ToList();

                    if (missingIndices.Count == 0)
                    {
                        logger.LogInformation("All {Total} parameters received!", totalCount);
                        return ParameterStreamResult.CreateSuccess(currentParams, totalCount, overallStopwatch.Elapsed);
                    }

                    logger.LogInformation("Requesting {Missing} missing parameters individually...", missingIndices.Count);

                    // Request in batches of 20
                    for (var i = 0; i < missingIndices.Count && i < 20; i++)
                    {
                        await parameterService.RequestParameterByIndexAsync(vehicleId, missingIndices[i], cancellationToken);
                    }

                    await Task.Delay(500, cancellationToken);
                }
            }

            // Run streaming attempt with 30-second timeout
            var result = await StreamAllParametersAsync(vehicleId, progress, null,
                TimeSpan.FromSeconds(30), cancellationToken);

            if (result.Success)
            {
                return result;
            }

            // Check if we made progress
            var newReceivedCount = parameterRegistry.GetAllParameters(vehicleId).Count;
            if (newReceivedCount > lastReceivedCount)
            {
                logger.LogInformation("Progress: {Old} → {New} parameters",
                    lastReceivedCount, newReceivedCount);
                lastReceivedCount = newReceivedCount;
            }

            retry++;
        }

        // Final check
        var finalParams = parameterRegistry.GetAllParameters(vehicleId);
        var finalTotalCount = parameterRegistry.GetParameterCount(vehicleId) ?? 0;

        return finalParams.Count >= finalTotalCount && finalTotalCount > 0
            ? ParameterStreamResult.CreateSuccess(finalParams, finalTotalCount, overallStopwatch.Elapsed)
            : ParameterStreamResult.CreateFailure(
                $"Failed after {maxRetries} retries. Received {finalParams.Count}/{finalTotalCount} parameters.",
                overallStopwatch.Elapsed);
    }
}
