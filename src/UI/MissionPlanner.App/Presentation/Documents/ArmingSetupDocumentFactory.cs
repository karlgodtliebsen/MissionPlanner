using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Setup.Arming;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Snapshot of configuration and existing diagnostic evidence; no transport access.</summary>
public sealed record ArmingDocumentContext(bool Online, ArmingSetupState? Setup, VehicleArmingDiagnostic? Diagnostic,
    bool Pending, string Movement, string Operation, string ArmAvailability);

/// <summary>Creates the Arming page's explanations without performing I/O.</summary>
public interface IArmingSetupDocumentFactory
{
    /// <summary>Explains current readiness and configuration separately from request history.</summary>
    UserDocument CreateStatus(ArmingDocumentContext context);
    /// <summary>Formats detailed evidence and copyable confirmed parameter assignments.</summary>
    UserDocument CreateDiagnostics(ArmingDocumentContext context);
}

/// <summary>Composes escaped arming documents from the existing live diagnostic and semantic configuration.</summary>
public sealed class ArmingSetupDocumentFactory : IArmingSetupDocumentFactory
{
    /// <inheritdoc />
    public UserDocument CreateStatus(ArmingDocumentContext context)
    {
        var b = new UserDocumentBuilder().Heading("Arming status");
        if (!context.Online)
        {
            return b.Paragraph("Disconnected. Connect a vehicle to see current arming evidence.").Build("Arming");
        }
        var d = context.Diagnostic;
        b.Paragraph(d?.Summary ?? "Arming readiness unknown.");
        if (d is not null)
        {
            b.Paragraph(d.Stage == ArmingDiagnosticStage.Unknown ? "Armed state needs fresh heartbeat confirmation." : d.IsArmed ? "Armed heartbeat observed." : "Heartbeat reports disarmed.");
            if (d.IsReadyToArm == true && !d.IsArmed)
            {
                b.Paragraph("All currently enabled pre-arm checks are passing. This does not establish that all checks are enabled.");
            }
            if (d.Reasons.Count > 0)
            {
                b.Heading("Current blockers", 3);
                foreach (var reason in d.Reasons)
                {
                    b.Bullet(reason);
                }
                b.Paragraph("Review the responsible subsystem: Radio, Safety, Failsafe, Compass or Battery setup. Arming does not replace those editors.");
            }
            b.Heading("Last arming activity", 3);
            if (d.LastArmAttemptAt is null)
            {
                b.Paragraph("No recent arm request was observed.")
                    .Paragraph("Check Arm/Disarm RC assignment, transmitter mapping, or stick arming.");
            }
            else
            {
                b.Paragraph($"Request source: {d.LastArmCommandSource ?? "Unknown"}; at {d.LastArmAttemptAt:O}.");
                b.Paragraph(d.LastArmResult);
            }
            if (d.Stage == ArmingDiagnosticStage.ArmRequested && d.LastArmAck == 0 && !d.IsArmed)
            {
                b.Paragraph("Arm acknowledged; waiting for armed heartbeat.");
            }
            if (d.Stage == ArmingDiagnosticStage.DisarmRequested)
            {
                b.Paragraph("Disarm requested; waiting for disarmed heartbeat.");
            }
            b.Paragraph(d.Guidance);
        }
        b.Heading("Arming configuration", 3);
        if (context.Setup is { } s)
        {
            b.Paragraph(s.Status);
            b.Bullet($"Pre-arm checks: {s.Current.Checks}");
            foreach (var setting in s.Settings.Where(s => s.Setting != ArmingSetting.Checks))
            {
                var label = setting.Choices.FirstOrDefault(c => c.Value == setting.Current)?.Label ??
                    (setting.Current is null ? setting.UnavailableReason ?? "Unknown" : "Unrecognized firmware value");
                b.Bullet($"{setting.Label}: {label}");
            }
            b.Bullet("RC Arm/Disarm switch: " + (!s.AssignmentsKnown ? "Unknown" : s.ArmSwitches.Count == 0 ? "None" : string.Join(", ", s.ArmSwitches.Select(c => $"RC{c}"))));
            if (s.ArmSwitches.Count > 1)
            {
                b.Note("Multiple Arm/Disarm assignments exist. Review their individual options in Full Parameters before reassignment.");
            }
        }
        b.Paragraph(context.ArmAvailability);
        if (context.Pending)
        {
            b.Note("Unapplied configuration changes are pending and are not active FC values.");
        }
        b.Heading("Operation", 3).Paragraph(context.Operation);
        return b.Build("Arming");
    }

    /// <inheritdoc />
    public UserDocument CreateDiagnostics(ArmingDocumentContext context)
    {
        var b = new UserDocumentBuilder().Heading("Advanced arming diagnostics");
        if (!context.Online)
        {
            return b.Paragraph("No current vehicle evidence.").Build("Arming diagnostics");
        }
        if (context.Diagnostic is { } d)
        {
            b.Paragraph($"Stage: {d.Stage}; readiness: {d.IsReadyToArm?.ToString() ?? "Unknown"}")
                .Paragraph($"Request source: {d.LastArmCommandSource ?? "Unknown"}; time: {d.LastArmAttemptAt:O}")
                .Paragraph($"ACK: {d.LastArmAck?.ToString() ?? "Not observed"}; transaction result: {d.LastArmResult ?? "Unknown"}")
                .Paragraph("ACK acceptance is not an observed heartbeat transition.");
            b.Heading("Current blockers", 3);
            foreach (var reason in d.Reasons)
            {
                b.Bullet(reason);
            }
            b.Heading("Historical evidence — not current blockers", 3)
                .Paragraph("Last arm failure: " + (d.LastArmFailure ?? "None recorded"))
                .Paragraph("Last PreArm message: " + (d.LastPreArmReason ?? "None recorded"))
                .Paragraph($"Historical PreArm time: {d.LastPreArmReasonAt:O}");
        }
        b.Heading("RC movement", 3).Paragraph(context.Movement)
            .Paragraph("Movement proves input only, not transmitter intent or successful arming.");
        b.Heading("Confirmed FC parameters", 3);
        if (context.Setup is { ParametersReady: true } s)
        {
            b.CodeBlock(s.Evidence.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => UserDocumentBuilder.ParameterAssignment(p.Key, p.Value)));
            foreach (var setting in s.Settings)
            {
                b.Paragraph($"{setting.Label}: default {setting.Default?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unknown"}; reboot required: {setting.RequiresReboot}. {setting.UnavailableReason}");
            }
        }
        else
        {
            b.CodeBlock(["// Parameters are not fully loaded; no confirmed assignment export is available."]);
        }
        return b.Build("Arming diagnostics");
    }
}
