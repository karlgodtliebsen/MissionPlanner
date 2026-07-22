using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Setup;

/// <summary>Projects servo output functions with live PWM and applies confirmed, readback-verified writes.</summary>
public sealed class ServoOutputConfigurationService : IServoOutputConfigurationService
{
    private const int MaximumOutputs = 16;
    private static readonly TimeSpan staleWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan readbackTimeout = TimeSpan.FromSeconds(4);
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterMetadataService metadataService;
    private readonly IVehicleParameterService parameterService;
    private readonly IDateTimeProvider clock;
    private readonly ILogger<ServoOutputConfigurationService> logger;

    /// <summary>Initializes the servo output configuration service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="metadataService">The firmware parameter metadata service.</param>
    /// <param name="parameterService">The parameter protocol service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    public ServoOutputConfigurationService(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterMetadataService metadataService,
        IVehicleParameterService parameterService,
        IDateTimeProvider clock,
        ILogger<ServoOutputConfigurationService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.parameterRegistry = parameterRegistry;
        this.metadataService = metadataService;
        this.parameterService = parameterService;
        this.clock = clock;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<ServoOutputConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var state = RequireActiveVehicle(vehicleId);
        var values = parameterRegistry.GetAllParameters(vehicleId);
        var metadata = await metadataService.GetAllMetadataAsync(vehicleId, cancellationToken).ConfigureAwait(false);
        var options = metadata.TryGetValue("SERVO1_FUNCTION", out var definition)
            ? definition.GetValueOptions().OrderBy(option => option.Key).Select(option => new ServoFunctionOption((int)option.Key, option.Value)).ToArray()
            : [];
        var optionLookup = options.ToDictionary(option => option.Value, option => option.Name);
        var outputs = state.Radio.ServoOutputsRaw;
        var stale = state.Radio.ServoObservedAt is null || clock.UtcNow - state.Radio.ServoObservedAt > staleWindow;

        var result = new List<ServoOutputInfo>();
        for (var output = 1; output <= MaximumOutputs; output++)
        {
            if (!values.TryGetValue($"SERVO{output}_FUNCTION", out var functionParameter))
            {
                continue;
            }

            var function = (int)Math.Round(functionParameter.Value);
            int? livePwm = outputs is not null && output <= outputs.Count ? outputs[output - 1] : null;
            result.Add(new ServoOutputInfo(
                output,
                function,
                optionLookup.TryGetValue(function, out var name) ? name : $"Function {function}",
                livePwm,
                stale));
        }

        return new ServoOutputConfiguration(vehicleId, result, options);
    }

    /// <inheritdoc />
    public async Task<ServoOutputApplyResult> SetFunctionAsync(VehicleId vehicleId, int output, int functionValue, CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        var name = $"SERVO{output}_FUNCTION";
        if (parameterRegistry.GetParameter(vehicleId, name) is not { } parameter)
        {
            return new ServoOutputApplyResult(false, $"{name} is not available on the connected vehicle.");
        }

        logger.LogInformation("Assigning servo output {Output} to function {Function} on {VehicleId}.", output, functionValue, vehicleId);
        if (await WriteAndConfirmAsync(vehicleId, name, functionValue, parameter.Type, cancellationToken).ConfigureAwait(false))
        {
            return new ServoOutputApplyResult(true, $"Confirmed output {output} function by vehicle readback.");
        }

        return new ServoOutputApplyResult(false, $"Readback did not confirm output {output}. Reconnect, refresh, and verify before flying.");
    }

    private VehicleState RequireActiveVehicle(VehicleId vehicleId)
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }

        return state;
    }

    private async Task<bool> WriteAndConfirmAsync(VehicleId vehicleId, string name, int value, MavParamType type, CancellationToken cancellationToken)
    {
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == vehicleId && args.Parameter is { } parameter && parameter.Name == name && Math.Abs(parameter.Value - value) <= 0.5f)
            {
                readback.TrySetResult();
            }
        }

        parameterRegistry.Changed += OnChanged;
        try
        {
            if (!await parameterService.SetParameterAsync(vehicleId, name, value, type, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (parameterRegistry.GetParameter(vehicleId, name) is { } current && Math.Abs(current.Value - value) <= 0.5f)
            {
                return true;
            }

            await parameterService.RequestParameterAsync(vehicleId, name, cancellationToken).ConfigureAwait(false);
            await readback.Task.WaitAsync(readbackTimeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            parameterRegistry.Changed -= OnChanged;
        }
    }
}
