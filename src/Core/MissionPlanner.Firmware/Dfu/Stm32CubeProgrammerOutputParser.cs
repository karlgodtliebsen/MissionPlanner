using System.Globalization;
using System.Text.RegularExpressions;

namespace MissionPlanner.Firmware.Dfu;

internal static partial class Stm32CubeProgrammerOutputParser
{
    internal sealed record UsbDevice(int Index, string? SerialNumber);

    public static IReadOnlyList<UsbDevice> ParseUsbDevices(IEnumerable<string> lines)
    {
        var devices = new Dictionary<int, string?>();
        int? current = null;
        foreach (var line in lines)
        {
            var indexMatch = UsbIndexPattern().Match(line);
            if (indexMatch.Success && int.TryParse(indexMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var index) && index > 0)
            {
                current = index;
                devices.TryAdd(index, null);
            }
            var serialMatch = SerialPattern().Match(line);
            if (current is int currentIndex && serialMatch.Success) devices[currentIndex] = serialMatch.Groups[1].Value.Trim();
        }
        return devices.Select(item => new UsbDevice(item.Key, item.Value)).OrderBy(item => item.Index).ToArray();
    }

    public static (string? DeviceId, string? Revision, long? FlashBytes) ParseDeviceInformation(IEnumerable<string> lines)
    {
        string? deviceId = null;
        string? revision = null;
        long? flashBytes = null;
        foreach (var line in lines)
        {
            deviceId ??= MatchValue(DeviceIdPattern(), line);
            revision ??= MatchValue(RevisionPattern(), line);
            var flash = FlashPattern().Match(line);
            if (flash.Success && long.TryParse(flash.Groups[1].Value, CultureInfo.InvariantCulture, out var kilobytes)) flashBytes = kilobytes * 1024;
        }
        return (deviceId, revision, flashBytes);
    }

    public static double? ParsePercentage(string line)
    {
        var match = PercentagePattern().Match(line);
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0, 100)
            : null;
    }

    public static DfuProgrammingOutcome Classify(DfuProcessResult result)
    {
        var text = string.Join('\n', result.Output.Select(item => item.Text));
        if (ContainsAny(text, "no usb device", "no stm32 target", "device not found")) return DfuProgrammingOutcome.NoDfuDevice;
        if (ContainsAny(text, "failed to connect", "connection failed", "cannot connect")) return DfuProgrammingOutcome.ConnectionFailed;
        if (ContainsAny(text, "file does not exist", "file format not supported", "failed to open file", "invalid file")) return DfuProgrammingOutcome.FileRejected;
        if (ContainsAny(text, "erase failed", "failed to erase", "erasing memory failed")) return DfuProgrammingOutcome.EraseFailed;
        if (ContainsAny(text, "verification failed", "verify failed", "data mismatch")) return DfuProgrammingOutcome.VerificationFailed;
        if (ContainsAny(text, "download failed", "programming failed", "failed to download")) return DfuProgrammingOutcome.ProgrammingFailed;

        var programmingSucceeded = ContainsAny(text, "file download complete", "download completed successfully", "programming complete");
        var verificationSucceeded = ContainsAny(text, "download verified successfully", "verification successful", "verification completed successfully");
        return result.ExitCode == 0 && !result.TimedOut && !result.OutputTruncated && programmingSucceeded && verificationSucceeded
            ? DfuProgrammingOutcome.Succeeded
            : programmingSucceeded ? DfuProgrammingOutcome.VerificationFailed : DfuProgrammingOutcome.ProgrammingFailed;
    }

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    private static string? MatchValue(Regex pattern, string line)
    {
        var match = pattern.Match(line);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"\bUSB\s*([1-9]\d*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UsbIndexPattern();
    [GeneratedRegex(@"serial\s*(?:number)?\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SerialPattern();
    [GeneratedRegex(@"device\s*id\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeviceIdPattern();
    [GeneratedRegex(@"revision\s*(?:id)?\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RevisionPattern();
    [GeneratedRegex(@"flash\s*(?:size)?\s*:\s*(\d+)\s*(?:KBytes|KB)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FlashPattern();
    [GeneratedRegex(@"(?<!\d)(\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentagePattern();
}
