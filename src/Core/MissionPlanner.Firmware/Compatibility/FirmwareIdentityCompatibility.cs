using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Compatibility;

/// <summary>Evaluates identity sources and intent without replacing package/protocol safety checks.</summary>
public static class FirmwareIdentityCompatibility
{
    /// <summary>Returns attributed evidence and an explicit recovery decision.</summary>
    public static FirmwareCompatibilityResult Evaluate(FirmwareIdentitySnapshot identity, FirmwareInstallMode mode,
        ApjFirmwarePackage? package = null)
    {
        var selected = identity.Selected;
        var running = identity.Running;
        var boot = identity.Bootloader;
        var evidence = new List<string>();
        if (running is not null)
        {
            evidence.Add($"Running firmware [AUTOPILOT_VERSION/STATUSTEXT]: {running.Target ?? "unknown target"} / {running.BoardId?.ToString() ?? "unknown board ID"}; {running.Version}");
        }
        if (selected is not null)
        {
            evidence.Add($"Selected firmware [{selected.Source}]: {selected.Target ?? "unknown target"} / {selected.BoardId}; {selected.VehicleType}; {selected.Version}");
        }
        if (boot is not null)
        {
            evidence.Add($"Bootloader [BootloaderProtocol]: {boot.Target ?? "target not reported"} / {boot.BoardId}; revision {boot.BootloaderRevision}");
        }
        FirmwareCompatibilityResult Result(FirmwareCompatibilityStatus status, string summary, bool proceed = false, bool offer = false) =>
            new(proceed, $"identity.{status}", summary)
            {
                Status = status, Mode = mode, Evidence = evidence.ToArray(), Summary = summary,
                RequiresExplicitConfirmation = proceed && mode == FirmwareInstallMode.Recovery,
                CanOfferRecovery = offer
            };
        if (selected is null || selected.BoardId <= 0 || string.IsNullOrWhiteSpace(selected.Target))
        {
            return Result(FirmwareCompatibilityStatus.IdentityInsufficient, "Select firmware with an exact embedded or catalogue target.");
        }
        if (boot is not null && (boot.BoardId != selected.BoardId ||
            (boot.Target is not null && !Same(boot.Target, selected.Target))))
        {
            return Result(FirmwareCompatibilityStatus.BootloaderMismatch, "Bootloader identity conflicts with the selected firmware. Recovery cannot override this.");
        }
        if (package is not null)
        {
            var embedded = package.Identity;
            if (package.BoardId != selected.BoardId || (embedded.Target is not null &&
                !Same(embedded.Target, selected.Target)))
            {
                return Result(FirmwareCompatibilityStatus.TargetMismatch, "Embedded APJ target differs from the selected firmware. File naming cannot override it.");
            }
            if (embedded.VehicleType != FirmwareVehicleType.Unknown && selected.VehicleType != FirmwareVehicleType.Unknown &&
                embedded.VehicleType != selected.VehicleType &&
                !(embedded.VehicleType == FirmwareVehicleType.Copter && !embedded.IsVehicleVariantKnown && selected.VehicleType == FirmwareVehicleType.Helicopter))
            {
                return Result(FirmwareCompatibilityStatus.VehicleTypeMismatch, "Embedded APJ vehicle variant differs from the selected variant.");
            }
            var physicalMcu = identity.Physical?.McuFamily ?? boot?.ChipDescription;
            if (physicalMcu is not null && embedded.McuFamily is not null &&
                !McuCompatible(physicalMcu, embedded.McuFamily))
            {
                return Result(FirmwareCompatibilityStatus.PhysicalDeviceMismatch, "Observed MCU differs from the APJ MCU requirement.");
            }
        }
        if (running?.IsAmbiguous == true)
        {
            return Result(FirmwareCompatibilityStatus.Ambiguous, "Conflicting running target evidence requires a fresh identity read.");
        }
        var runningMismatch = running is not null && (running.BoardId != selected.BoardId ||
            (running.Target is not null && !Same(running.Target, selected.Target)));
        if (selected.VehicleType == FirmwareVehicleType.Unknown || !selected.IsVehicleVariantKnown)
        {
            return Result(FirmwareCompatibilityStatus.IdentityInsufficient, "Embedded APJ metadata identifies the family but not its vehicle variant. Select a verified catalogue variant or an APJ with explicit variant metadata.");
        }
        if (mode == FirmwareInstallMode.Recovery)
        {
            if (boot is null)
            {
                return Result(FirmwareCompatibilityStatus.IdentityInsufficient, "Recovery requires querying a compatible bootloader before erase.");
            }
            if (selected.VehicleType == FirmwareVehicleType.Unknown)
            {
                return Result(FirmwareCompatibilityStatus.IdentityInsufficient, "Recovery requires known selected vehicle/variant metadata.");
            }
            return Result(FirmwareCompatibilityStatus.CompatibleForRecovery,
                "Bootloader matches the selected APJ. Recovery will replace the current firmware target.", true);
        }
        if (runningMismatch)
        {
            return Result(FirmwareCompatibilityStatus.RunningFirmwareMismatch,
                "Firmware target mismatch. The running firmware and selected target differ; the wrong firmware may currently be installed.", offer: true);
        }
        if (running is not null && running.VehicleType != FirmwareVehicleType.Unknown &&
            selected.VehicleType != FirmwareVehicleType.Unknown && running.VehicleType != selected.VehicleType)
        {
            return Result(FirmwareCompatibilityStatus.VehicleTypeMismatch, "Running and selected vehicle variants differ.");
        }
        if (boot is null && (running?.BoardId is null || running.Target is null))
        {
            return Result(FirmwareCompatibilityStatus.IdentityInsufficient, "Read the running target or query the bootloader; USB names are not board proof.");
        }
        return Result(FirmwareCompatibilityStatus.Compatible, "Available target identities match.", true);
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static bool McuCompatible(string observed, string expected)
    {
        var actual = observed.Split(',')[0].Trim().TrimEnd('x', 'X', '?');
        var required = expected.Split(',')[0].Trim().TrimEnd('x', 'X', '?');
        // Chip descriptions can name a family (STM32F40x), not an exact MCU (STM32F405xx).
        return actual.StartsWith(required, StringComparison.OrdinalIgnoreCase) ||
            required.StartsWith(actual, StringComparison.OrdinalIgnoreCase);
    }
}
