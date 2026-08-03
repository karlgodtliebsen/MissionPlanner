namespace MissionPlanner.Firmware.Presentation;

/// <summary>Implements deterministic firmware-page presentation policy.</summary>
public sealed class FirmwarePageModeResolver : IFirmwarePageModeResolver
{
    /// <inheritdoc />
    public FirmwarePageState Resolve(FirmwarePageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsOperationInProgress)
        {
            return new FirmwarePageState(
                FirmwarePageMode.OperationInProgress,
                false, false, false, false, false, false,
                false, false, true, false, context.OperationState);
        }

        if (!context.IsDirectInstallationSupported)
        {
            return new FirmwarePageState(
                FirmwarePageMode.UnsupportedPlatform,
                false, false, false, false, false, false,
                false, false, false, true, null);
        }

        if (context.IsVehicleConnected)
        {
            var canUpdateBootloader = !context.IsVehicleArmed && context.IsSupportedArduPilot;
            return new FirmwarePageState(
                FirmwarePageMode.Connected,
                true, false, false, false, false, false,
                false, canUpdateBootloader, false, true, null);
        }

        return new FirmwarePageState(
            FirmwarePageMode.Disconnected,
            false, true, true, true, true, true,
            true, false, false, true, null);
    }
}
