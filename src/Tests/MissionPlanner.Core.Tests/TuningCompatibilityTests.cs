using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Firmware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks firmware-name selection and native-unit behavior across tuning generations.</summary>
public sealed class TuningCompatibilityTests
{
    /// <summary>Reported current names take precedence without modifying values or hiding old firmware.</summary>
    [Theory]
    [InlineData("WPNAV_SPEED", "WP_SPD", "cm/s", "m/s")]
    [InlineData("WPNAV_ACCEL", "WP_ACC", "cm/s²", "m/s²")]
    [InlineData("LOIT_SPEED", "LOIT_SPEED_MS", "cm/s", "m/s")]
    [InlineData("PSC_ACCZ_P", "PSC_D_ACC_P", "gain", "gain")]
    public void ResolvesOnlyReportedAliasWithNativeUnits(string oldName, string currentName, string oldUnits, string currentUnits)
    {
        var definition = new ParameterFieldDefinition(oldName, TuningParameterCompatibility.Names(oldName));
        var parameters = new Dictionary<string, VehicleParameter>
        {
            [oldName] = new(oldName, 500, MavParamType.Real32, 0, 2)
        };
        Assert.Equal(oldName, definition.Resolve(parameters));
        parameters[currentName] = new(currentName, 5, MavParamType.Real32, 1, 2);
        Assert.Equal(currentName, definition.Resolve(parameters));
        Assert.Equal(currentUnits, TuningParameterCompatibility.Units(currentName, oldUnits));
        Assert.Equal(500, parameters[oldName].Value);
        Assert.Equal(5, parameters[currentName].Value);
    }

    /// <summary>Coupled validation compares physical values instead of raw numbers from mixed aliases.</summary>
    [Fact]
    public void UnitConversionPreservesPhysicalSpeedAndRoverAcceleration()
    {
        Assert.Equal(TuningParameterCompatibility.ToBaseUnits(250, "cm/s"), TuningParameterCompatibility.ToBaseUnits(2.5, "m/s"));
        Assert.Equal("g", TuningParameterCompatibility.Units("ATC_TURN_MAX_G", "m/s²"));
        Assert.Equal(9.80665, TuningParameterCompatibility.ToBaseUnits(1, "g"), 6);
    }

    /// <summary>Plane exposes fixed-wing controllers; VTOL fields require reported Q-prefixed parameters.</summary>
    [Fact]
    public void PlaneDoesNotUseCopterControllerNames()
    {
        var catalog = new ExtendedTuningProfileCatalog();
        var profile = catalog.GetProfile(FirmwareFamily.ArduPlane)!;
        var fields = profile.Descriptors.SelectMany(catalog.Expand).ToArray();
        Assert.Contains(fields, field => field.Parameter.ParameterNames.Contains("RLL_RATE_P"));
        Assert.Contains(fields, field => field.Parameter.ParameterNames.Contains("PTCH_RATE_I"));
        Assert.Contains(fields, field => field.Parameter.ParameterNames.Contains("Q_A_RAT_RLL_P"));
        Assert.DoesNotContain(fields, field => field.Parameter.ParameterNames.Any(name => name.StartsWith("ATC_RAT_", StringComparison.Ordinal)));
        var planeOnly = new Dictionary<string, VehicleParameter> { ["RLL_RATE_P"] = new("RLL_RATE_P", .1f, MavParamType.Real32, 0, 1) };
        Assert.All(fields.Where(field => field.Parameter.ParameterNames.Any(name => name.StartsWith("Q_", StringComparison.Ordinal))),
            field => Assert.Null(field.Parameter.Resolve(planeOnly)));
    }
    /// <summary>Old and current family snapshots resolve independently, with no cross-family substitutions.</summary>
    [Theory]
    [InlineData("ATC_RAT_PIT_FLTE", "RATE_PIT_FILT", "ATC_RAT_PIT_FLTE", "Hz", "Hz")]
    [InlineData("PSC_VELXY_P", "LOITER_LAT_P", "PSC_NE_VEL_P", "gain", "gain")]
    [InlineData("PSC_POSZ_P", "THR_ALT_P", "PSC_D_POS_P", "gain", "gain")]
    [InlineData("Q_WP_SPEED", "Q_WP_SPEED", "Q_WP_SPD", "cm/s", "m/s")]
    [InlineData("Q_WP_ACCEL_Z", "Q_WP_ACCEL_Z", "Q_WP_ACC_Z", "cm/s²", "m/s²")]
    [InlineData("Q_P_VELXY_I", "Q_P_VELXY_I", "Q_P_NE_VEL_I", "gain", "gain")]
    [InlineData("LIM_ROLL_CD", "LIM_ROLL_CD", "ROLL_LIMIT_DEG", "cdeg", "deg")]
    [InlineData("AIRSPEED_CRUISE", "TRIM_ARSPD_CM", "AIRSPEED_CRUISE", "cm/s", "m/s")]
    [InlineData("RC7_OPTION", "CH7_OPT", "RC7_OPTION", "enum", "enum")]
    public void FamilySnapshotAliasMatrix(string catalogName, string historical, string current, string oldUnits, string currentUnits)
    {
        var field = new ParameterFieldDefinition(catalogName, TuningParameterCompatibility.Names(catalogName));
        Dictionary<string, VehicleParameter> Snapshot(string name, float value) => new() { [name] = new(name, value, MavParamType.Real32, 0, 1) };
        var old = Snapshot(historical, 500);
        var modern = Snapshot(current, 5);
        Assert.Equal(historical, field.Resolve(old));
        Assert.Equal(current, field.Resolve(modern));
        Assert.Equal(currentUnits, TuningParameterCompatibility.Units(current, currentUnits));
        Assert.Equal(oldUnits, TuningParameterCompatibility.Units(historical, oldUnits));
        Assert.Equal(500, old[historical].Value);
        Assert.Equal(5, modern[current].Value);
        Assert.Null(field.Resolve(Snapshot("UNRELATED_PARAM", 1)));
    }

    /// <summary>Rover offers independent old/new controllers, output/braking and obstacle response.</summary>
    [Fact]
    public void RoverProfileIncludesLegacyControllersAndMissingControls()
    {
        var extended = new ExtendedTuningProfileCatalog();
        var names = extended.GetProfile(FirmwareFamily.Rover)!.Descriptors.SelectMany(extended.Expand)
            .SelectMany(field => field.Parameter.ParameterNames).ToHashSet();
        Assert.All(new[] { "ATC_SPEED_P", "ATC_STR_RAT_I", "STEER2SRV_IMAX", "SPEED2THR_D", "MOT_PWM_TYPE", "RNGFND_TRIGGR_CM", "SONAR_TURN_ANGLE", "RC7_OPTION", "CH7_OPTION" }, name => Assert.Contains(name, names));
        Assert.DoesNotContain("RLL_RATE_P", names);
        Assert.DoesNotContain("Q_A_RAT_RLL_P", names);
        var basic = new BasicTuningProfileCatalog().GetProfile(FirmwareFamily.Rover)!;
        var basics = basic.Groups.SelectMany(group => group.Fields).SelectMany(field => field.Parameter.ParameterNames).ToHashSet();
        Assert.All(new[] { "ATC_BRAKE", "WP_SPEED", "CRUISE_SPEED", "ATC_TURN_MAX_G", "TURN_RADIUS", "WP_OVERSHOOT" }, name => Assert.Contains(name, basics));
    }

}
