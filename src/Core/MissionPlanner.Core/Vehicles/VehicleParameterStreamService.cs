using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Vehicles;

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
        // Use shorter timeout like original MissionPlanner (4-5 seconds per attempt)
        var perAttemptTimeout = TimeSpan.FromSeconds(5);
        var overallStopwatch = Stopwatch.StartNew();

        // First attempt: Request all parameters
        var result = await StreamAllParametersAsync(vehicleId, progress, null, perAttemptTimeout, cancellationToken);

        if (result.Success)
        {
            return result;
        }

        // Retry attempts: Use original MissionPlanner's proven strategy
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ParameterStreamResult.CreateFailure("Operation cancelled", overallStopwatch.Elapsed);
            }

            logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} for vehicle {VehicleId}", attempt, maxRetries, vehicleId);

            // Get the current state from the registry (DON'T clear it!)
            var currentParams = parameterRegistry.GetAllParameters(vehicleId);
            var totalCount = parameterRegistry.GetParameterCount(vehicleId) ?? 0;

            if (totalCount == 0 || currentParams.Count == 0)
            {
                // No parameters received at all, retry full request
                logger.LogWarning("No parameters received yet. Retrying full request...");
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                result = await StreamAllParametersAsync(vehicleId, progress, null, perAttemptTimeout, cancellationToken);

                if (result.Success)
                {
                    return result;
                }

                continue;
            }

            // Build set of received indices
            var receivedIndices = currentParams.Values.Select(p => p.Index).ToHashSet();

            // Find missing indices
            var missingIndices = Enumerable.Range(0, totalCount)
                .Select(i => (ushort)i)
                .Where(i => !receivedIndices.Contains(i))
                .ToList();

            if (missingIndices.Count == 0)
            {
                // All parameters received!
                logger.LogInformation("All {Total} parameters received after retry", totalCount);
                return ParameterStreamResult.CreateSuccess(currentParams, totalCount, overallStopwatch.Elapsed);
            }

            var percentReceived = currentParams.Count * 100 / totalCount;
            logger.LogInformation("Received {Received}/{Total} ({Percent}%). Missing: {Missing}",
                currentParams.Count, totalCount, percentReceived, missingIndices.Count);

            // Original MissionPlanner strategy:
            // If less than 75% received and retry < 2: Re-send full PARAM_REQUEST_LIST
            // Otherwise: Request missing parameters individually (10-20 at a time)
            if (percentReceived < 75 && attempt <= 2)
            {
                logger.LogInformation("Less than 75% received. Re-requesting full parameter list...");
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

                // Don't clear existing parameters - just send another PARAM_REQUEST_LIST
                // The vehicle will resend all params, and duplicates will be handled by the registry
                var requestSent = await parameterService.RequestParameterListAsync(vehicleId, cancellationToken);
                if (!requestSent)
                {
                    logger.LogWarning("Failed to send PARAM_REQUEST_LIST");
                    continue;
                }

                // Wait for parameters with short timeout (5 seconds)
                var retryStopwatch = Stopwatch.StartNew();
                var retryTimeout = TimeSpan.FromSeconds(5);

                while (retryStopwatch.Elapsed < retryTimeout && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);

                    currentParams = parameterRegistry.GetAllParameters(vehicleId);
                    if (currentParams.Count >= totalCount)
                    {
                        logger.LogInformation("All {Total} parameters received after full list retry!", totalCount);
                        return ParameterStreamResult.CreateSuccess(currentParams, totalCount, overallStopwatch.Elapsed);
                    }
                }

                logger.LogInformation("After full list retry: {Received}/{Total}",
                    currentParams.Count, totalCount);
                continue;
            }

            // Request missing parameters individually, 10-20 at a time
            logger.LogInformation("Requesting missing parameters individually (10-20 at a time)...");

            var firstMissing = string.Join(", ", missingIndices.Take(10));
            logger.LogDebug("Missing indices (first 10): {Indices}", firstMissing);

            var batchSize = 20;
            var receivedBefore = receivedIndices.Count;

            // Request missing parameters in small batches (like original MissionPlanner's "10 by 10" strategy)
            var batchCount = 0;
            for (var batchStart = 0; batchStart < missingIndices.Count; batchStart += batchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var batchIndices = missingIndices.Skip(batchStart).Take(batchSize).ToList();
                batchCount++;

                logger.LogDebug("Requesting batch {Batch}: {Count} parameters (indices {Start} to {End})",
                    batchCount, batchIndices.Count, batchIndices.First(), batchIndices.Last());

                // Send all requests in this batch quickly
                foreach (var missingIndex in batchIndices)
                {
                    await parameterService.RequestParameterByIndexAsync(vehicleId, missingIndex, cancellationToken);
                }

                // Wait briefly for this batch to arrive
                var batchStopwatch = Stopwatch.StartNew();
                var batchTimeout = TimeSpan.FromSeconds(2);
                var batchStartCount = parameterRegistry.GetAllParameters(vehicleId).Count;

                while (batchStopwatch.Elapsed < batchTimeout && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(50, cancellationToken);

                    currentParams = parameterRegistry.GetAllParameters(vehicleId);

                    // Report progress
                    if (progress != null && totalCount > 0)
                    {
                        var percentComplete = (int)(currentParams.Count / (double)totalCount * 100);
                        progress.Report(new ParameterStreamProgress(
                            currentParams.Count,
                            totalCount,
                            percentComplete,
                            currentParams.Count >= totalCount));
                    }

                    // Check if all parameters received
                    if (currentParams.Count >= totalCount)
                    {
                        var totalReceived = currentParams.Count - receivedBefore;
                        logger.LogInformation("All {Total} parameters received! (Received {New} new)", totalCount, totalReceived);
                        return ParameterStreamResult.CreateSuccess(currentParams, totalCount, overallStopwatch.Elapsed);
                    }

                    // Check if this batch is done (received some new params)
                    var batchReceived = currentParams.Count - batchStartCount;
                    if (batchReceived >= batchIndices.Count)
                    {
                        logger.LogDebug("Batch {Batch} complete: received {Count} parameters", batchCount, batchReceived);
                        break;
                    }
                }

                var currentReceived = parameterRegistry.GetAllParameters(vehicleId).Count;
                var batchNewParams = currentReceived - batchStartCount;
                logger.LogDebug("Batch {Batch} timeout: received {New} of {Requested} parameters. Total: {Total}/{Expected}",
                    batchCount, batchNewParams, batchIndices.Count, currentReceived, totalCount);
            }

            // Log progress after this retry attempt
            var finalReceivedCount = parameterRegistry.GetAllParameters(vehicleId).Count;
            var newInThisAttempt = finalReceivedCount - receivedBefore;
            logger.LogInformation("Retry attempt {Attempt} completed. Received {Received}/{Total} ({New} new)",
                attempt, finalReceivedCount, totalCount, newInThisAttempt);

            // If this was the last attempt, check if we got everything
            if (attempt >= maxRetries)
            {
                currentParams = parameterRegistry.GetAllParameters(vehicleId);
                return currentParams.Count >= totalCount
                    ? ParameterStreamResult.CreateSuccess(currentParams, totalCount, overallStopwatch.Elapsed)
                    : ParameterStreamResult.CreateFailure($"Incomplete after {maxRetries} retries. Received {currentParams.Count}/{totalCount} parameters.", overallStopwatch.Elapsed);
            }
        }

        var finalParams = parameterRegistry.GetAllParameters(vehicleId);
        var finalCount = parameterRegistry.GetParameterCount(vehicleId) ?? 0;

        return ParameterStreamResult.CreateFailure($"Failed after {maxRetries} retries. Received {finalParams.Count}/{finalCount} parameters.", overallStopwatch.Elapsed);
    }
}
