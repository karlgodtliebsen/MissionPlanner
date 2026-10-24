namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Calculates the original MissionPlanner initial multicopter tune recommendations.</summary>
public static class InitTuneParametersCalculator
{
    /// <summary>Calculates recommendations for propeller and battery characteristics.</summary>
    public static IReadOnlyDictionary<string, double> Calculate(
        double propellerInches,
        int batteryCells,
        double cellMaximumVoltage,
        double cellMinimumVoltage,
        bool tMotorEsc = false)
    {
        if (!double.IsFinite(propellerInches) || !double.IsFinite(cellMinimumVoltage) || !double.IsFinite(cellMaximumVoltage) ||
            propellerInches <= 0 || propellerInches > 100 || batteryCells is < 1 or > 32 || cellMinimumVoltage <= 0 || cellMaximumVoltage <= cellMinimumVoltage)
        {
            throw new ArgumentOutOfRangeException(nameof(propellerInches), "Propeller size and battery values must describe a valid vehicle.");
        }

        var yawAcceleration = Math.Max(8000, RoundTo(-900 * propellerInches + 36000, -2));
        var pitchAcceleration = Math.Max(10000, RoundTo(
            -2.613267 * Math.Pow(propellerInches, 3) +
            343.39216 * Math.Pow(propellerInches, 2) -
            15083.7121 * propellerInches + 235771,
            -2));
        var gyroFilter = Math.Max(20, Math.Round(289.22 * Math.Pow(propellerInches, -0.838), 0));
        var rateFilter = Math.Max(10, gyroFilter / 2);
        return new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["ACRO_YAW_P"] = 0.5 * yawAcceleration / 4500,
            ["ATC_ACCEL_P_MAX"] = pitchAcceleration,
            ["ATC_ACCEL_R_MAX"] = pitchAcceleration,
            ["ATC_ACCEL_Y_MAX"] = yawAcceleration,
            ["INS_GYRO_FILTER"] = gyroFilter,
            ["ATC_RAT_PIT_FLTD"] = rateFilter,
            ["ATC_RAT_PIT_FLTT"] = rateFilter,
            ["ATC_RAT_RLL_FLTT"] = rateFilter,
            ["ATC_RAT_YAW_FLTT"] = rateFilter,
            ["ATC_RAT_PIT_FLTE"] = 0,
            ["ATC_RAT_RLL_FLTE"] = 0,
            ["ATC_RAT_YAW_FLTD"] = 0,
            ["ATC_RAT_RLL_FLTD"] = rateFilter,
            ["ATC_RAT_YAW_FLTE"] = 2,
            ["ATC_THR_MIX_MAN"] = 0.1,
            ["INS_ACCEL_FILTER"] = 10,
            ["MOT_THST_EXPO"] = tMotorEsc ? 0.2 : Math.Min(Math.Round(0.15686 * Math.Log(propellerInches) + 0.23693, 2), 0.8),
            ["MOT_THST_HOVER"] = 0.2,
            ["BATT_ARM_VOLT"] = (batteryCells - 1) * 0.1 + (cellMinimumVoltage + 0.3) * batteryCells,
            ["BATT_CRT_VOLT"] = (cellMinimumVoltage + 0.2) * batteryCells,
            ["BATT_LOW_VOLT"] = (cellMinimumVoltage + 0.3) * batteryCells,
            ["MOT_BAT_VOLT_MAX"] = cellMaximumVoltage * batteryCells,
            ["MOT_BAT_VOLT_MIN"] = cellMinimumVoltage * batteryCells
        };
    }

    /// <summary>Maps historical recommendations to reported Copter or QuadPlane parameters in their native units.</summary>
    /// <param name="recommendations">Calculated historical recommendation values.</param>
    /// <param name="reportedNames">Names reported by the selected firmware.</param>
    /// <param name="quadPlane">Whether recommendations target the QuadPlane controllers.</param>
    /// <returns>Only supported names, with centidegree acceleration converted for newer degree-based parameters.</returns>
    public static IReadOnlyDictionary<string, double> Resolve(
        IReadOnlyDictionary<string, double> recommendations, IEnumerable<string> reportedNames, bool quadPlane)
    {
        var names = reportedNames.ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (original, value) in recommendations)
        {
            var name = quadPlane && original.StartsWith("ATC_", StringComparison.Ordinal) ? "Q_A_" + original[4..] :
                quadPlane && original.StartsWith("MOT_", StringComparison.Ordinal) ? "Q_M_" + original[4..] : original;
            var acceleration = name.Replace("_ACCEL_", "_ACC_", StringComparison.Ordinal);
            if (acceleration != name && names.Contains(acceleration))
            {
                result[acceleration] = value / 100d;
            }
            else if (names.Contains(name))
            {
                result[name] = value;
            }
            else if ((name.EndsWith("_FLTD", StringComparison.Ordinal) && !name.Contains("_YAW_", StringComparison.Ordinal)) ||
                     name.EndsWith("_YAW_FLTE", StringComparison.Ordinal))
            {
                var legacy = name[..^4] + "FILT";
                if (names.Contains(legacy))
                {
                    result[legacy] = value;
                }
            }
        }
        return result;
    }

    private static double RoundTo(double value, int precision)
    {
        if (precision >= 0)
        {
            return Math.Round(value, precision);
        }

        var increment = (int)Math.Pow(10, Math.Abs(precision));
        value += 5 * increment / 10;
        return Math.Round(value - value % increment, 0);
    }
}
