namespace MissionPlanner.Core.Setup.Reporting;

/// <summary>Existing controller discovery observations; capturing these inputs never starts a scan.</summary>
/// <param name="Connected">Whether vehicle telemetry is connected.</param>
/// <param name="SerialPorts">Observed candidate serial ports.</param>
/// <param name="DfuDevices">Observed STM32 USB endpoints.</param>
/// <param name="SerialStatus">Serial discovery diagnostic.</param>
/// <param name="DfuStatus">DFU provider/tool diagnostic.</param>
/// <param name="RuntimeIdentity">Protocol-reported runtime identity.</param>
/// <param name="DfuIdentity">Selected USB endpoint identity and its limitations.</param>
/// <param name="Refreshing">Whether discovery is in progress.</param>
/// <param name="Installing">Whether a firmware operation is in progress.</param>
/// <param name="InstallationSupported">Whether the current host supports direct installation.</param>
public sealed record FirmwareDiscoveryEvidence(bool Connected, IReadOnlyList<string> SerialPorts,
    IReadOnlyList<string> DfuDevices, string SerialStatus, string DfuStatus, string RuntimeIdentity,
    string DfuIdentity, bool Refreshing, bool Installing, bool InstallationSupported);

/// <summary>Derives reporting and next-step guidance from existing firmware discovery evidence.</summary>
public static class FirmwareDiscoveryReporting
{
    /// <summary>Builds a report without treating a candidate USB endpoint as proven target compatibility.</summary>
    public static SetupReport Create(FirmwareDiscoveryEvidence evidence)
    {
        var next = !evidence.InstallationSupported
            ? "Direct installation is unavailable on this platform. Use the Windows desktop app."
            : evidence.Installing
                ? "A firmware operation is in progress. Follow its progress dialog and keep the controller powered."
            : evidence.Refreshing
                ? "Discovery is running. Wait for the available controller workflows to update."
            : evidence.DfuDevices.Count > 0
                ? "Review the selected DFU endpoint and tool readiness in Configuration."
            : evidence.SerialPorts.Count > 0
                ? "Use Configuration to probe the selected controller, prepare firmware and choose the required boot transition."
                : "Attach a controller by USB and refresh devices in Configuration. Firmware can be browsed and prepared without a controller.";
        return new SetupReport("Firmware installation", "Review controller discovery, identity and tool readiness before preparing an upgrade.",
            null, evidence.Refreshing ? "Controller discovery is running." : "Latest controller discovery evidence.",
            [
                new("Vehicle telemetry", evidence.Connected ? "Connected" : "Not connected"),
                new("Serial candidates", evidence.SerialPorts.Count == 0 ? "None detected" : string.Join(", ", evidence.SerialPorts)),
                new("Serial discovery", evidence.SerialStatus),
                new("STM32 DFU endpoints", evidence.DfuDevices.Count == 0 ? "None detected" : string.Join("; ", evidence.DfuDevices)),
                new("DFU tools", evidence.DfuStatus),
                new("Runtime identity", evidence.RuntimeIdentity),
                new("DFU identity", evidence.DfuIdentity),
            ],
            [next, "A serial port or USB endpoint does not establish firmware compatibility. Validate the selected controller and firmware before installation.",
                "A telemetry connection is not required for USB firmware installation. Only a session owning the selected serial port conflicts.",
                "Use the proven controller's boot transition or follow its BOOT/RESET procedure. STM32 ROM DFU normally appears as a USB endpoint, not a COM port."], []);
    }
}
