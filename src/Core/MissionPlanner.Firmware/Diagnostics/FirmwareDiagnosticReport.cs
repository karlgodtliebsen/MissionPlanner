using System.Text;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Diagnostics;

/// <summary>Contains bounded, non-sensitive evidence from one firmware operation.</summary>
public sealed record FirmwareDiagnosticReport(
    Guid OperationId,
    FirmwareOperationState State,
    string? FirmwareSource = null,
    int? FirmwareBoardId = null,
    int? DetectedBoardId = null,
    int? BootloaderRevision = null,
    string? OriginalDevice = null,
    string? BootloaderDevice = null,
    string? ApplicationDevice = null,
    long? BytesProgrammed = null,
    string? VerificationResult = null,
    string? FailureCode = null,
    TimeSpan? Elapsed = null,
    FirmwareOperationState? FailureStage = null,
    string? FailureDetail = null,
    FirmwareBoardIdOverrideState? BoardIdOverride = null)
{
    /// <summary>Creates a copyable multiline diagnostic report.</summary>
    public string CreateReport()
    {
        var text = new StringBuilder()
            .AppendLine($"Operation: {OperationId}")
            .AppendLine($"State: {State}");
        Add("Firmware source", FirmwareSource);
        Add("Firmware board ID", FirmwareBoardId);
        Add("Detected board ID", DetectedBoardId);
        Add("Board ID override", BoardIdOverride);
        Add("Bootloader revision", BootloaderRevision);
        Add("Original device", OriginalDevice);
        Add("Bootloader device", BootloaderDevice);
        Add("Application device", ApplicationDevice);
        Add("Bytes programmed", BytesProgrammed);
        Add("Verification", VerificationResult);
        Add("Failure", FailureCode);
        Add("Failure stage", FailureStage);
        Add("Failure detail", FailureDetail);
        Add("Elapsed", Elapsed);
        return text.ToString().TrimEnd();

        void Add(string label, object? value)
        {
            if (value is not null) text.AppendLine($"{label}: {value}");
        }
    }
}
