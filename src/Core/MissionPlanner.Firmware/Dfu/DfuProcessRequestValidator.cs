using System.Text.RegularExpressions;

namespace MissionPlanner.Firmware.Dfu;

internal static partial class DfuProcessRequestValidator
{
    public static string? Validate(DfuProcessRequest request)
    {
        if (!Path.IsPathFullyQualified(request.ExecutablePath) ||
            !string.Equals(Path.GetFileName(request.ExecutablePath), "STM32_Programmer_CLI.exe", StringComparison.OrdinalIgnoreCase))
            return "Only a fully qualified STM32_Programmer_CLI.exe path may be executed.";
        if (request.StartupTimeout <= TimeSpan.Zero || request.ExecutionTimeout <= TimeSpan.Zero)
            return "Process timeouts must be positive.";
        if (request.Arguments.Count is 0 or > 16 || request.Arguments.Any(argument =>
                string.IsNullOrEmpty(argument) || argument.Length > 4096 || argument.Any(char.IsControl)))
            return "Provider arguments are empty, excessive, or contain control characters.";

        return request.Purpose switch
        {
            DfuProcessPurpose.ValidateTool when request.Arguments is ["--version"] or ["--help"] => null,
            DfuProcessPurpose.ListDevices when request.Arguments is ["-l", "usb"] => null,
            DfuProcessPurpose.InspectDevice when request.Arguments is ["-c", var port] && UsbPortPattern().IsMatch(port) => null,
            DfuProcessPurpose.ProgramAndVerify when IsProgramAndVerify(request.Arguments) => null,
            _ => "Arguments do not match the selected controlled DFU process purpose."
        };
    }

    private static bool IsProgramAndVerify(IReadOnlyList<string> arguments) =>
        arguments is ["-c", var port, "-w", var file, "-v"] &&
        UsbPortPattern().IsMatch(port) &&
        Path.IsPathFullyQualified(file) &&
        string.Equals(Path.GetExtension(file), ".hex", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^port=usb[1-9]\d*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UsbPortPattern();
}
