using Microsoft.Extensions.Options;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Applies conservative target compatibility and explicit-confirmation policy.</summary>
public sealed class DfuTargetSafetyService(IOptions<DfuOptions> options) : IDfuTargetSafetyService
{
    /// <inheritdoc />
    public DfuTargetSafetyResult Evaluate(DfuTargetSafetyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var evidence = new List<string>();
        if (string.IsNullOrWhiteSpace(request.SelectedPlatform)) return Block("PlatformNotSelected");
        if (request.Artifact is null || !IsValidArtifact(request.Artifact)) return Block("HexInvalid");
        var selectedPlatform = request.SelectedPlatform.Trim();

        if (request.Artifact.Platform is { Length: > 0 } artifactPlatform &&
            !string.Equals(artifactPlatform, selectedPlatform, StringComparison.OrdinalIgnoreCase)) return Block("ArtifactPlatformMismatch");
        if (request.SelectedBoardId is int selectedBoard && request.Artifact.BoardId is int artifactBoard && selectedBoard != artifactBoard)
            return Block("ArtifactBoardMismatch");
        if (request.ManifestEntry is { } manifest &&
            (!string.Equals(manifest.Target.Platform, selectedPlatform, StringComparison.OrdinalIgnoreCase) ||
             (request.SelectedBoardId is int selectedId && manifest.Target.BoardId != selectedId))) return Block("ManifestTargetMismatch");
        if (request.IsNormalInstall && request.Artifact.Metadata.AppearsToContainBootloader && !request.Artifact.Metadata.AppearsToContainApplication)
            return Block("BootloaderOnlyImage");

        var device = request.DeviceInformation;
        if (device?.InternalFlashBytes is long flashBytes && !FitsReportedFlash(request.Artifact.Metadata, flashBytes))
            return Block("ArtifactExceedsReportedFlash");

        var policies = options.Value.TargetPolicies.Where(policy =>
            string.Equals(policy.Platform, selectedPlatform, StringComparison.OrdinalIgnoreCase) &&
            (policy.BoardId is null || policy.BoardId == request.SelectedBoardId)).ToArray();
        foreach (var policy in policies)
        {
            if (device?.McuDeviceId is { Length: > 0 } mcu && policy.CompatibleMcuDeviceIds.Count > 0 &&
                !policy.CompatibleMcuDeviceIds.Contains(mcu, StringComparer.OrdinalIgnoreCase)) return Block("KnownIncompatibleMcu");
            if (device?.InternalFlashBytes is long size &&
                ((policy.MinimumInternalFlashBytes is long minimum && size < minimum) ||
                 (policy.MaximumInternalFlashBytes is long maximum && size > maximum))) return Block("KnownIncompatibleFlashSize");
        }

        if (device?.McuDeviceId is { Length: > 0 }) evidence.Add("McuIdentityIsNotBoardProof");
        if (request.Artifact.Metadata.AppearsToContainBootloader) evidence.Add("BootloaderRegionPresent");
        if (request.Artifact.Metadata.AppearsToContainApplication) evidence.Add("ApplicationRegionPresent");
        if (request.ManifestEntry is not null) evidence.Add("OfficialManifestTargetMatches");

        if (AssociationMatches(request, selectedPlatform))
        {
            evidence.Add("RememberedDeviceAssociationMatches");
            return new DfuTargetSafetyResult(DfuTargetSafetyDecision.Allowed, evidence.AsReadOnly());
        }

        var requiredPhrase = $"FLASH {selectedPlatform}";
        evidence.Add(string.Equals(request.ConfirmationPhrase?.Trim(), requiredPhrase, StringComparison.Ordinal)
            ? "StrongConfirmationAccepted"
            : "StrongConfirmationRequired");
        return new DfuTargetSafetyResult(DfuTargetSafetyDecision.AllowedWithStrongWarning, evidence.AsReadOnly(),
            string.Equals(request.ConfirmationPhrase?.Trim(), requiredPhrase, StringComparison.Ordinal) ? null : requiredPhrase);

        DfuTargetSafetyResult Block(string code) => new(DfuTargetSafetyDecision.Blocked, [code]);
    }

    private bool FitsReportedFlash(DfuArtifactMetadata metadata, long flashBytes)
    {
        if (flashBytes <= 0) return false;
        var flashEndExclusive = (ulong)options.Value.Stm32FlashStartAddress + (ulong)flashBytes;
        return (ulong)metadata.LowestAddress >= options.Value.Stm32FlashStartAddress && (ulong)metadata.HighestAddress < flashEndExclusive;
    }

    private bool IsValidArtifact(DfuArtifact artifact)
    {
        var metadata = artifact.Metadata;
        return metadata.Ranges.Count > 0 && metadata.DataBytes > 0 && metadata.LowestAddress <= metadata.HighestAddress &&
               metadata.Ranges.Sum(range => (long)range.Data.Length) == metadata.DataBytes && metadata.Sha256.Length == 64 &&
               metadata.Sha256.All(Uri.IsHexDigit) && metadata.Ranges.All(range =>
                   range.StartAddress >= options.Value.Stm32FlashStartAddress && range.EndAddress < options.Value.Stm32FlashEndAddressExclusive);
    }

    private static bool AssociationMatches(DfuTargetSafetyRequest request, string selectedPlatform)
    {
        var association = request.RememberedAssociation;
        return association is not null && request.DeviceInformation is not null &&
               !string.IsNullOrWhiteSpace(request.PreviousApplicationIdentity) &&
               string.Equals(association.Platform, selectedPlatform, StringComparison.OrdinalIgnoreCase) &&
               association.BoardId == request.SelectedBoardId &&
               string.Equals(association.ApplicationIdentity, request.PreviousApplicationIdentity, StringComparison.Ordinal) &&
               string.Equals(association.DfuSerialNumber, request.DeviceInformation.Device.SerialNumber, StringComparison.OrdinalIgnoreCase);
    }
}
