using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Tuning;

/// <summary>Provides presence-gated Basic Tuning catalogs for supported ArduPilot families.</summary>
public sealed class BasicTuningProfileCatalog : IBasicTuningProfileCatalog
{
    private static readonly IReadOnlyDictionary<FirmwareFamily, BasicTuningProfile> profiles =
        new Dictionary<FirmwareFamily, BasicTuningProfile>
        {
            [FirmwareFamily.ArduCopter] = Copter(),
            [FirmwareFamily.ArduPlane] = Plane(),
            [FirmwareFamily.Rover] = Rover(),
            [FirmwareFamily.ArduSub] = Sub()
        };

    /// <inheritdoc />
    public BasicTuningProfile? GetProfile(FirmwareFamily family) => profiles.GetValueOrDefault(family);

    private static BasicTuningProfile Copter() => new(
        FirmwareFamily.ArduCopter,
        [
            Group(
                "pilot-feel",
                "Pilot feel",
                "Adjust how quickly commanded attitude and yaw inputs are applied.",
                [
                    Field("input-time", "Input smoothing", "Time used to smooth pilot attitude inputs; lower values feel sharper.", "s", "ATC_INPUT_TC",
                        warning: "Very low input smoothing can make the aircraft difficult to control."),
                    Field("yaw-rate", "Maximum pilot yaw rate", "Maximum yaw rate commanded by the pilot.", "deg/s", "PILOT_Y_RATE")
                ],
                warning: "Aggressive pilot-feel settings can destabilize a poorly tuned aircraft."),
            Group(
                "climb-descent",
                "Climb and descent",
                "Set pilot vertical speeds and acceleration.",
                [
                    AliasField("pilot-speed-up", "Pilot climb speed", "Maximum pilot-commanded climb speed.", "cm/s", ["PILOT_SPEED_UP", "PILOT_VELZ_MAX"]),
                    Field("pilot-speed-down", "Pilot descent speed", "Maximum pilot-commanded descent speed.", "cm/s", "PILOT_SPEED_DN"),
                    Field("pilot-accel-z", "Pilot vertical acceleration", "Acceleration used to reach pilot climb and descent speeds.", "cm/s²", "PILOT_ACCEL_Z")
                ],
                [
                    LessOrEqual("pilot-speed-down", "pilot-speed-up", "Pilot descent speed must not exceed pilot climb speed."),
                    PositiveCompanion("pilot-speed-up", "pilot-accel-z", "A positive pilot climb speed requires positive vertical acceleration.")
                ]),
            Group(
                "navigation",
                "Navigation speed",
                "Set autonomous horizontal and vertical speed limits and acceleration.",
                [
                    Field("wp-speed", "Waypoint speed", "Horizontal speed used for waypoint navigation.", "cm/s", "WPNAV_SPEED"),
                    Field("wp-speed-up", "Waypoint climb speed", "Vertical climb speed used during waypoint navigation.", "cm/s", "WPNAV_SPEED_UP"),
                    Field("wp-speed-down", "Waypoint descent speed", "Vertical descent speed used during waypoint navigation.", "cm/s", "WPNAV_SPEED_DN"),
                    Field("wp-accel", "Waypoint acceleration", "Horizontal acceleration used to reach waypoint speed.", "cm/s²", "WPNAV_ACCEL"),
                    Field("wp-accel-z", "Waypoint vertical acceleration", "Vertical acceleration used during waypoint navigation.", "cm/s²", "WPNAV_ACCEL_Z")
                ],
                [
                    LessOrEqual("wp-speed-down", "wp-speed-up", "Waypoint descent speed must not exceed waypoint climb speed."),
                    PositiveCompanion("wp-speed", "wp-accel", "A positive waypoint speed requires positive waypoint acceleration.")
                ]),
            Group(
                "loiter",
                "Loiter behavior",
                "Set position-mode speed and acceleration limits.",
                [
                    Field("loiter-speed", "Loiter speed", "Maximum horizontal speed in loiter and position modes.", "cm/s", "LOIT_SPEED"),
                    Field("loiter-accel", "Loiter acceleration", "Maximum horizontal acceleration in loiter and position modes.", "cm/s²", "LOIT_ACC_MAX")
                ],
                [PositiveCompanion("loiter-speed", "loiter-accel", "A positive loiter speed requires positive loiter acceleration.")])
        ]);

    private static BasicTuningProfile Plane() => new(
        FirmwareFamily.ArduPlane,
        [
            Group(
                "responsiveness",
                "Attitude responsiveness",
                "Adjust roll and pitch response time without exposing PID gains.",
                [
                    Field("roll-time", "Roll response time", "Time constant used to reach the demanded roll angle.", "s", "RLL2SRV_TCONST"),
                    Field("pitch-time", "Pitch response time", "Time constant used to reach the demanded pitch angle.", "s", "PTCH2SRV_TCONST")
                ],
                warning: "Response-time changes affect closed-loop control; make small changes and flight-test safely."),
            Group(
                "airspeed-throttle",
                "Airspeed and throttle",
                "Set cruise airspeed and the normal throttle envelope.",
                [
                    Field("cruise-airspeed", "Cruise airspeed", "Target airspeed used by automatic flight modes.", "m/s", "AIRSPEED_CRUISE"),
                    Field("throttle-min", "Minimum throttle", "Minimum automatic throttle output.", "%", "THR_MIN"),
                    Field("throttle-cruise", "Cruise throttle", "Nominal throttle required for level cruise.", "%", "TRIM_THROTTLE"),
                    Field("throttle-max", "Maximum throttle", "Maximum automatic throttle output.", "%", "THR_MAX")
                ],
                [
                    LessOrEqual("throttle-min", "throttle-cruise", "Minimum throttle must not exceed cruise throttle."),
                    LessOrEqual("throttle-cruise", "throttle-max", "Cruise throttle must not exceed maximum throttle.")
                ]),
            Group(
                "climb-descent",
                "Climb and descent",
                "Set TECS climb and sink-rate limits.",
                [
                    Field("climb-max", "Maximum climb rate", "Maximum demanded climb rate.", "m/s", "TECS_CLMB_MAX"),
                    Field("sink-min", "Minimum sink rate", "Normal descent-rate target.", "m/s", "TECS_SINK_MIN"),
                    Field("sink-max", "Maximum sink rate", "Maximum demanded descent rate.", "m/s", "TECS_SINK_MAX")
                ],
                [LessOrEqual("sink-min", "sink-max", "Minimum sink rate must not exceed maximum sink rate.")]),
            Group(
                "navigation",
                "Navigation and loiter",
                "Set navigation response and loiter geometry.",
                [
                    Field("l1-period", "Navigation period", "L1 navigation period; lower values command tighter path tracking.", "s", "NAVL1_PERIOD"),
                    Field("l1-damping", "Navigation damping", "Damping applied to L1 navigation corrections.", "ratio", "NAVL1_DAMPING"),
                    Field("loiter-radius", "Loiter radius", "Nominal radius used by loiter commands.", "m", "WP_LOITER_RAD"),
                    Field("roll-limit", "Roll limit", "Maximum automatic bank angle.", "cdeg", "LIM_ROLL_CD",
                        warning: "Increasing the roll limit raises stall and structural-load risk.")
                ])
        ]);

    private static BasicTuningProfile Rover() => new(
        FirmwareFamily.Rover,
        [
            Group(
                "speed",
                "Cruise and navigation speed",
                "Set manual cruise references and automatic waypoint speed.",
                [
                    AliasField("wp-speed", "Waypoint speed", "Target speed in automatic waypoint navigation.", "m/s", ["WP_SPEED", "CRUISE_SPEED"]),
                    Field("pilot-speed", "Pilot speed", "Maximum speed commanded in pilot-assisted modes.", "m/s", "PILOT_SPEED"),
                    Field("cruise-throttle", "Cruise throttle", "Throttle normally required to maintain cruise speed.", "%", "CRUISE_THROTTLE")
                ]),
            Group(
                "acceleration",
                "Acceleration and braking",
                "Set how quickly the vehicle changes speed.",
                [
                    Field("accel-max", "Maximum acceleration", "Maximum forward acceleration used by the speed controller.", "m/s²", "ATC_ACCEL_MAX"),
                    Field("decel-max", "Maximum deceleration", "Maximum commanded braking deceleration.", "m/s²", "ATC_DECEL_MAX")
                ],
                warning: "High acceleration or braking can cause wheel slip, rollover, or loss of steering authority."),
            Group(
                "turning",
                "Turn behavior",
                "Set path-following and steering-rate limits.",
                [
                    Field("turn-g", "Maximum turn acceleration", "Maximum lateral acceleration requested in a turn.", "m/s²", "TURN_MAX_G"),
                    Field("steer-rate", "Maximum steering rate", "Maximum steering-rate command.", "deg/s", "ATC_STR_RAT_MAX"),
                    Field("steer-accel", "Steering acceleration", "Maximum change in steering rate.", "deg/s²", "ATC_STR_ACC_MAX"),
                    Field("pivot-angle", "Pivot-turn angle", "Heading error at which a pivot turn is requested.", "deg", "WP_PIVOT_ANGLE")
                ],
                [PositiveCompanion("steer-rate", "steer-accel", "A positive steering rate requires positive steering acceleration.")])
        ]);

    private static BasicTuningProfile Sub() => new(
        FirmwareFamily.ArduSub,
        [
            Group(
                "pilot-feel",
                "Pilot feel",
                "Adjust input smoothing and acro rotation-rate limits.",
                [
                    Field("input-time", "Input smoothing", "Time used to smooth pilot attitude inputs.", "s", "ATC_INPUT_TC"),
                    Field("acro-rp", "Acro roll/pitch rate", "Maximum acro roll and pitch rate.", "deg/s", "ACRO_RP_RATE"),
                    Field("acro-yaw", "Acro yaw rate", "Maximum acro yaw rate.", "deg/s", "ACRO_Y_RATE")
                ],
                warning: "Aggressive rate limits can make a submerged vehicle difficult to recover."),
            Group(
                "vertical",
                "Vertical motion",
                "Set pilot climb, dive, and vertical acceleration limits.",
                [
                    Field("pilot-speed-up", "Pilot ascent speed", "Maximum pilot-commanded ascent speed.", "cm/s", "PILOT_SPEED_UP"),
                    Field("pilot-speed-down", "Pilot descent speed", "Maximum pilot-commanded descent speed.", "cm/s", "PILOT_SPEED_DN"),
                    Field("pilot-accel-z", "Vertical acceleration", "Acceleration used to reach ascent and descent speeds.", "cm/s²", "PILOT_ACCEL_Z")
                ],
                [
                    LessOrEqual("pilot-speed-down", "pilot-speed-up", "Pilot descent speed must not exceed pilot ascent speed."),
                    PositiveCompanion("pilot-speed-up", "pilot-accel-z", "A positive ascent speed requires positive vertical acceleration.")
                ]),
            Group(
                "navigation",
                "Navigation speed",
                "Set automatic horizontal speed and acceleration.",
                [
                    Field("wp-speed", "Waypoint speed", "Horizontal speed used for waypoint navigation.", "cm/s", "WPNAV_SPEED"),
                    Field("wp-accel", "Waypoint acceleration", "Horizontal acceleration used to reach waypoint speed.", "cm/s²", "WPNAV_ACCEL")
                ],
                [PositiveCompanion("wp-speed", "wp-accel", "A positive waypoint speed requires positive waypoint acceleration.")])
        ]);

    private static BasicTuningGroupDefinition Group(
        string key,
        string title,
        string description,
        IReadOnlyList<BasicTuningFieldDefinition> fields,
        IReadOnlyList<BasicTuningRule>? rules = null,
        string? warning = null) =>
        new(key, title, description, fields, rules ?? [], warning);

    private static BasicTuningFieldDefinition Field(
        string key,
        string title,
        string description,
        string units,
        string parameterName,
        string? warning = null) =>
        new(key, title, description, units, ParameterFieldDefinition.Exact(parameterName), warning);

    private static BasicTuningFieldDefinition AliasField(
        string key,
        string title,
        string description,
        string units,
        IReadOnlyList<string> parameterNames) =>
        new(key, title, description, units, new ParameterFieldDefinition(key, parameterNames));

    private static BasicTuningRule LessOrEqual(string first, string second, string message) =>
        new(BasicTuningRuleKind.LessThanOrEqual, first, second, message);

    private static BasicTuningRule PositiveCompanion(string first, string second, string message) =>
        new(BasicTuningRuleKind.PositiveCompanion, first, second, message);
}
