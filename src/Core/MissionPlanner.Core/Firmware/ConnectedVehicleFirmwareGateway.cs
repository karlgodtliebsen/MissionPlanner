using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Connected;

namespace MissionPlanner.Core.Firmware;

/// <summary>Adapts the existing acknowledged command service to connected bootloader updates.</summary>
public sealed class ConnectedVehicleFirmwareGateway(IActiveVehicleContext activeVehicle, IVehicleCommandService commandService) : IConnectedVehicleFirmwareGateway
{
    private const ushort FlashBootloaderCommand = 42650;
    private const float ArduPilotConfirmation = 290876;

    /// <inheritdoc />
    public bool IsConnected => activeVehicle.IsOnline && activeVehicle.VehicleId is not null;

    /// <inheritdoc />
    public bool IsArmed => activeVehicle.State?.IsArmed == true;

    /// <inheritdoc />
    public bool IsSupportedArduPilot => activeVehicle.State?.Identity.Firmware.Family is
        FirmwareFamily.ArduCopter or FirmwareFamily.ArduPlane or FirmwareFamily.Rover or
        FirmwareFamily.ArduSub or FirmwareFamily.AntennaTracker or FirmwareFamily.Blimp;

    /// <inheritdoc />
    public async Task<ConnectedFirmwareCommandResult> FlashEmbeddedBootloaderAsync(CancellationToken cancellationToken = default)
    {
        var vehicleId = activeVehicle.VehicleId;
        if (vehicleId is null)
        {
            return ConnectedFirmwareCommandResult.Failed;
        }

        var response = await commandService.ExecuteExpertAsync(
            new ExpertVehicleCommand(vehicleId.Value, FlashBootloaderCommand, [0, 0, 0, 0, ArduPilotConfirmation, 0, 0]),
            true,
            cancellationToken).ConfigureAwait(false);
        return response.Result switch
        {
            VehicleCommandResult.Accepted => ConnectedFirmwareCommandResult.Accepted,
            VehicleCommandResult.TemporarilyRejected or VehicleCommandResult.Busy => ConnectedFirmwareCommandResult.TemporarilyRejected,
            VehicleCommandResult.Denied => ConnectedFirmwareCommandResult.Denied,
            VehicleCommandResult.Unsupported => ConnectedFirmwareCommandResult.Unsupported,
            VehicleCommandResult.Timeout => ConnectedFirmwareCommandResult.Timeout,
            var _ => ConnectedFirmwareCommandResult.Failed
        };
    }
}
