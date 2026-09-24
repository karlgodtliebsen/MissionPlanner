using System.Globalization;
using System.Text.RegularExpressions;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Presentation inputs for a Compass report, excluding unrelated telemetry and timestamps.</summary>
public sealed record CompassDocumentContext(
    CompassSetupState? State, CompassConfiguration? Desired, bool Online, bool Loading,
    string Validation, CompassCalibrationWorkflowState Calibration, string Instruction,
    string Progress, string? Quality, bool RequiresReboot)
{
    /// <summary>Compares only facts rendered by the document, preserving selection on redundant refreshes.</summary>
    public bool HasSameContent(CompassDocumentContext other)
    {
        return Desired == other.Desired && Online == other.Online && Loading == other.Loading &&
            Validation == other.Validation && Calibration == other.Calibration && Instruction == other.Instruction &&
            Progress == other.Progress && Quality == other.Quality && RequiresReboot == other.RequiresReboot &&
            (State is null ? other.State is null : other.State is { } state &&
                State.VehicleId == state.VehicleId && State.Current == state.Current &&
                State.Health == state.Health && State.ArmingImpact == state.ArmingImpact &&
                State.Validation == state.Validation && State.IsSupported == state.IsSupported &&
                State.UnsupportedReason == state.UnsupportedReason &&
                State.DetectedDevices.SequenceEqual(state.DetectedDevices) &&
                State.Settings.Count == state.Settings.Count && State.Settings.Zip(state.Settings).All(pair =>
                    SettingContent(pair.First) == SettingContent(pair.Second) && pair.First.Choices.SequenceEqual(pair.Second.Choices)));
    }

    private static (CompassSetting, string, string, double?, string?) SettingContent(CompassSettingDefinition setting) =>
        (setting.Setting, setting.Label, setting.ParameterName, setting.Current,
            setting.Choices.FirstOrDefault(choice => choice.Value == setting.Current)?.Label);
}

/// <summary>Produces Compass explanations from existing semantic facts, without I/O.</summary>
public interface ICompassSetupDocumentFactory
{
    /// <summary>Creates a concise report of confirmed and distinctly pending configuration.</summary>
    UserDocument Create(CompassDocumentContext context);

    /// <summary>Creates copyable parameter assignments and commented advanced diagnostics.</summary>
    UserDocument CreateDiagnostics(CompassDocumentContext context);
}

/// <summary>Application-owned Compass prose; no rendering or protocol dependencies.</summary>
public sealed class CompassSetupDocumentFactory : ICompassSetupDocumentFactory
{
    /// <inheritdoc />
    public UserDocument Create(CompassDocumentContext context)
    {
        var document = new UserDocumentBuilder().Heading("Compass status");
        if (!context.Online)
        {
            return document.Paragraph("Disconnected. Reconnect to read the current flight-controller state.")
                .Paragraph(context.Desired != context.State?.Current ? "Pending edits are local and cannot be applied while disconnected." : null)
                .Build("Compass");
        }
        if (context.Loading || context.State is null)
        {
            return document.Paragraph("Loading compass parameters… Current values are not yet available.").Build("Compass");
        }
        var state = context.State;
        if (!state.IsSupported)
        {
            return document.Paragraph("Compass setup is unsupported for this vehicle.")
                .Paragraph(state.UnsupportedReason).Build("Compass");
        }
        document.Paragraph(state.Current.Enabled switch
        {
            true => "The compass is enabled.",
            false => "The compass is disabled.",
            _ => "Compass enable state is unknown."
        });
        document.Paragraph($"Health: {state.Health}.");
        var yaw = state.Settings.FirstOrDefault(setting => setting.Setting == CompassSetting.YawSource);
        var yawLabel = yaw?.Choices.FirstOrDefault(choice => choice.Value == yaw.Current)?.Label ?? "Unknown or unrecognized";
        document.Paragraph($"Current EKF yaw source (first source set): {yawLabel}.");
        document.Paragraph(state.Validation).Paragraph(state.ArmingImpact);
        document.Heading("Detected devices", 3);
        if (state.DetectedDevices.Count == 0)
        {
            document.Paragraph("No compass device information is currently available.");
        }
        foreach (var device in state.DetectedDevices)
        {
            document.Bullet(device);
        }
        document.Heading("Calibration", 3).Paragraph(context.Calibration switch
        {
            CompassCalibrationWorkflowState.NotStarted => state.Current.Enabled == false
                ? "Enable compass and apply before calibration." : "Calibration has not been started in this session; calibration validity is not established by this page.",
            CompassCalibrationWorkflowState.Preparing => "Preparing calibration.",
            CompassCalibrationWorkflowState.Running => "Calibration is in progress.",
            CompassCalibrationWorkflowState.PendingAcceptance => "Calibration results are ready. Accept results to save them.",
            CompassCalibrationWorkflowState.Success => "Calibration completed and results were accepted.",
            CompassCalibrationWorkflowState.Failed => "Calibration failed. Review the reported reason before retrying.",
            CompassCalibrationWorkflowState.Cancelled => "Calibration was cancelled.",
            _ => "Calibration was interrupted by disconnection."
        });
        document.Paragraph(context.Instruction).Paragraph(context.Progress).Paragraph(context.Quality);
        if (context.Desired is { } desired && desired != state.Current)
        {
            document.Heading("Pending changes", 3).Note("These values are not active. Current FC state remains unchanged until Apply and verification succeed.");
            document.Table("Setting", "Current → Pending", state.Settings
                .Where(setting => state.Current.Get(setting.Setting) != desired.Get(setting.Setting))
                .Select(setting => (setting.Label,
                    $"{Choice(setting, state.Current.Get(setting.Setting))} → {Choice(setting, desired.Get(setting.Setting))}")));
            document.Paragraph(context.Validation);
        }
        if (context.RequiresReboot)
        {
            document.Note("Changes applied. Reboot required.");
        }
        document.Heading("Relevant parameters — current FC state", 3)
            .CodeBlock(state.Settings.Where(setting => setting.Current.HasValue)
                .Select(setting => UserDocumentBuilder.ParameterAssignment(setting.ParameterName, setting.Current!.Value)));
        return document.Build("Compass");
    }

    /// <inheritdoc />
    public UserDocument CreateDiagnostics(CompassDocumentContext context)
    {
        var document = new UserDocumentBuilder().Heading("Advanced parameters and diagnostics");
        if (!context.Online)
        {
            return document.Paragraph("Disconnected. Reconnect to read current diagnostics.").Build("Compass diagnostics");
        }
        if (context.Loading || context.State is null)
        {
            return document.Paragraph("Loading compass parameters… Diagnostics are not yet available.").Build("Compass diagnostics");
        }
        if (context.State.Diagnostics.Count == 0)
        {
            return document.Paragraph("No advanced diagnostics are currently available.").Build("Compass diagnostics");
        }
        return document.Paragraph("Current flight-controller values. Unavailable values and diagnostic notes are comments.")
            .CodeBlock(context.State.Diagnostics.Select(FormatDiagnostic)).Build("Compass diagnostics");
    }

    private static string FormatDiagnostic(string evidence)
    {
        var match = Regex.Match(evidence, @"^([A-Z][A-Z0-9_]*)\s*=\s*([^·\r\n]*)(?:·([^\r\n]*))?$", RegexOptions.CultureInvariant);
        if (match.Success &&
            (double.TryParse(match.Groups[2].Value.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
             double.TryParse(match.Groups[2].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) &&
            double.IsFinite(value))
        {
            return UserDocumentBuilder.ParameterAssignment(match.Groups[1].Value, value, match.Groups[3].Value.Trim());
        }
        return UserDocumentBuilder.ParameterComment(evidence);
    }

    private static string Choice(CompassSettingDefinition setting, double? value) =>
        setting.Choices.FirstOrDefault(choice => choice.Value == value)?.Label ??
        value?.ToString("G", CultureInfo.InvariantCulture) ?? "Unknown";
}
