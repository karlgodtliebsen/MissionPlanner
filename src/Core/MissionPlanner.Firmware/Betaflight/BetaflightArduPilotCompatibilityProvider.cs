namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Applies only explicit reviewed registrations; unknown boards remain blocked.</summary>
public sealed class BetaflightArduPilotCompatibilityProvider(IEnumerable<BetaflightArduPilotMapping> mappings)
    : IBetaflightArduPilotCompatibilityProvider
{
    /// <inheritdoc />
    public BetaflightArduPilotMapping? Resolve(BetaflightDeviceInfo identity)
    {
        if (identity.FirmwareVariant != "BTFL" || identity.Board is not { } board)
        {
            return null;
        }
        var matches = mappings.Where(mapping => !string.IsNullOrWhiteSpace(mapping.ReviewEvidence)
                                                && !string.IsNullOrWhiteSpace(mapping.BoardName) && !string.IsNullOrWhiteSpace(mapping.ManufacturerId)
                                                && !string.IsNullOrWhiteSpace(mapping.TargetName) && !string.IsNullOrWhiteSpace(mapping.ArduPilotPlatform)
                                                && mapping.ArduPilotBoardId > 0 && mapping.ReviewedOn != default
                                                && mapping.BoardIdentifier == board.Identifier && mapping.TargetName == board.TargetName
                                                && mapping.BoardName == board.BoardName && mapping.ManufacturerId == board.ManufacturerId
                                                && mapping.HardwareRevision == board.HardwareRevision).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}