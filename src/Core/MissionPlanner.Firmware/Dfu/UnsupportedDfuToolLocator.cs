namespace MissionPlanner.Firmware.Dfu;

internal sealed class UnsupportedDfuToolLocator : IDfuToolLocator
{
    public Task<DfuToolStatus> LocateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DfuToolStatus(DfuToolAvailability.NotInstalled, Diagnostic: "STM32CubeProgrammer discovery is supported on Windows only."));
    }
}
