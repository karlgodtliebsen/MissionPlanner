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

        var receivedCount = 0;
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
                receivedCount++;
                totalCount = param.Count;

                logger.LogDebug("Received parameter {Index}/{Count}: {Name} = {Value}", param.Index + 1, param.Count, param.Name, param.Value);

                // Invoke callback
                onParameterReceived?.Invoke(param);

                // Report progress
                if (totalCount > 0)
                {
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
                if (totalCount > 0 && receivedCount >= totalCount)
                {
                    completed = true;
                }
            }

            if (!completed)
            {
                if (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("Parameter stream timeout after {Duration}s. Received {Received}/{Total}", stopwatch.Elapsed.TotalSeconds, receivedCount, totalCount);

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

            var result = await StreamAllParametersAsync(vehicleId, progress, null, actualTimeout, cancellationToken);

            if (result.Success)
            {
                return result;
            }

            // If it's not the last attempt, continue retrying
            if (attempt < maxRetries)
            {
                logger.LogWarning("Parameter stream failed on attempt {Attempt}: {Error}. Retrying...", attempt + 1, result.ErrorMessage);
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
