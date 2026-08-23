using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Calculates and applies safe MOT_SPIN_ARM and MOT_SPIN_MIN values.</summary>
public sealed class MotorSpinParameterService(
    IVehicleParameterRegistry parameterRegistry,
    IVehicleParameterMetadataService metadataService,
    IVehicleParameterService parameterService) : IMotorSpinParameterService
{
    private const string SpinArmName = "MOT_SPIN_ARM";
    private const string SpinMinName = "MOT_SPIN_MIN";
    private const double SpinArmMarginPercent = 2;
    private const double SpinMinMarginPercent = 3;
    private const double MaximumSetupPercent = 20;
    private const float ComparisonTolerance = 0.0005f;
    private static readonly TimeSpan readbackTimeout = TimeSpan.FromSeconds(4);

    /// <inheritdoc />
    public MotorSpinParameterState GetState(VehicleId vehicleId)
    {
        return new MotorSpinParameterState(
            parameterRegistry.GetParameter(vehicleId, SpinArmName)?.Value,
            parameterRegistry.GetParameter(vehicleId, SpinMinName)?.Value);
    }

    /// <inheritdoc />
    public MotorSpinRecommendation RecommendSpinArm(VehicleId vehicleId, double testThrottlePercent)
    {
        var state = GetState(vehicleId);
        if (!state.HasSpinArm)
        {
            return Failure(SpinArmName, "MOT_SPIN_ARM is not available on the connected firmware.");
        }

        if (!double.IsFinite(testThrottlePercent) || testThrottlePercent < 0 || testThrottlePercent >= MaximumSetupPercent)
        {
            return Failure(SpinArmName, "Motor-test throttle must be at least 0% and below 20% for this setup operation.");
        }

        var percent = testThrottlePercent + SpinArmMarginPercent;
        if (percent >= MaximumSetupPercent)
        {
            return Failure(SpinArmName, "The recommended MOT_SPIN_ARM would be 20% or higher; lower the test throttle first.");
        }

        var normalized = MotorSpinPercentage.ToNormalized(percent);
        if (state.SpinMinNormalized is { } spinMin && normalized >= spinMin - ComparisonTolerance)
        {
            return Failure(SpinArmName, "MOT_SPIN_ARM must remain below the current MOT_SPIN_MIN value.");
        }

        return Success(SpinArmName, percent);
    }

    /// <inheritdoc />
    public MotorSpinRecommendation RecommendSpinMin(VehicleId vehicleId)
    {
        var state = GetState(vehicleId);
        if (!state.HasSpinMin)
        {
            return Failure(SpinMinName, "MOT_SPIN_MIN is not available on the connected firmware.");
        }

        if (state.SpinArmNormalized is not { } spinArm)
        {
            return Failure(SpinMinName, "MOT_SPIN_ARM is unavailable, so a safe MOT_SPIN_MIN recommendation cannot be calculated.");
        }

        var percent = MotorSpinPercentage.ToPercent(spinArm) + SpinMinMarginPercent;
        if (percent >= MaximumSetupPercent)
        {
            return Failure(SpinMinName, "The recommended MOT_SPIN_MIN would be 20% or higher and is refused by this setup workflow.");
        }

        return Success(SpinMinName, percent);
    }

    /// <inheritdoc />
    public Task<MotorSpinWriteResult> SetSpinArmAsync(
        VehicleId vehicleId,
        double testThrottlePercent,
        CancellationToken cancellationToken = default)
    {
        return ApplyAsync(vehicleId, RecommendSpinArm(vehicleId, testThrottlePercent), cancellationToken);
    }

    /// <inheritdoc />
    public Task<MotorSpinWriteResult> SetSpinMinAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        return ApplyAsync(vehicleId, RecommendSpinMin(vehicleId), cancellationToken);
    }

    private async Task<MotorSpinWriteResult> ApplyAsync(
        VehicleId vehicleId,
        MotorSpinRecommendation recommendation,
        CancellationToken cancellationToken)
    {
        if (!recommendation.Success || recommendation.NormalizedValue is not { } value)
        {
            return new MotorSpinWriteResult(false, recommendation.Message);
        }

        var parameter = parameterRegistry.GetParameter(vehicleId, recommendation.ParameterName);
        if (parameter is null)
        {
            return new MotorSpinWriteResult(false, $"{recommendation.ParameterName} is not available on the connected firmware.");
        }

        var metadata = await metadataService.GetMetadataAsync(vehicleId, recommendation.ParameterName, cancellationToken).ConfigureAwait(false);
        if (metadata?.ReadOnly == true || metadata?.MinValue is { } minimum && value < minimum - ComparisonTolerance ||
            metadata?.MaxValue is { } maximum && value > maximum + ComparisonTolerance)
        {
            return new MotorSpinWriteResult(false, $"The recommended {recommendation.ParameterName} value is outside its firmware metadata limits.");
        }

        if (!await WriteAndConfirmAsync(vehicleId, parameter, value, cancellationToken).ConfigureAwait(false))
        {
            return new MotorSpinWriteResult(false, $"The vehicle did not confirm {recommendation.ParameterName}; the previous value remains active and the operation can be retried.");
        }

        return new MotorSpinWriteResult(true, $"Confirmed {recommendation.ParameterName}: {recommendation.Percent:0.#}% ({value:0.###}).");
    }

    private async Task<bool> WriteAndConfirmAsync(
        VehicleId vehicleId,
        VehicleParameter parameter,
        float value,
        CancellationToken cancellationToken)
    {
        var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == vehicleId && args.Parameter is { } changed &&
                changed.Name == parameter.Name && Math.Abs(changed.Value - value) <= ComparisonTolerance)
            {
                readback.TrySetResult();
            }
        }

        parameterRegistry.Changed += OnChanged;
        try
        {
            if (!await parameterService.SetParameterAsync(vehicleId, parameter.Name, value, parameter.Type, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (parameterRegistry.GetParameter(vehicleId, parameter.Name) is { } current &&
                Math.Abs(current.Value - value) <= ComparisonTolerance)
            {
                return true;
            }

            await parameterService.RequestParameterAsync(vehicleId, parameter.Name, cancellationToken).ConfigureAwait(false);
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

    private static MotorSpinRecommendation Success(string parameterName, double percent)
    {
        return new MotorSpinRecommendation(
            true,
            parameterName,
            percent,
            MotorSpinPercentage.ToNormalized(percent),
            $"Recommended {parameterName}: {percent:0.#}%.");
    }

    private static MotorSpinRecommendation Failure(string parameterName, string message)
    {
        return new MotorSpinRecommendation(false, parameterName, null, null, message);
    }
}
