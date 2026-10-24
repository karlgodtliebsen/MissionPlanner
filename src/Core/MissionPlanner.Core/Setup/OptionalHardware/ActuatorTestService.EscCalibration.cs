using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Firmware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

public sealed partial class ActuatorTestService
{
    /// <inheritdoc />
    public async Task<MotorTestResult> SetEscCalibrationPendingAsync(VehicleId vehicleId, bool prepare,
        bool safetyConfirmed, CancellationToken cancellationToken = default)
    {
        var state = activeVehicle.State;
        if (!safetyConfirmed || !activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || state is null ||
            state.IsArmed || state.Identity.Firmware.Family != FirmwareFamily.ArduCopter ||
            clock.UtcNow < state.LastHeartbeatAt || clock.UtcNow - state.LastHeartbeatAt > TimeSpan.FromSeconds(3))
        {
            return new(false, "ESC calibration preparation requires a current disarmed Copter and explicit safety confirmation.");
        }
        var parameter = parameterRegistry.GetParameter(vehicleId, "ESC_CALIBRATION");
        if (parameter is null || prepare && !GetEscCalibrationGuidance(vehicleId).Applicable)
        {
            return new(false, "This firmware/protocol does not report supported analog ESC calibration.");
        }
        if (!operationGate.TryAcquire(vehicleId, "ESC calibration preparation", out var lease))
        {
            return new(false, "Another vehicle operation is in progress.");
        }
        using (lease)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken, timeout.Token))
        {
            var expected = prepare ? 3f : 0f;
            var readback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Changed(VehicleParameterChangedEventArgs args)
            {
                if (args.VehicleId == vehicleId && args.Parameter is { Name: "ESC_CALIBRATION" } value && value.Value == expected)
                {
                    readback.TrySetResult();
                }
            }
            parameterRegistry.Changed += Changed;
            try
            {
                if (!await session.ParameterService.SetParameterAsync(vehicleId, parameter.Name, expected, parameter.Type, lifetime.Token).ConfigureAwait(false))
                {
                    return new(false, "The vehicle rejected ESC calibration preparation.");
                }
                await session.ParameterService.RequestParameterAsync(vehicleId, parameter.Name, lifetime.Token).ConfigureAwait(false);
                await readback.Task.WaitAsync(lifetime.Token).ConfigureAwait(false);
                return new(true, prepare
                    ? "ESC_CALIBRATION=3 confirmed. With propellers removed, disconnect USB and battery, then connect the battery and follow the firmware's safety-button and tone sequence. Power-cycle normally afterwards. This is preparation, not proof of completed ESC calibration."
                    : "ESC_CALIBRATION=0 confirmed: normal startup restored.");
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return new(false, "ESC calibration readback timed out. The request may have been stored; refresh ESC_CALIBRATION before reconnecting flight power.");
            }
            finally
            {
                parameterRegistry.Changed -= Changed;
            }
        }
    }
}
