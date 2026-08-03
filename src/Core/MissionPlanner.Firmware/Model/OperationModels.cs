namespace MissionPlanner.Firmware.Model;

/// <summary>Represents a compatibility decision with a stable reason code.</summary>
public sealed record FirmwareCompatibilityResult(bool IsCompatible, string Code, string? TechnicalDetail = null);

/// <summary>Reports deterministic firmware-operation progress.</summary>
public sealed record FirmwareProgress
{
    /// <summary>Initializes a progress report.</summary>
    public FirmwareProgress(
        FirmwareOperationState state,
        double? percentage,
        string messageCode,
        long? completedBytes = null,
        long? totalBytes = null,
        string? technicalDetail = null)
    {
        if (percentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
        if (completedBytes < 0) throw new ArgumentOutOfRangeException(nameof(completedBytes));
        if (totalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(totalBytes));
        if (completedBytes.HasValue && totalBytes.HasValue && completedBytes > totalBytes)
            throw new ArgumentException("Completed bytes cannot exceed total bytes.", nameof(completedBytes));
        State = state;
        Percentage = percentage;
        MessageCode = string.IsNullOrWhiteSpace(messageCode)
            ? throw new ArgumentException("A stable message code is required.", nameof(messageCode))
            : messageCode;
        CompletedBytes = completedBytes;
        TotalBytes = totalBytes;
        TechnicalDetail = technicalDetail;
    }

    /// <summary>Gets the lifecycle state.</summary>
    public FirmwareOperationState State { get; }

    /// <summary>Gets completion percentage from zero through one hundred.</summary>
    public double? Percentage { get; }

    /// <summary>Gets the stable localizable message code.</summary>
    public string MessageCode { get; }

    /// <summary>Gets completed bytes when meaningful.</summary>
    public long? CompletedBytes { get; }

    /// <summary>Gets total bytes when meaningful.</summary>
    public long? TotalBytes { get; }

    /// <summary>Gets optional non-localized diagnostic detail.</summary>
    public string? TechnicalDetail { get; }
}

/// <summary>Describes a firmware operation failure without UI text.</summary>
public sealed record FirmwareOperationFailure(string Code, string? TechnicalDetail = null, string? ExceptionType = null);

/// <summary>Represents the terminal result of a firmware operation.</summary>
public sealed record FirmwareOperationResult(
    Guid OperationId,
    FirmwareOperationKind Kind,
    FirmwareOperationState State,
    FirmwareOperationFailure? Failure = null);
