using System.Globalization;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Builds read-only motor evidence without inferring physical motion from acknowledgements.</summary>
public static class MotorOutputDiagnostics
{
    /// <summary>Formats downloaded configuration and frame-derived output assignments.</summary>
    public static string Describe(
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        MotorLayout? layout,
        Func<int, MotorOutputResolution> resolve)
    {
        string Value(string name) => parameters.TryGetValue(name, out var value)
            ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "unavailable";
        var lines = new List<string>();
        foreach (var name in new[] { "FRAME_CLASS", "FRAME_TYPE", "MOT_PWM_TYPE", "MOT_SPIN_ARM",
                     "MOT_SPIN_MIN", "MOT_SAFE_DISARM", "BRD_SAFETY_DEFLT" })
        {
            lines.Add($"{name} = {Value(name)}");
        }
        foreach (var pair in parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if ((pair.Key.StartsWith("SERVO", StringComparison.Ordinal) && pair.Key.EndsWith("_FUNCTION", StringComparison.Ordinal)) ||
                pair.Key is "Q_FRAME_CLASS" or "Q_FRAME_TYPE")
            {
                lines.Add($"{pair.Key} = {Value(pair.Key)}");
            }
        }
        var interlocks = parameters.Where(pair => pair.Key.StartsWith("RC", StringComparison.Ordinal) &&
            pair.Key.EndsWith("_OPTION", StringComparison.Ordinal) && pair.Value.Value == 32).Select(pair => pair.Key).ToArray();
        lines.Add(interlocks.Length == 0
            ? "Motor interlock: none in downloaded RC options (missing options remain unknown)."
            : $"Motor interlock: {string.Join(", ", interlocks)} = 32; switch state unavailable.");
        var protocol = parameters.TryGetValue("MOT_PWM_TYPE", out var pwm) ? Protocol(pwm.Value) : "unavailable";
        lines.Add($"Selected protocol: {protocol}; effective hardware support/reboot state not verified.");
        if (layout is null)
        {
            lines.Add("Motor positions unavailable for this frame.");
        }
        else
        {
            foreach (var motor in layout.Motors.OrderBy(motor => motor.TestOrder))
            {
                var output = resolve(motor.MotorNumber);
                var channels = output.OutputChannels.Count == 0 ? "none" : string.Join(", ", output.OutputChannels);
                lines.Add($"{motor.Label} | {Position(motor)} | output {channels} ({output.Status}) | {protocol}");
            }
        }
        lines.Add("Timer/output groups: unavailable; consult the board output-group documentation.");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Formats evidence and conditional checks without diagnosing a parameter in isolation.</summary>
    public static string Guidance(int observedRotatingMotors, int motorCount, string? commandFailure)
    {
        var evidence = observedRotatingMotors > 0
            ? $"User confirmed rotation on {observedRotatingMotors}/{motorCount} motors: those FC/ESC/motor paths operated during the test."
            : "Physical rotation has not been confirmed. Command acceptance alone does not prove movement.";
        if (!string.IsNullOrWhiteSpace(commandFailure))
        {
            evidence += $" Latest command failure: {commandFailure}. This does not establish failure of every motor.";
        }
        return evidence + Environment.NewLine +
            "If the vehicle arms but motors remain stopped, inspect MOT_SPIN_ARM, spool state, interlock and safety state." + Environment.NewLine +
            "If Motor Test fails on all motors, inspect protocol, output mapping and ESC power before changing parameters.";
    }

    private static string Position(MotorLayoutMotor motor)
    {
        var longitudinal = motor.Pitch > 0.01 ? "front" : motor.Pitch < -0.01 ? "rear" : "center";
        var lateral = motor.Roll < -0.01 ? "right" : motor.Roll > 0.01 ? "left" : "center";
        return $"{longitudinal}/{lateral} (frame roll {motor.Roll:0.###}, pitch {motor.Pitch:0.###})";
    }

    /// <summary>Names the downloaded MOT_PWM_TYPE value without assuming effective hardware support.</summary>
    public static string Protocol(float value) => value switch
    {
        0 => "Normal PWM",
        1 => "OneShot",
        2 => "OneShot125",
        3 => "Brushed",
        4 => "DShot150",
        5 => "DShot300",
        6 => "DShot600",
        7 => "DShot1200",
        8 => "PWMRange",
        9 => "PWMAngle",
        _ => $"Unknown ({value})"
    };
}
