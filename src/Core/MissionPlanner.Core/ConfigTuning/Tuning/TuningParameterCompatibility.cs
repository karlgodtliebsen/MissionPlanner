namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Resolves reported tuning names without changing the native values stored in the edit session.</summary>
public static class TuningParameterCompatibility
{
    private static readonly IReadOnlyDictionary<string, string> modernNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["WPNAV_SPEED"] = "WP_SPD",
        ["WPNAV_SPEED_UP"] = "WP_SPD_UP",
        ["WPNAV_SPEED_DN"] = "WP_SPD_DN",
        ["WPNAV_RADIUS"] = "WP_RADIUS_M",
        ["WPNAV_ACCEL"] = "WP_ACC",
        ["WPNAV_ACCEL_Z"] = "WP_ACC_Z",
        ["WPNAV_ACCEL_C"] = "WP_ACC_CNR",
        ["LIM_ROLL_CD"] = "ROLL_LIMIT_DEG",
        ["LIM_PITCH_MIN"] = "PTCH_LIM_MIN_DEG",
        ["LIM_PITCH_MAX"] = "PTCH_LIM_MAX_DEG",
        ["LOIT_SPEED"] = "LOIT_SPEED_MS",
        ["LOIT_ACC_MAX"] = "LOIT_ACC_MAX_M"
    };

    /// <summary>Returns current and historical names in precedence order; values remain in the selected name's native units.</summary>
    /// <param name="name">The historical catalog name.</param>
    /// <returns>Explicit aliases for the same user-facing setting.</returns>
    public static IReadOnlyList<string> Names(string name)
    {
        if (name.StartsWith("Q_WP_", StringComparison.Ordinal))
        {
            var original = "WPNAV_" + name[5..];
            if (modernNames.TryGetValue(original, out var current)) return ["Q_" + current, name];
        }
        if (name.StartsWith("Q_LOIT_", StringComparison.Ordinal) && modernNames.TryGetValue(name[2..], out var loiter)) return ["Q_" + loiter, name];
        if (name.StartsWith("RC", StringComparison.Ordinal) && name.EndsWith("_OPTION", StringComparison.Ordinal) &&
            int.TryParse(name[2..^7], out var channel) && channel is >= 5 and <= 16)
            return [name, $"CH{channel}_OPTION", $"CH{channel}_OPT"];
        if (name == "TUNE_MIN") return [name, "TUNE_LOW"];
        if (name == "TUNE_MAX") return [name, "TUNE_HIGH"];
        if (name == "AIRSPEED_CRUISE") return [name, "TRIM_ARSPD_CM"];
        if (name == "AIRSPEED_MIN") return [name, "ARSPD_FBW_MIN"];
        if (name == "AIRSPEED_MAX") return [name, "ARSPD_FBW_MAX"];
        if (name.StartsWith("ATC_ANG_", StringComparison.Ordinal)) return [name, "STB_" + name[8..]];
        if (name.StartsWith("ATC_RAT_", StringComparison.Ordinal))
        {
            if (name.EndsWith("_FLTE", StringComparison.Ordinal))
                return [name, name[..^4] + "FILT", "RATE_" + name[8..^4] + "FILT"];
            return [name, "RATE_" + name[8..]];
        }
        if (name == "PSC_POSXY_P") return ["PSC_NE_POS_P", name, "POS_XY_P", "HLD_LAT_P"];
        if (name == "PSC_POSZ_P") return ["PSC_D_POS_P", name, "POS_Z_P", "THR_ALT_P"];
        if (name == "PSC_VELZ_P") return ["PSC_D_VEL_P", name, "VEL_Z_P", "THR_RATE_P"];
        if (name.StartsWith("PSC_VELXY_", StringComparison.Ordinal))
        {
            var component = name[10..];
            return ["PSC_NE_VEL_" + component, name, "VEL_XY_" + component, "LOITER_LAT_" + component];
        }
        if (name.StartsWith("PSC_ACCZ_", StringComparison.Ordinal))
        {
            var suffix = name[9..];
            return ["PSC_D_ACC_" + suffix, name, "ACCEL_Z_" + suffix, "THR_ACCEL_" + suffix];
        }
        if (modernNames.TryGetValue(name, out var modern))
        {
            return [modern, name];
        }

        foreach (var (oldPrefix, newPrefix) in new[]
        {
            ("PSC_POSXY_", "PSC_NE_POS_"), ("PSC_VELXY_", "PSC_NE_VEL_"),
            ("PSC_POSZ_", "PSC_D_POS_"), ("PSC_VELZ_", "PSC_D_VEL_"),
            ("PSC_ACCZ_", "PSC_D_ACC_"), ("Q_P_POSXY_", "Q_P_NE_POS_"),
            ("Q_P_VELXY_", "Q_P_NE_VEL_"), ("Q_P_POSZ_", "Q_P_D_POS_"),
            ("Q_P_VELZ_", "Q_P_D_VEL_"), ("Q_P_ACCZ_", "Q_P_D_ACC_")
        })
        {
            if (name.StartsWith(oldPrefix, StringComparison.Ordinal))
            {
                return [newPrefix + name[oldPrefix.Length..], name];
            }
        }

        return name == "TURN_MAX_G" ? ["ATC_TURN_MAX_G", name] : [name];
    }

    /// <summary>Returns a name-specific fallback for native units when firmware metadata supplies none.</summary>
    /// <param name="name">The actual reported parameter name.</param>
    /// <param name="fallback">The catalog fallback for historical firmware.</param>
    /// <returns>The native unit label, never a converted display value.</returns>
    public static string Units(string name, string fallback)
    {
        return name switch
        {
            "WP_SPD" or "WP_SPD_UP" or "WP_SPD_DN" or "LOIT_SPEED_MS" or "Q_WP_SPD" or "Q_WP_SPD_UP" or "Q_WP_SPD_DN" or "Q_LOIT_SPEED_MS" => "m/s",
            "TRIM_ARSPD_CM" => "cm/s",
            "ROLL_LIMIT_DEG" or "PTCH_LIM_MIN_DEG" or "PTCH_LIM_MAX_DEG" => "deg",
            "WP_ACC" or "WP_ACC_Z" or "WP_ACC_CNR" or "LOIT_ACC_MAX_M" or "Q_WP_ACC" or "Q_WP_ACC_Z" or "Q_WP_ACC_CNR" or "Q_LOIT_ACC_MAX_M" => "m/s²",
            "WP_RADIUS_M" or "Q_WP_RADIUS_M" => "m",
            "TURN_MAX_G" or "ATC_TURN_MAX_G" => "g",
            _ when (name.StartsWith("PSC_NE_", StringComparison.Ordinal) || name.StartsWith("PSC_D_", StringComparison.Ordinal) ||
                    name.StartsWith("Q_P_NE_", StringComparison.Ordinal) || name.StartsWith("Q_P_D_", StringComparison.Ordinal)) &&
                   name.EndsWith("VEL_IMAX", StringComparison.Ordinal) => "m/s²",
            _ when name.EndsWith("D_ACC_IMAX", StringComparison.Ordinal) => "ratio",
            _ when name.EndsWith("IMAX", StringComparison.Ordinal) &&
                (name.StartsWith("RATE_", StringComparison.Ordinal) || name.StartsWith("THR_ACCEL_", StringComparison.Ordinal) || name.StartsWith("ACCEL_Z_", StringComparison.Ordinal)) => "native output units",
            _ => fallback
        };
    }

    /// <summary>Converts a native value to base units for comparisons between differently scaled firmware aliases.</summary>
    /// <param name="value">The native value.</param>
    /// <param name="units">The metadata or resolved fallback units.</param>
    /// <returns>The comparison value; the pending parameter value is never mutated.</returns>
    public static double ToBaseUnits(double value, string? units)
    {
        return units switch
        {
            "cm" or "cm/s" or "cm/s²" or "cm/s/s" => value / 100d,
            "cdeg" or "cdeg/s" or "cdeg/s/s" => value / 100d,
            "g" => value * 9.80665d,
            _ => value
        };
    }
}
