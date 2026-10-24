using System.Globalization;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Provides structured advanced controller descriptors for supported ArduPilot families.</summary>
public sealed class ExtendedTuningProfileCatalog : IExtendedTuningProfileCatalog
{
    private const string ControllerWarning =
        "Expert control-loop values can destabilize the vehicle. Preserve a known-good backup and change only values you understand.";

    private const string FilterWarning =
        "Incorrect filter or notch settings can amplify noise or hide real motion. Verify logs and sensor sample rates before applying.";

    private static readonly IReadOnlyDictionary<FirmwareFamily, ExtendedTuningProfile> profiles =
        new Dictionary<FirmwareFamily, ExtendedTuningProfile> { [FirmwareFamily.ArduCopter] = Copter(), [FirmwareFamily.ArduPlane] = Plane(), [FirmwareFamily.Rover] = Rover(), [FirmwareFamily.ArduSub] = Sub() };

    /// <inheritdoc />
    public ExtendedTuningProfile? GetProfile(FirmwareFamily family)
    {
        return profiles.GetValueOrDefault(family);
    }

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
                        new ParameterFieldDefinition(parameterName, TuningParameterCompatibility.Names(parameterName))));
                }
            }
        }

        return result;
    }

    private static ExtendedTuningProfile Copter()
    {
        return new ExtendedTuningProfile(
            FirmwareFamily.ArduCopter,
            [
                RateDescriptor(["RLL", "PIT", "YAW"]),
                Descriptor("attitude", "Controllers", "Attitude angle controllers", "Angle-error proportional controllers.",
                    "ATC_ANG_{axis}", ["RLL", "PIT", "YAW"], [0],
                    [C("P", "Proportional", "Angle-error proportional gain.", "gain")]),
                Descriptor("position-xy", "Position", "Horizontal position and velocity", "Horizontal position/velocity controller gains and filters.",
                    "PSC_VELXY", [""], [0],
                    [
                        C("P", "Velocity P", "Horizontal velocity proportional gain.", "gain"),
                        C("I", "Velocity I", "Horizontal velocity integral gain.", "gain"),
                        C("D", "Velocity D", "Horizontal velocity derivative gain.", "gain"),
                        C("IMAX", "Velocity I limit", "Maximum horizontal integral contribution.", "cm/s²"),
                        C("FILT", "Velocity filter", "Horizontal velocity controller filter cutoff.", "Hz")
                    ],
                    [PositiveCompanion("I", "IMAX", "A positive velocity I gain requires a positive integrator limit.")]),
                Descriptor("accel-z", "Position", "Vertical acceleration controller", "Vertical acceleration PID and feed-forward controller.",
                    "PSC_ACCZ", [""], [0], PidComponents(true),
                    [PositiveCompanion("I", "IMAX", "A positive vertical I gain requires a positive integrator limit.")]),
                HarmonicNotch("harmonic-notch", "INS_HNTCH"),
                HarmonicNotch("harmonic-notch-2", "INS_HNTC2"),
                GyroFilters(),
                Descriptor("autotune", "Autotune setup", "Autotune configuration", "Parameters that configure a later on-vehicle autotune; this page never starts autotune.",
                    "AUTOTUNE", [""], [0],
                    [
                        C("AXES", "Axes mask", "Controller axes eligible for autotune.", "bitmask"),
                        C("AGGR", "Aggressiveness", "Target autotune aggressiveness.", "ratio"),
                        C("MIN_D", "Minimum D", "Minimum derivative gain considered by autotune.", "gain")
                    ]),
                Descriptor("horizontal-position", "Position", "Horizontal position gain", "Position-error proportional response.",
                    "PSC_POSXY", [""], [0], [C("P", "Proportional", "Horizontal position gain.", "gain")]),
                Descriptor("vertical-position", "Position", "Vertical position gain", "Height-error proportional response.",
                    "PSC_POSZ", [""], [0], [C("P", "Proportional", "Vertical position gain.", "gain")]),
                Descriptor("vertical-velocity", "Position", "Vertical velocity controller", "Vertical velocity gains and filters.",
                    "PSC_VELZ", [""], [0], PidComponents(true)),
                Descriptor("rc-tuning", "Pilot tuning", "RC tuning assignment", "Explicit in-flight tuning function and limits; this editor never starts tuning.",
                    "TUNE", [""], [0], [C("", "Function", "Firmware tuning function.", "enum"), C("MIN", "Minimum", "Lower tuning limit.", ""), C("MAX", "Maximum", "Upper tuning limit.", "")]),
                Descriptor("auxiliary", "Pilot tuning", "Auxiliary channel functions", "Firmware-supported RC switch functions.",
                    "RC{instance}", [""], Enumerable.Range(5, 12).ToArray(), [C("OPTION", "Function", "Auxiliary channel function.", "enum")]),
                Descriptor("motor-settings", "Motors", "Motor configuration", "Reported protocol, output range and spin settings. Review changes with propellers removed.",
                    "MOT", [""], [0], [C("PWM_TYPE", "Protocol", "Output protocol.", "enum"), C("PWM_MIN", "Minimum PWM", "Minimum output.", "us"), C("PWM_MAX", "Maximum PWM", "Maximum output.", "us"), C("SPIN_ARM", "Armed spin", "Armed spin fraction.", "ratio"), C("SPIN_MIN", "Minimum spin", "Minimum active spin fraction.", "ratio"), C("SPIN_MAX", "Maximum spin", "Maximum spin fraction.", "ratio")]),
                EstimatorDescriptor()
            ]);
    }

    private static ExtendedTuningProfile Plane()
    {
        return new ExtendedTuningProfile(
            FirmwareFamily.ArduPlane,
            [
                Descriptor("plane-rate", "Controllers", "Fixed-wing rate controllers", "Native fixed-wing roll and pitch rate controllers.",
                    "{axis}_RATE", ["RLL", "PTCH", "YAW"], [0], PidComponents(true)),
                Descriptor("plane-legacy", "Controllers", "Legacy fixed-wing controllers", "Older controller gains in their reported units; no automatic gain conversion.",
                    "{axis}2SRV", ["RLL", "PTCH"], [0], PidComponents(true)),
                Descriptor("quadplane-rate", "QuadPlane", "VTOL rate controllers", "Shown only for reported QuadPlane controller parameters.",
                    "Q_A_RAT_{axis}", ["RLL", "PIT", "YAW"], [0], PidComponents(true)),
                Descriptor("quadplane-attitude", "QuadPlane", "VTOL attitude controllers", "QuadPlane attitude-angle proportional gains.",
                    "Q_A_ANG_{axis}", ["RLL", "PIT", "YAW"], [0], [C("P", "Proportional", "Attitude proportional gain.", "gain")]),
                Descriptor("quadplane-position", "QuadPlane", "VTOL horizontal velocity", "QuadPlane position-control velocity gains.",
                    "Q_P_VELXY", [""], [0], PidComponents(true)),
                Descriptor("quadplane-vertical", "QuadPlane", "VTOL vertical acceleration", "QuadPlane vertical acceleration gains.",
                    "Q_P_ACCZ", [""], [0], PidComponents(true)),
                Descriptor("quadplane-horizontal-position", "QuadPlane", "VTOL horizontal position", "Reported QuadPlane position gain.",
                    "Q_P_POSXY", [""], [0], [C("P", "Proportional", "Horizontal position gain.", "gain")]),
                Descriptor("quadplane-height", "QuadPlane", "VTOL height position", "Reported QuadPlane height gain.",
                    "Q_P_POSZ", [""], [0], [C("P", "Proportional", "Height position gain.", "gain")]),
                Descriptor("quadplane-climb", "QuadPlane", "VTOL vertical velocity", "Reported vertical velocity controller.",
                    "Q_P_VELZ", [""], [0], PidComponents(true)),
                Descriptor("quadplane-navigation", "QuadPlane", "VTOL waypoint navigation", "Current and historical names retain their native values and units.",
                    "Q_WP", [""], [0], [C("SPEED", "Horizontal speed", "Waypoint horizontal speed.", "cm/s"), C("SPEED_UP", "Climb speed", "Waypoint ascent speed.", "cm/s"), C("SPEED_DN", "Descent speed", "Waypoint descent speed.", "cm/s"), C("RADIUS", "Waypoint radius", "Waypoint arrival radius.", "cm"), C("ACCEL", "Horizontal acceleration", "Waypoint acceleration.", "cm/s²"), C("ACCEL_Z", "Vertical acceleration", "Waypoint climb acceleration.", "cm/s²"), C("JERK", "Jerk", "Waypoint jerk.", "m/s³")]),
                Descriptor("quadplane-loiter", "QuadPlane", "VTOL loiter", "Reported loiter speed and acceleration.",
                    "Q_LOIT", [""], [0], [C("SPEED", "Speed", "Loiter maximum speed.", "cm/s"), C("ACC_MAX", "Acceleration", "Loiter acceleration limit.", "cm/s²")]),
                Descriptor("plane-yaw", "Controllers", "Yaw coordination", "Reported fixed-wing yaw damping and coordination.",
                    "YAW2SRV", [""], [0], [C("DAMP", "Damping", "Yaw damping gain.", "gain"), C("INT", "Integrator", "Yaw integrator gain.", "gain"), C("RLL", "Roll coupling", "Roll coordination gain.", "gain"), C("IMAX", "Integrator limit", "Reported native integrator limit.", "native output units")]),
                Descriptor("plane-legacy-energy", "Legacy controllers", "Legacy energy/height/airspeed controllers", "Only shown when reported by older firmware. These are independent of TECS gains.",
                    "{axis}", ["ENRGY2THR", "ALT2PTCH", "ARSP2PTCH"], [0], PidComponents(false)),
                Descriptor("plane-mixing", "Controllers", "Fixed-wing feed-forward mixing", "Independent pitch/throttle and rudder mixing controls.",
                    "KFF", [""], [0], [C("THR2PTCH", "Throttle to pitch", "Throttle feed-forward into pitch.", "gain"), C("PTCH2THR", "Pitch to throttle", "Pitch feed-forward into throttle.", "gain"), C("RDDRMIX", "Rudder mixing", "Rudder mixing ratio.", "ratio")]),
                Descriptor("tecs", "Navigation", "TECS energy controller", "Advanced airspeed, pitch, throttle, and energy-controller response.",
                    "TECS", [""], [0],
                    [
                        C("TIME_CONST", "Time constant", "TECS control response time constant.", "s"),
                        C("SPDWEIGHT", "Speed weighting", "Balance between airspeed and height control.", "ratio"),
                        C("PTCH_DAMP", "Pitch damping", "Pitch-demand damping.", "gain"),
                        C("INTEG_GAIN", "Integrator gain", "Energy-controller integrator gain.", "gain"),
                        C("THR_DAMP", "Throttle damping", "Throttle-demand damping.", "gain")
                    ]),
                Descriptor("l1", "Navigation", "L1 path controller", "Advanced lateral path-tracking controller values.",
                    "NAVL1", [""], [0],
                    [
                        C("PERIOD", "Period", "L1 controller period.", "s"),
                        C("DAMPING", "Damping", "L1 controller damping ratio.", "ratio"),
                        C("XTRACK_I", "Cross-track I", "Cross-track integrator gain.", "gain")
                    ]),
                HarmonicNotch("harmonic-notch", "INS_HNTCH"),
                GyroFilters(),
                Descriptor("autotune", "Autotune setup", "Autotune configuration", "Parameters that configure a later flight autotune; this page never starts it.",
                    "AUTOTUNE", [""], [0],
                    [
                        C("LEVEL", "Autotune level", "Requested autotune aggressiveness level.", "level"),
                        C("OPTIONS", "Autotune options", "Autotune option bitmask.", "bitmask")
                    ]),
                EstimatorDescriptor()
            ]);
    }

    private static ExtendedTuningProfile Rover()
    {
        return new ExtendedTuningProfile(
            FirmwareFamily.Rover,
            [
                Descriptor("legacy-rover-speed", "Legacy controllers", "Legacy speed controller", "Historical native gains; no automatic conversion to the ATC controller.",
                    "SPEED2THR", [""], [0], PidComponents(false)),
                Descriptor("legacy-rover-steering", "Legacy controllers", "Legacy steering controller", "Historical native steering gains and limits.",
                    "STEER2SRV", [""], [0], PidComponents(false)),
                Descriptor("rover-output", "Outputs", "Motor and throttle outputs", "Firmware-supported output protocol, limits and slew rate.",
                    "MOT", [""], [0], [C("PWM_TYPE", "Protocol", "Output protocol.", "enum"), C("THR_MIN", "Minimum throttle", "Minimum throttle.", "%"), C("THR_MAX", "Maximum throttle", "Maximum throttle.", "%"), C("SLEWRATE", "Slew rate", "Throttle change limit.", "%/s")]),
                Descriptor("rover-legacy-output", "Outputs", "Legacy throttle limits", "Reported legacy throttle limits.",
                    "THR", [""], [0], [C("MIN", "Minimum throttle", "Minimum throttle.", "%"), C("MAX", "Maximum throttle", "Maximum throttle.", "%")]),
                Descriptor("rover-obstacles", "Navigation", "Rangefinder obstacle response", "Reported response threshold and turning behavior.",
                    "RNGFND", [""], [0], [C("TRIGGR_CM", "Trigger distance", "Obstacle response distance.", "cm"), C("TURN_ANGL", "Turn angle", "Obstacle turn angle.", "deg"), C("TURN_TIME", "Turn duration", "Obstacle turn duration.", "s"), C("DEBOUNCE", "Debounce", "Consecutive readings required.", "samples")]),
                Descriptor("rover-legacy-obstacles", "Navigation", "Legacy sonar response", "Independent legacy names shown only when reported.",
                    "SONAR", [""], [0], [C("TRIGGER_CM", "Trigger distance", "Obstacle response distance.", "cm"), C("TURN_ANGLE", "Turn angle", "Obstacle turn angle.", "deg"), C("TURN_TIME", "Turn duration", "Obstacle turn duration.", "s"), C("DEBOUNCE", "Debounce", "Consecutive readings required.", "samples")]),
                Descriptor("rover-auxiliary", "Pilot tuning", "Auxiliary channel functions", "Reported RC switch assignments.",
                    "RC{instance}", [""], Enumerable.Range(5, 12).ToArray(), [C("OPTION", "Function", "Auxiliary channel function.", "enum")]),
                Descriptor("speed-controller", "Controllers", "Speed controller", "Forward-speed PID and feed-forward controller.",
                    "ATC_SPEED", [""], [0], PidComponents(true),
                    [PositiveCompanion("I", "IMAX", "A positive speed I gain requires a positive integrator limit.")]),
                Descriptor("steering-controller", "Controllers", "Steering-rate controller", "Steering-rate PID and feed-forward controller.",
                    "ATC_STR_RAT", [""], [0], PidComponents(true),
                    [PositiveCompanion("I", "IMAX", "A positive steering I gain requires a positive integrator limit.")]),
                Descriptor("l1", "Navigation", "L1 path controller", "Advanced lateral path-tracking controller values.",
                    "NAVL1", [""], [0],
                    [
                        C("PERIOD", "Period", "L1 controller period.", "s"),
                        C("DAMPING", "Damping", "L1 controller damping ratio.", "ratio"),
                        C("XTRACK_I", "Cross-track I", "Cross-track integrator gain.", "gain")
                    ]),
                HarmonicNotch("harmonic-notch", "INS_HNTCH"),
                GyroFilters(),
                EstimatorDescriptor()
            ]);
    }

    private static ExtendedTuningProfile Sub()
    {
        return new ExtendedTuningProfile(
            FirmwareFamily.ArduSub,
            [
                RateDescriptor(["RLL", "PIT", "YAW"]),
                Descriptor("depth-position", "Depth", "Depth position controller", "Depth position and vertical velocity response.",
                    "PSC_POSZ", [""], [0],
                    [C("P", "Depth position P", "Depth-position proportional gain.", "gain")]),
                Descriptor("depth-velocity", "Depth", "Depth velocity controller", "Vertical velocity PID and integrator limit.",
                    "PSC_VELZ", [""], [0],
                    [
                        C("P", "Velocity P", "Vertical velocity proportional gain.", "gain"),
                        C("I", "Velocity I", "Vertical velocity integral gain.", "gain"),
                        C("D", "Velocity D", "Vertical velocity derivative gain.", "gain"),
                        C("IMAX", "Velocity I limit", "Maximum vertical integral contribution.", "cm/s²")
                    ],
                    [PositiveCompanion("I", "IMAX", "A positive depth-velocity I gain requires a positive integrator limit.")]),
                Descriptor("depth-accel", "Depth", "Depth acceleration controller", "Vertical acceleration PID and feed-forward controller.",
                    "PSC_ACCZ", [""], [0], PidComponents(true),
                    [PositiveCompanion("I", "IMAX", "A positive depth-acceleration I gain requires a positive integrator limit.")]),
                HarmonicNotch("harmonic-notch", "INS_HNTCH"),
                GyroFilters(),
                Descriptor("autotune", "Autotune setup", "Autotune configuration", "Parameters that configure a later on-vehicle autotune; this page never starts it.",
                    "AUTOTUNE", [""], [0],
                    [
                        C("AXES", "Axes mask", "Controller axes eligible for autotune.", "bitmask"),
                        C("AGGR", "Aggressiveness", "Target autotune aggressiveness.", "ratio")
                    ]),
                EstimatorDescriptor()
            ]);
    }

    private static AdvancedTuningDescriptor RateDescriptor(IReadOnlyList<string> axes)
    {
        return Descriptor("rate-pid", "Controllers", "Body-rate controllers", "Repeated rate PID, feed-forward, limits, and filters by axis.",
            "ATC_RAT_{axis}", axes, [0], PidComponents(true),
            [PositiveCompanion("I", "IMAX", "A positive rate I gain requires a positive integrator limit.")]);
    }

    private static AdvancedTuningDescriptor HarmonicNotch(string key, string prefix)
    {
        return Descriptor(key, "Filters", prefix == "INS_HNTCH" ? "Primary harmonic notch" : "Secondary harmonic notch",
            "Harmonic sensor-noise notch center, bandwidth, attenuation, and tracking mode.",
            prefix, [""], [0],
            [
                C("ENABLE", "Enabled", "Enable this harmonic notch when supported.", "enum"),
                C("REF", "Reference", "Tracking reference; interpretation depends on the tracking mode.", "ratio"),
                C("OPTS", "Options", "Firmware-supported notch options.", "bitmask"),
                C("FREQ", "Center frequency", "Notch center frequency.", "Hz"),
                C("BW", "Bandwidth", "Notch bandwidth.", "Hz"),
                C("ATT", "Attenuation", "Notch attenuation.", "dB"),
                C("MODE", "Tracking mode", "Source used to track the notch frequency.", "enum"),
                C("HMNCS", "Harmonics", "Enabled harmonic mask.", "bitmask")
            ],
            [
                PositiveCompanion("FREQ", "BW", "A positive notch frequency requires positive bandwidth."),
                LessOrEqual("BW", "FREQ", "Notch bandwidth must not exceed its center frequency.")
            ],
            FilterWarning);
    }

    private static AdvancedTuningDescriptor GyroFilters()
    {
        return Descriptor("gyro-filters", "Filters", "Gyro instance filters", "Repeated low-pass filter cutoffs for available gyro instances.",
            "INS_GYRO{instance}", [""], [0, 2, 3],
            [C("FILTER", "Gyro low-pass", "Gyroscope low-pass filter cutoff.", "Hz")],
            warning: FilterWarning);
    }

    private static AdvancedTuningDescriptor EstimatorDescriptor()
    {
        return Descriptor("ekf3-noise", "Estimator", "EKF3 observation noise", "Selected EKF3 observation-noise values that influence estimator weighting.",
            "EK3", [""], [0],
            [
                C("POSNE_M_NSE", "Horizontal position noise", "GPS horizontal position observation noise.", "m"),
                C("VELNE_M_NSE", "Horizontal velocity noise", "GPS horizontal velocity observation noise.", "m/s"),
                C("ALT_M_NSE", "Altitude noise", "Altitude observation noise.", "m"),
                C("MAG_M_NSE", "Magnetic noise", "Magnetic-field observation noise.", "gauss")
            ],
            warning: "Estimator noise values affect sensor weighting and can cause position or attitude divergence when set incorrectly.");
    }

    private static IReadOnlyList<AdvancedTuningComponent> PidComponents(bool includeFeedForward)
    {
        var components = new List<AdvancedTuningComponent> { C("P", "Proportional", "Proportional controller gain.", "gain"), C("I", "Integral", "Integral controller gain.", "gain"), C("D", "Derivative", "Derivative controller gain.", "gain") };
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
        string? warning = null)
    {
        return new AdvancedTuningDescriptor(key, category, title, description, prefix, axes, instances, components, rules ?? [], warning ?? ControllerWarning);
    }

    private static AdvancedTuningComponent C(string key, string title, string description, string units)
    {
        return new AdvancedTuningComponent(key, title, description, units);
    }

    private static BasicTuningRule LessOrEqual(string first, string second, string message)
    {
        return new BasicTuningRule(BasicTuningRuleKind.LessThanOrEqual, first, second, message);
    }

    private static BasicTuningRule PositiveCompanion(string first, string second, string message)
    {
        return new BasicTuningRule(BasicTuningRuleKind.PositiveCompanion, first, second, message);
    }
}
