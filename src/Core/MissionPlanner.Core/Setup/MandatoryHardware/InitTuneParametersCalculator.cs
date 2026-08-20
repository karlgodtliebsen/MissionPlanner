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
        if (propellerInches <= 0 || batteryCells < 1 || cellMinimumVoltage <= 0 || cellMaximumVoltage <= cellMinimumVoltage)
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
