using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects servo output functions with live PWM and applies confirmed, readback-verified writes.</summary>
public sealed class ServoOutputConfigurationService : IServoOutputConfigurationService
{
    private const int MaximumOutputs = 16;
    private const int DefaultMinimumPwm = 800;
    private const int DefaultMaximumPwm = 2200;
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
            var prefix = $"SERVO{output}_";
            if (!TryGetInteger(values, prefix + "FUNCTION", out var function) ||
                !TryGetInteger(values, prefix + "REVERSED", out var reversed) ||
                !TryGetInteger(values, prefix + "MIN", out var minimum) ||
                !TryGetInteger(values, prefix + "TRIM", out var trim) ||
                !TryGetInteger(values, prefix + "MAX", out var maximum))
            {
                continue;
            }

            int? livePwm = outputs is not null && output <= outputs.Count ? outputs[output - 1] : null;
            var (allowedMinimum, allowedMaximum) = ResolvePwmRange(metadata, prefix);
            result.Add(new ServoOutputInfo(
                output,
                function,
                optionLookup.TryGetValue(function, out var name) ? name : $"Function {function}",
                reversed != 0,
                minimum,
                trim,
                maximum,
                livePwm,
                stale,
                allowedMinimum,
                allowedMaximum));
        }

        return new ServoOutputConfiguration(vehicleId, result, options);
    }

    /// <inheritdoc />
    public async Task<ServoOutputApplyResult> SetOutputAsync(VehicleId vehicleId, ServoOutputSettings settings, CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        var writes = new (string Suffix, int Value)[]
        {
            ("REVERSED", settings.Reversed ? 1 : 0),
            ("FUNCTION", settings.FunctionValue),
            ("MIN", settings.MinimumPwm),
            ("TRIM", settings.TrimPwm),
            ("MAX", settings.MaximumPwm)
        };

        foreach (var (suffix, value) in writes)
        {
            var name = $"SERVO{settings.ChannelNumber}_{suffix}";
            if (parameterRegistry.GetParameter(vehicleId, name) is not { } parameter)
            {
                return new ServoOutputApplyResult(false, $"{name} is not available on the connected vehicle.");
            }

            if (Math.Abs(parameter.Value - value) <= 0.5f)
            {
                continue;
            }

            logger.LogInformation("Assigning {Parameter} value {Value} on {VehicleId}.", name, value, vehicleId);
            if (!await WriteAndConfirmAsync(vehicleId, name, value, parameter.Type, cancellationToken).ConfigureAwait(false))
            {
                return new ServoOutputApplyResult(false, $"Readback did not confirm {name}. Correct the value and retry.");
            }
        }

        return new ServoOutputApplyResult(true, $"Confirmed output {settings.ChannelNumber} settings by vehicle readback.");
    }

    /// <inheritdoc />
    public async Task<ServoOutputApplyResult> SetFunctionAsync(
        VehicleId vehicleId,
        int output,
        int functionValue,
        CancellationToken cancellationToken = default)
    {
        _ = RequireActiveVehicle(vehicleId);
        var name = $"SERVO{output}_FUNCTION";
        return parameterRegistry.GetParameter(vehicleId, name) is not { } parameter
            ? new ServoOutputApplyResult(false, $"{name} is not available on the connected vehicle.")
            : await WriteAndConfirmAsync(vehicleId, name, functionValue, parameter.Type, cancellationToken).ConfigureAwait(false)
            ? new ServoOutputApplyResult(true, $"Confirmed output {output} function by vehicle readback.")
            : new ServoOutputApplyResult(false, $"Readback did not confirm {name}. Correct the value and retry.");
    }

    private static bool TryGetInteger(IReadOnlyDictionary<string, VehicleParameter> values, string name, out int value)
    {
        if (values.TryGetValue(name, out var parameter))
        {
            value = (int)Math.Round(parameter.Value);
            return true;
        }

        value = 0;
        return false;
    }

    private static (int Minimum, int Maximum) ResolvePwmRange(
        IReadOnlyDictionary<string, ParameterMetadata> metadata,
        string prefix)
    {
        var definitions = new[] { prefix + "MIN", prefix + "TRIM", prefix + "MAX" }
            .Select(name => metadata.TryGetValue(name, out var definition) ? definition : null)
            .Where(definition => definition is not null)
            .ToArray();
        var minimum = definitions.Select(definition => definition!.MinValue).FirstOrDefault(value => value.HasValue);
        var maximum = definitions.Select(definition => definition!.MaxValue).FirstOrDefault(value => value.HasValue);
        return (
            minimum is { } min ? (int)Math.Ceiling(min) : DefaultMinimumPwm,
            maximum is { } max ? (int)Math.Floor(max) : DefaultMaximumPwm);
    }

    private VehicleState RequireActiveVehicle(VehicleId vehicleId)
    {
        return !activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || activeVehicle.State is not { } state
            ? throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.")
            : state;
    }

    private async Task<bool> WriteAndConfirmAsync(VehicleId vehicleId, string name, int value, MavParamType type, CancellationToken cancellationToken)
    {
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnChanged(VehicleParameterChangedEventArgs args)
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
