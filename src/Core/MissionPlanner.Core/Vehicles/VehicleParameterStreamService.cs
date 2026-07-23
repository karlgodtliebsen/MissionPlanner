using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Parameter streaming service that subscribes to domain events.
/// This reduces the layers and ensures we don't miss any parameters.
/// </summary>
public sealed class VehicleParameterStreamService : IVehicleParameterStreamService
{
    private readonly IVehicleParameterService parameterService;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IDomainEventHub domainEventHub;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ILogger<VehicleParameterStreamService> logger;
    private readonly IArduPilotPackedParameterDecoder packedParameterDecoder;
    private readonly IVehicleFileSystemService? fileSystemService;
    private static readonly TimeSpan defaultTimeout = TimeSpan.FromSeconds(30);
    private const string PackedParameterPath = "@PARAM/param.pck";

    /// <summary>
    /// Parameter streaming service that subscribes to domain events.
    /// This reduces the layers and ensures we don't miss any parameters.
    /// </summary>
    public VehicleParameterStreamService(IVehicleParameterService parameterService,
        IVehicleParameterRegistry parameterRegistry,
        IDomainEventHub domainEventHub,
        IDateTimeProvider dateTimeProvider,
        ILogger<VehicleParameterStreamService> logger,
        IArduPilotPackedParameterDecoder packedParameterDecoder,
        IVehicleFileSystemService? fileSystemService = null)
    {
        this.parameterService = parameterService;
        this.parameterRegistry = parameterRegistry;
        this.domainEventHub = domainEventHub;
        this.dateTimeProvider = dateTimeProvider;
        this.logger = logger;
        this.packedParameterDecoder = packedParameterDecoder;
        this.fileSystemService = fileSystemService;
    }


    private async Task<ParameterStreamResult> StreamAllParametersAsync(
        VehicleId vehicleId,
        IProgress<ParameterStreamProgress>? progress = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var actualTimeout = timeout ?? defaultTimeout;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Starting parameter stream for vehicle {VehicleId} with timeout {Timeout}s", vehicleId, actualTimeout.TotalSeconds);

        // Clear existing parameters
        parameterRegistry.ClearParameters(vehicleId);

        var receivedIndices = new HashSet<ushort>();
        var totalCount = 0;
        var lastReceivedTime = dateTimeProvider.UtcNow;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(actualTimeout);

            using var subscription = domainEventHub.SubscribeDomainEventAsync<VehicleParameterReceived>(async (parameter, ct) =>
            {
                // Filter by vehicle
                if (parameter.VehicleId.SystemId != vehicleId.SystemId || parameter.VehicleId.ComponentId != vehicleId.ComponentId)
                {
                    return;
                }

                lastReceivedTime = dateTimeProvider.UtcNow;
                totalCount = parameter.Parameter.Count;

                // Track indices, excluding 65535 (special invalid index)
                if (parameter.Parameter.Index != 65535)
                {
                    receivedIndices.Add(parameter.Parameter.Index);
                }

                // Report progress (count only non-65535 indices)
                if (totalCount > 0)
                {
                    var receivedCount = receivedIndices.Count;
                    var percentComplete = (int)(receivedCount / (double)totalCount * 100);
                    var isComplete = receivedCount >= totalCount;

                    progress?.Report(new ParameterStreamProgress(receivedCount, totalCount, percentComplete, isComplete));
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Received parameter {Index}/{Count}: {Name} = {Value} (non-65535: {Received}/{Total})",
                            parameter.Parameter.Index, parameter.Parameter.Count,
                            parameter.Name, parameter.Parameter.Value,
                            receivedCount, totalCount);
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

            // Active polling loop (like original MissionPlanner)
            var stableCount = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(10, cts.Token);

                // Check if complete (all non-65535 indices received)
                if (totalCount > 0 && receivedIndices.Count >= totalCount)
                {
                    stableCount++;

                    // Wait for stability (no new params for 500ms)
                    if (stableCount >= 5)
                    {
                        var allParams = parameterRegistry.GetAllParameters(vehicleId);
                        logger.LogInformation("Successfully received {Received} unique indices out of {Total} parameters in {Duration}s", receivedIndices.Count, allParams.Count, stopwatch.Elapsed.TotalSeconds);

                        return ParameterStreamResult.CreateSuccess(allParams, totalCount, stopwatch.Elapsed);
                    }
                }
                else
                {
                    stableCount = 0;
                }

                // Check for stall (no new params for 4 seconds)
                if (totalCount > 0 && (dateTimeProvider.UtcNow - lastReceivedTime).TotalSeconds >= 4)
                {
                    logger.LogWarning("No new parameters for 4 seconds. Received {Received}/{Total} unique indices", receivedIndices.Count, totalCount);
                    break;
                }
            }

            // Timeout or stall
            var finalParams = parameterRegistry.GetAllParameters(vehicleId);
            logger.LogWarning("Parameter stream incomplete after {Duration}s. Received {Received}/{Total} unique indices ({Stored} stored)",
                stopwatch.Elapsed.TotalSeconds, receivedIndices.Count, totalCount, finalParams.Count);

            return ParameterStreamResult.CreateFailure($"Incomplete: received {receivedIndices.Count}/{totalCount} unique indices", stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            var finalParams = parameterRegistry.GetAllParameters(vehicleId);
            if (totalCount > 0 && receivedIndices.Count >= totalCount)
            {
                // Got all parameters but timeout occurred
                logger.LogInformation("Received all {Total} parameters (timeout occurred after completion)", receivedIndices.Count);
                return ParameterStreamResult.CreateSuccess(finalParams, totalCount, stopwatch.Elapsed);
            }

            return ParameterStreamResult.CreateFailure($"Timeout: received {receivedIndices.Count}/{totalCount} unique indices", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming parameters for vehicle {VehicleId}", vehicleId);
            return ParameterStreamResult.CreateFailure($"Error: {ex.Message}", stopwatch.Elapsed);
        }
    }

    /// <inheritdoc/>
    public async Task<ParameterStreamResult> StreamAllParametersWithRetryAsync(VehicleId vehicleId, IProgress<ParameterStreamProgress>? progress = null, int maxRetries = 3, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();

        progress?.Report(new ParameterStreamProgress(Message: "Reading parameters using MAVFTP..."));

        var ftpResult = await TryDownloadPackedParametersAsync(vehicleId, progress, overallStopwatch, cancellationToken).ConfigureAwait(false);
        if (ftpResult is not null)
        {
            return ftpResult;
        }

        progress?.Report(new ParameterStreamProgress(Message: "Reading parameters using MAVLink stream..."));

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ParameterStreamResult.CreateFailure("Operation cancelled", overallStopwatch.Elapsed);
            }

            if (attempt > 0)
            {
                logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} for vehicle {VehicleId}", attempt, maxRetries, vehicleId);

                // Small delay before retry
                await Task.Delay(100, cancellationToken);
            }

            var result = await StreamAllParametersAsync(vehicleId, progress, timeout, cancellationToken);

            if (result.Success)
            {
                progress?.Report(new ParameterStreamProgress(result.Parameters.Count, result.TotalCount, 100, true));
                return result;
            }

            // Log progress
            var currentParams = parameterRegistry.GetAllParameters(vehicleId);
            logger.LogInformation("Attempt {Attempt} incomplete: {Stored} parameters stored", attempt + 1, currentParams.Count);
        }

        // Final check
        var finalParams = parameterRegistry.GetAllParameters(vehicleId);
        var finalCount = parameterRegistry.GetParameterCount(vehicleId) ?? 0;

        logger.LogError("Failed after {Retries} retries. Stored: {Stored}, Expected: {Total}", maxRetries + 1, finalParams.Count, finalCount);

        progress?.Report(new ParameterStreamProgress(finalParams.Count, finalCount, finalParams.Count * 100 / finalCount, true, "Parameter loading failed."));

        return ParameterStreamResult.CreateFailure($"Failed after {maxRetries + 1} attempts. Stored {finalParams.Count}/{finalCount} parameters.", overallStopwatch.Elapsed);
    }

    private async Task<ParameterStreamResult?> TryDownloadPackedParametersAsync(VehicleId vehicleId, IProgress<ParameterStreamProgress>? progress, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        if (fileSystemService is null)
        {
            logger.LogDebug("MAVFTP parameter loading is unavailable because no file-system service is attached to the connection.");
            return null;
        }

        try
        {
            logger.LogInformation("Loading parameters for {VehicleId} through MAVFTP {Path}.", vehicleId, PackedParameterPath);
            using var packedFile = new MemoryStream();
            await fileSystemService.DownloadFileAsync(vehicleId, PackedParameterPath, packedFile, cancellationToken: cancellationToken).ConfigureAwait(false);

            var parameters = packedParameterDecoder.Decode(packedFile.GetBuffer().AsSpan(0, checked((int)packedFile.Length)));
            if (parameters.Count == 0)
            {
                throw new InvalidDataException("ArduPilot packed parameter file contained no parameters.");
            }

            parameterRegistry.ClearParameters(vehicleId);
            for (var index = 0; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                parameterRegistry.StoreParameter(vehicleId, parameter, cancellationToken);
                var received = index + 1;
                progress?.Report(new ParameterStreamProgress(received, parameters.Count, received * 100 / parameters.Count, received == parameters.Count));
            }

            var stored = parameterRegistry.GetAllParameters(vehicleId);
            logger.LogInformation("Loaded {Count} parameters for {VehicleId} through MAVFTP in {Duration}s.", stored.Count, vehicleId, stopwatch.Elapsed.TotalSeconds);
            return ParameterStreamResult.CreateSuccess(stored, parameters.Count, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "MAVFTP packed parameter loading is unavailable for {VehicleId}; falling back to PARAM_REQUEST_LIST.", vehicleId);
            return null;
        }
    }
}
