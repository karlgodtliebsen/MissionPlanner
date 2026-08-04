namespace MissionPlanner.Firmware.Dfu;

/// <summary>Builds version-aware, allow-listed STM32CubeProgrammer CLI requests.</summary>
public sealed class Stm32CubeProgrammerCommandBuilder
{
    /// <summary>Builds the documented USB interface-list command.</summary>
    public DfuProcessRequest BuildListDevices(string executablePath, TimeSpan startupTimeout, TimeSpan executionTimeout) =>
        new(executablePath, ["-l", "usb"], startupTimeout, executionTimeout, Purpose: DfuProcessPurpose.ListDevices);

    /// <summary>Builds a non-mutating connection command for one USB provider index.</summary>
    public DfuProcessRequest BuildInspectDevice(string executablePath, int usbIndex, TimeSpan startupTimeout, TimeSpan executionTimeout) =>
        new(executablePath, ["-c", UsbPort(usbIndex)], startupTimeout, executionTimeout, Purpose: DfuProcessPurpose.InspectDevice);

    /// <summary>Builds the documented HEX write followed immediately by mandatory verification.</summary>
    public DfuProcessRequest BuildProgramAndVerify(string executablePath, int usbIndex, string hexPath, TimeSpan startupTimeout, TimeSpan executionTimeout) =>
        new(executablePath, ["-c", UsbPort(usbIndex), "-w", Path.GetFullPath(hexPath), "-v"], startupTimeout, executionTimeout,
            MayKillProcessTreeOnCancellation: false, Purpose: DfuProcessPurpose.ProgramAndVerify);

    /// <summary>Returns conservative capabilities for a validated provider version.</summary>
    public DfuProviderCapabilities GetCapabilities(Version version) => new(
        CanListDevices: true,
        CanInspectDevice: true,
        CanProgramIntelHex: true,
        CanVerify: true,
        CanDetach: false,
        CanSafelyCancelProgramming: false,
        ProviderVersion: version);

    private static string UsbPort(int usbIndex)
    {
        if (usbIndex <= 0) throw new ArgumentOutOfRangeException(nameof(usbIndex));
        return $"port=usb{usbIndex}";
    }
}
