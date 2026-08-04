using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Programs and verifies validated Intel HEX artifacts through STM32CubeProgrammer CLI.</summary>
public sealed partial class Stm32CubeProgrammerCliDfuProgrammer(
    IDfuToolLocator toolLocator,
    IDfuProcessRunner processRunner,
    IIntelHexInspector hexInspector,
    Stm32CubeProgrammerCommandBuilder commands,
    IOptions<DfuOptions> options) : IDfuProgrammer
{
    /// <inheritdoc />
    public async Task<DfuProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var tool = await toolLocator.LocateAsync(cancellationToken).ConfigureAwait(false);
        return tool.Availability == DfuToolAvailability.Available && tool.Version is not null
            ? commands.GetCapabilities(tool.Version)
            : new DfuProviderCapabilities(false, false, false, false, false, false, tool.Version);
    }

    /// <inheritdoc />
    public async Task<DfuDeviceInformation> InspectAsync(DfuDeviceDescriptor device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        var tool = await RequireToolAsync(cancellationToken).ConfigureAwait(false);
        var index = await ResolveUsbIndexAsync(device, tool, cancellationToken).ConfigureAwait(false);
        if (index is null)
            return new DfuDeviceInformation(device, null, null, null, [], ["The selected Windows device could not be associated with a CubeProgrammer USB index."]);

        var configured = options.Value;
        var result = await processRunner.RunAsync(commands.BuildInspectDevice(tool.ExecutablePath!, index.Value,
            configured.ProviderStartupTimeout, configured.CubeProgrammerProbeTimeout), cancellationToken: cancellationToken).ConfigureAwait(false);
        var parsed = Stm32CubeProgrammerOutputParser.ParseDeviceInformation(result.Output.Select(item => item.Text));
        var warnings = result.ExitCode == 0 ? Array.Empty<string>() : ["CubeProgrammer could not inspect the selected USB DFU device."];
        return new DfuDeviceInformation(device with { ProviderUsbIndex = index }, parsed.DeviceId, parsed.Revision, parsed.FlashBytes, [], warnings, RawLog(result));
    }

    /// <inheritdoc />
    public async Task<DfuProgrammingResult> ProgramAndVerifyAsync(
        DfuProgrammingRequest request,
        IProgress<DfuProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Verify || request.RequestDetach || !await IsValidatedArtifactAsync(request.Artifact, cancellationToken).ConfigureAwait(false))
            return Failure(DfuProgrammingOutcome.FileRejected, DfuOperationState.InspectingHex, "FileRejected", "The artifact is not a validated local Intel HEX package.");

        DfuToolStatus tool;
        try { tool = await RequireToolAsync(cancellationToken).ConfigureAwait(false); }
        catch (InvalidOperationException exception) { return Failure(DfuProgrammingOutcome.ToolNotFound, DfuOperationState.LocatingTool, "ToolNotFound", exception.Message); }

        var index = await ResolveUsbIndexAsync(request.Device, tool, cancellationToken).ConfigureAwait(false);
        if (index is null) return Failure(DfuProgrammingOutcome.NoDfuDevice, DfuOperationState.WaitingForDevice, "NoDfuDevice", "The selected DFU device has no validated CubeProgrammer USB index.");

        var configured = options.Value;
        var processProgress = new InlineProgress<DfuProcessOutput>(line =>
        {
            var state = line.Text.Contains("verif", StringComparison.OrdinalIgnoreCase) ? DfuOperationState.Verifying : DfuOperationState.Programming;
            progress?.Report(new DfuProgress(state, state == DfuOperationState.Verifying ? "DfuVerifying" : "DfuProgramming",
                Stm32CubeProgrammerOutputParser.ParsePercentage(line.Text), TotalBytes: request.Artifact.Metadata.DataBytes, TechnicalDetail: line.Text));
        });
        var result = await processRunner.RunAsync(commands.BuildProgramAndVerify(tool.ExecutablePath!, index.Value, request.Artifact.LocalPath,
            configured.ProviderStartupTimeout, configured.ProviderProgrammingTimeout), processProgress, cancellationToken).ConfigureAwait(false);
        var outcome = Stm32CubeProgrammerOutputParser.Classify(result);
        var succeeded = outcome == DfuProgrammingOutcome.Succeeded;
        return new DfuProgrammingResult(
            succeeded ? DfuOperationState.Completed : FailureStage(outcome),
            succeeded,
            succeeded,
            false,
            succeeded ? null : new DfuFailure(outcome.ToString(), FailureStage(outcome), FailureMessage(outcome), result.FailureCode),
            RawLog(result),
            result.ExitCode,
            outcome);
    }

    private async Task<DfuToolStatus> RequireToolAsync(CancellationToken cancellationToken)
    {
        var tool = await toolLocator.LocateAsync(cancellationToken).ConfigureAwait(false);
        if (tool.Availability != DfuToolAvailability.Available || tool.ExecutablePath is null)
            throw new InvalidOperationException(tool.Diagnostic ?? "STM32CubeProgrammer CLI is unavailable.");
        return tool;
    }

    private async Task<int?> ResolveUsbIndexAsync(DfuDeviceDescriptor device, DfuToolStatus tool, CancellationToken cancellationToken)
    {
        if (device.ProviderUsbIndex is > 0) return device.ProviderUsbIndex;
        var direct = UsbProviderIdPattern().Match(device.ProviderId);
        if (direct.Success && int.TryParse(direct.Groups[1].Value, out var directIndex)) return directIndex;
        var configured = options.Value;
        var listed = await processRunner.RunAsync(commands.BuildListDevices(tool.ExecutablePath!, configured.ProviderStartupTimeout,
            configured.CubeProgrammerProbeTimeout), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (listed.ExitCode != 0) return null;
        var devices = Stm32CubeProgrammerOutputParser.ParseUsbDevices(listed.Output.Select(item => item.Text));
        if (!string.IsNullOrWhiteSpace(device.SerialNumber))
        {
            var matches = devices.Where(item => string.Equals(item.SerialNumber, device.SerialNumber, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 1) return matches[0].Index;
        }
        return devices.Count == 1 ? devices[0].Index : null;
    }

    private async Task<bool> IsValidatedArtifactAsync(DfuArtifact artifact, CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetExtension(artifact.LocalPath), ".hex", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(artifact.LocalPath) || artifact.Metadata.Ranges.Count == 0 || artifact.Metadata.DataBytes <= 0 ||
            artifact.Metadata.Ranges.Sum(range => (long)range.Data.Length) != artifact.Metadata.DataBytes ||
            !Sha256Pattern().IsMatch(artifact.Metadata.Sha256)) return false;
        try
        {
            await using var stream = new FileStream(artifact.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var inspected = await hexInspector.InspectAsync(stream, cancellationToken).ConfigureAwait(false);
            return string.Equals(inspected.Sha256, artifact.Metadata.Sha256, StringComparison.OrdinalIgnoreCase) &&
                   inspected.DataBytes == artifact.Metadata.DataBytes && inspected.LowestAddress == artifact.Metadata.LowestAddress &&
                   inspected.HighestAddress == artifact.Metadata.HighestAddress;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    private static DfuProgrammingResult Failure(DfuProgrammingOutcome outcome, DfuOperationState stage, string code, string message) =>
        new(DfuOperationState.Failed, false, false, false, new DfuFailure(code, stage, message), Outcome: outcome);
    private static DfuOperationState FailureStage(DfuProgrammingOutcome outcome) => outcome == DfuProgrammingOutcome.VerificationFailed ? DfuOperationState.Verifying : DfuOperationState.Programming;
    private static string FailureMessage(DfuProgrammingOutcome outcome) => outcome switch
    {
        DfuProgrammingOutcome.NoDfuDevice => "CubeProgrammer did not find the selected USB DFU device.",
        DfuProgrammingOutcome.ConnectionFailed => "CubeProgrammer could not connect to the selected USB DFU device.",
        DfuProgrammingOutcome.FileRejected => "CubeProgrammer rejected the Intel HEX file.",
        DfuProgrammingOutcome.EraseFailed => "CubeProgrammer reported an erase failure.",
        DfuProgrammingOutcome.VerificationFailed => "Programming was not followed by proven successful verification.",
        _ => "CubeProgrammer did not prove programming success."
    };
    private static string RawLog(DfuProcessResult result) => string.Join(Environment.NewLine,
        result.Output.Select(item => $"{item.Timestamp:O} {(item.IsError ? "stderr" : "stdout")}: {item.Text}"));

    [GeneratedRegex(@"^usb([1-9]\d*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UsbProviderIdPattern();
    [GeneratedRegex("^[0-9A-Fa-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
