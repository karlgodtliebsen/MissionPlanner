using System.Globalization;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Tuning;

/// <summary>Provides structured advanced controller descriptors for supported ArduPilot families.</summary>
public sealed class ExtendedTuningProfileCatalog : IExtendedTuningProfileCatalog
{
    private const string ControllerWarning =
        "Expert control-loop values can destabilize the vehicle. Preserve a known-good backup and change only values you understand.";
    private const string FilterWarning =
        "Incorrect filter or notch settings can amplify noise or hide real motion. Verify logs and sensor sample rates before applying.";
    private static readonly IReadOnlyDictionary<FirmwareFamily, ExtendedTuningProfile> profiles =
        new Dictionary<FirmwareFamily, ExtendedTuningProfile>
        {
            [FirmwareFamily.ArduCopter] = Copter(),
            [FirmwareFamily.ArduPlane] = Plane(),
            [FirmwareFamily.Rover] = Rover(),
            [FirmwareFamily.ArduSub] = Sub()
        };

    /// <inheritdoc />
    public ExtendedTuningProfile? GetProfile(FirmwareFamily family) => profiles.GetValueOrDefault(family);

    /// <inheritdoc />
    public IReadOnlyList<AdvancedTuningFieldDefinition> Expand(AdvancedTuningDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.Axes.Count == 0 || descriptor.Instances.Count == 0 || descriptor.Components.Count == 0)
        {
            return [];
        }

        var result = new List<AdvancedTuningFieldDefinition>(
            descriptor.Axes.Count * descriptor.Instances.Count * descriptor.Components.Count);
        foreach (var instance in descriptor.Instances)
        {
            foreach (var axis in descriptor.Axes)
            {
                var prefix = descriptor.ParameterPrefixPattern
                    .Replace("{axis}", axis, StringComparison.Ordinal)
                    .Replace("{instance}", instance == 0 ? string.Empty : instance.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                foreach (var component in descriptor.Components)
                {
                    var parameterName = string.IsNullOrWhiteSpace(component.Key) ? prefix : $"{prefix}_{component.Key}";
                    result.Add(new AdvancedTuningFieldDefinition(
                        descriptor.Key,
                        descriptor.Category,
                        axis,
                        instance,
                        component,
                        ParameterFieldDefinition.Exact(parameterName)));
                }
            }
        }

        return result;
    }

    private static ExtendedTuningProfile Copter() => new(
        FirmwareFamily.ArduCopter,
        [
            RateDescriptor(["RLL", "PIT", "YAW"]),
            Descriptor("attitude", "Controllers", "Attitude angle controllers", "Angle-error proportional controllers.",
                "ATC_ANG_{axis}", ["RLL", "PIT", "YAW"], [0],
                [C("P", "Proportional", "Angle-error proportional gain.", "gain")]),
            Descriptor("position-xy", "Position", "Horizontal position and velocity", "Horizontal position/velocity controller gains and filters.",
                "PSC_VELXY", [""], [0],
                [C("P", "Velocity P", "Horizontal velocity proportional gain.", "gain"),
                 C("I", "Velocity I", "Horizontal velocity integral gain.", "gain"),
                 C("D", "Velocity D", "Horizontal velocity derivative gain.", "gain"),
                 C("IMAX", "Velocity I limit", "Maximum horizontal integral contribution.", "cm/s²"),
                 C("FILT", "Velocity filter", "Horizontal velocity controller filter cutoff.", "Hz")],
                [PositiveCompanion("I", "IMAX", "A positive velocity I gain requires a positive integrator limit.")]),
            Descriptor("accel-z", "Position", "Vertical acceleration controller", "Vertical acceleration PID and feed-forward controller.",
                "PSC_ACCZ", [""], [0], PidComponents(includeFeedForward: true),
                [PositiveCompanion("I", "IMAX", "A positive vertical I gain requires a positive integrator limit.")]),
            HarmonicNotch("harmonic-notch", "INS_HNTCH"),
            HarmonicNotch("harmonic-notch-2", "INS_HNTC2"),
            GyroFilters(),
            Descriptor("autotune", "Autotune setup", "Autotune configuration", "Parameters that configure a later on-vehicle autotune; this page never starts autotune.",
                "AUTOTUNE", [""], [0],
                [C("AXES", "Axes mask", "Controller axes eligible for autotune.", "bitmask"),
                 C("AGGR", "Aggressiveness", "Target autotune aggressiveness.", "ratio"),
                 C("MIN_D", "Minimum D", "Minimum derivative gain considered by autotune.", "gain")]),
            EstimatorDescriptor()
        ]);

    private static ExtendedTuningProfile Plane() => new(
        FirmwareFamily.ArduPlane,
        [
            RateDescriptor(["RLL", "PIT", "YAW"]),
            Descriptor("tecs", "Navigation", "TECS energy controller", "Advanced airspeed, pitch, throttle, and energy-controller response.",
                "TECS", [""], [0],
                [C("TIME_CONST", "Time constant", "TECS control response time constant.", "s"),
                 C("SPDWEIGHT", "Speed weighting", "Balance between airspeed and height control.", "ratio"),
                 C("PTCH_DAMP", "Pitch damping", "Pitch-demand damping.", "gain"),
                 C("INTEG_GAIN", "Integrator gain", "Energy-controller integrator gain.", "gain"),
                 C("THR_DAMP", "Throttle damping", "Throttle-demand damping.", "gain")]),
            Descriptor("l1", "Navigation", "L1 path controller", "Advanced lateral path-tracking controller values.",
                "NAVL1", [""], [0],
                [C("PERIOD", "Period", "L1 controller period.", "s"),
                 C("DAMPING", "Damping", "L1 controller damping ratio.", "ratio"),
                 C("XTRACK_I", "Cross-track I", "Cross-track integrator gain.", "gain")]),
            HarmonicNotch("harmonic-notch", "INS_HNTCH"),
            GyroFilters(),
            Descriptor("autotune", "Autotune setup", "Autotune configuration", "Parameters that configure a later flight autotune; this page never starts it.",
                "AUTOTUNE", [""], [0],
                [C("LEVEL", "Autotune level", "Requested autotune aggressiveness level.", "level"),
                 C("OPTIONS", "Autotune options", "Autotune option bitmask.", "bitmask")]),
            EstimatorDescriptor()
        ]);

    private static ExtendedTuningProfile Rover() => new(
        FirmwareFamily.Rover,
        [
            Descriptor("speed-controller", "Controllers", "Speed controller", "Forward-speed PID and feed-forward controller.",
                "ATC_SPEED", [""], [0], PidComponents(includeFeedForward: true),
                [PositiveCompanion("I", "IMAX", "A positive speed I gain requires a positive integrator limit.")]),
            Descriptor("steering-controller", "Controllers", "Steering-rate controller", "Steering-rate PID and feed-forward controller.",
                "ATC_STR_RAT", [""], [0], PidComponents(includeFeedForward: true),
                [PositiveCompanion("I", "IMAX", "A positive steering I gain requires a positive integrator limit.")]),
            Descriptor("l1", "Navigation", "L1 path controller", "Advanced lateral path-tracking controller values.",
                "NAVL1", [""], [0],
                [C("PERIOD", "Period", "L1 controller period.", "s"),
                 C("DAMPING", "Damping", "L1 controller damping ratio.", "ratio"),
                 C("XTRACK_I", "Cross-track I", "Cross-track integrator gain.", "gain")]),
            HarmonicNotch("harmonic-notch", "INS_HNTCH"),
            GyroFilters(),
            EstimatorDescriptor()
        ]);

    private static ExtendedTuningProfile Sub() => new(
        FirmwareFamily.ArduSub,
        [
            RateDescriptor(["RLL", "PIT", "YAW"]),
            Descriptor("depth-position", "Depth", "Depth position controller", "Depth position and vertical velocity response.",
                "PSC_POSZ", [""], [0],
                [C("P", "Depth position P", "Depth-position proportional gain.", "gain")]),
            Descriptor("depth-velocity", "Depth", "Depth velocity controller", "Vertical velocity PID and integrator limit.",
                "PSC_VELZ", [""], [0],
                [C("P", "Velocity P", "Vertical velocity proportional gain.", "gain"),
                 C("I", "Velocity I", "Vertical velocity integral gain.", "gain"),
                 C("D", "Velocity D", "Vertical velocity derivative gain.", "gain"),
                 C("IMAX", "Velocity I limit", "Maximum vertical integral contribution.", "cm/s²")],
                [PositiveCompanion("I", "IMAX", "A positive depth-velocity I gain requires a positive integrator limit.")]),
            Descriptor("depth-accel", "Depth", "Depth acceleration controller", "Vertical acceleration PID and feed-forward controller.",
                "PSC_ACCZ", [""], [0], PidComponents(includeFeedForward: true),
                [PositiveCompanion("I", "IMAX", "A positive depth-acceleration I gain requires a positive integrator limit.")]),
            HarmonicNotch("harmonic-notch", "INS_HNTCH"),
            GyroFilters(),
            Descriptor("autotune", "Autotune setup", "Autotune configuration", "Parameters that configure a later on-vehicle autotune; this page never starts it.",
                "AUTOTUNE", [""], [0],
                [C("AXES", "Axes mask", "Controller axes eligible for autotune.", "bitmask"),
                 C("AGGR", "Aggressiveness", "Target autotune aggressiveness.", "ratio")]),
            EstimatorDescriptor()
        ]);

    private static AdvancedTuningDescriptor RateDescriptor(IReadOnlyList<string> axes) =>
        Descriptor("rate-pid", "Controllers", "Body-rate controllers", "Repeated rate PID, feed-forward, limits, and filters by axis.",
            "ATC_RAT_{axis}", axes, [0], PidComponents(includeFeedForward: true),
            [PositiveCompanion("I", "IMAX", "A positive rate I gain requires a positive integrator limit.")]);

    private static AdvancedTuningDescriptor HarmonicNotch(string key, string prefix) =>
        Descriptor(key, "Filters", prefix == "INS_HNTCH" ? "Primary harmonic notch" : "Secondary harmonic notch",
            "Harmonic sensor-noise notch center, bandwidth, attenuation, and tracking mode.",
            prefix, [""], [0],
            [C("FREQ", "Center frequency", "Notch center frequency.", "Hz"),
             C("BW", "Bandwidth", "Notch bandwidth.", "Hz"),
             C("ATT", "Attenuation", "Notch attenuation.", "dB"),
             C("MODE", "Tracking mode", "Source used to track the notch frequency.", "enum"),
             C("HMNCS", "Harmonics", "Enabled harmonic mask.", "bitmask")],
            [PositiveCompanion("FREQ", "BW", "A positive notch frequency requires positive bandwidth."),
             LessOrEqual("BW", "FREQ", "Notch bandwidth must not exceed its center frequency.")],
            FilterWarning);

    private static AdvancedTuningDescriptor GyroFilters() =>
        Descriptor("gyro-filters", "Filters", "Gyro instance filters", "Repeated low-pass filter cutoffs for available gyro instances.",
            "INS_GYRO{instance}", [""], [0, 2, 3],
            [C("FILTER", "Gyro low-pass", "Gyroscope low-pass filter cutoff.", "Hz")],
            warning: FilterWarning);

    private static AdvancedTuningDescriptor EstimatorDescriptor() =>
        Descriptor("ekf3-noise", "Estimator", "EKF3 observation noise", "Selected EKF3 observation-noise values that influence estimator weighting.",
            "EK3", [""], [0],
            [C("POSNE_M_NSE", "Horizontal position noise", "GPS horizontal position observation noise.", "m"),
             C("VELNE_M_NSE", "Horizontal velocity noise", "GPS horizontal velocity observation noise.", "m/s"),
             C("ALT_M_NSE", "Altitude noise", "Altitude observation noise.", "m"),
             C("MAG_M_NSE", "Magnetic noise", "Magnetic-field observation noise.", "gauss")],
            warning: "Estimator noise values affect sensor weighting and can cause position or attitude divergence when set incorrectly.");

    private static IReadOnlyList<AdvancedTuningComponent> PidComponents(bool includeFeedForward)
    {
        var components = new List<AdvancedTuningComponent>
        {
            C("P", "Proportional", "Proportional controller gain.", "gain"),
            C("I", "Integral", "Integral controller gain.", "gain"),
            C("D", "Derivative", "Derivative controller gain.", "gain")
        };
        if (includeFeedForward)
        {
            components.Add(C("FF", "Feed-forward", "Feed-forward controller gain.", "gain"));
        }

        components.AddRange(
        [
            C("IMAX", "Integrator limit", "Maximum integral contribution.", "output"),
            C("FLTT", "Target filter", "Target low-pass filter cutoff.", "Hz"),
            C("FLTE", "Error filter", "Error low-pass filter cutoff.", "Hz"),
            C("FLTD", "Derivative filter", "Derivative low-pass filter cutoff.", "Hz")
        ]);
        return components;
    }

    private static AdvancedTuningDescriptor Descriptor(
        string key,
        string category,
        string title,
        string description,
        string prefix,
        IReadOnlyList<string> axes,
        IReadOnlyList<int> instances,
        IReadOnlyList<AdvancedTuningComponent> components,
        IReadOnlyList<BasicTuningRule>? rules = null,
        string? warning = null) =>
        new(key, category, title, description, prefix, axes, instances, components, rules ?? [], warning ?? ControllerWarning);

    private static AdvancedTuningComponent C(string key, string title, string description, string units) =>
        new(key, title, description, units);

    private static BasicTuningRule LessOrEqual(string first, string second, string message) =>
        new(BasicTuningRuleKind.LessThanOrEqual, first, second, message);

    private static BasicTuningRule PositiveCompanion(string first, string second, string message) =>
        new(BasicTuningRuleKind.PositiveCompanion, first, second, message);
}
