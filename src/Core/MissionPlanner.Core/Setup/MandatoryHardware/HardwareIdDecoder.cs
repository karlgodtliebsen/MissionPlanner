namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Decodes ArduPilot packed sensor identifiers without interpreting unrelated ID parameters as devices.</summary>
public static class HardwareIdDecoder
{
    // Layout and device codes: ArduPilot/MissionPlanner ExtLibs/Utilities/Device.cs,
    // upstream 8cdd00fe2390841fc0d80b4c1d1462d4ce8bb0e5 (GPL-3.0).
    /// <summary>Builds diagnostic fields; unknown or non-device IDs retain their original value.</summary>
    public static HwIdItem Decode(string name, double value)
    {
        var valid = double.IsFinite(value) && value >= 0 && value <= uint.MaxValue && value == Math.Truncate(value);
        var item = new HwIdItem(name, value, valid ? $"0x{(uint)value:X8} ({value:0})" : "Invalid identifier");
        var family = name.StartsWith("COMPASS", StringComparison.Ordinal) ? "compass"
            : name.StartsWith("BARO", StringComparison.Ordinal) ? "baro"
            : name.StartsWith("ARSPD", StringComparison.Ordinal) || name.StartsWith("ASP", StringComparison.Ordinal) ? "airspeed"
            : name.StartsWith("INS", StringComparison.Ordinal) ? "imu" : null;
        var isDeviceId = name.Contains("DEVID", StringComparison.Ordinal) || name.Contains("DEV_ID", StringComparison.Ordinal)
            || name.StartsWith("INS_ACC", StringComparison.Ordinal) && name.Contains("_ID", StringComparison.Ordinal)
            || name.StartsWith("INS_GYR", StringComparison.Ordinal) && name.Contains("_ID", StringComparison.Ordinal);
        if (!valid || value == 0 || family is null || !isDeviceId)
        {
            return item;
        }
        var id = (uint)value;
        var bus = id & 7;
        var type = (id >> 16) & 255;
        return item with
        {
            BusType = bus switch { 1 => "I2C", 2 => "SPI", 3 => "UAVCAN", 4 => "SITL", 5 => "MSP", 6 => "Serial", _ => $"Unknown ({bus})" },
            Bus = ((id >> 3) & 31).ToString(),
            Address = ((id >> 8) & 255).ToString(),
            DeviceType = bus == 3 ? $"Sensor ID {type}" : family switch
            {
                "compass" => compass_type(type),
                "baro" => baro_types(type),
                "airspeed" => airspeed_types(type),
                _ => imu_types(type),
            },
        };
    }

    private static string compass_type(uint code) => code switch
    {
        0x01 => "HMC5883_OLD",
        0x07 => "HMC5883",
        0x02 => "LSM303D",
        0x04 => "AK8963",
        0x05 => "BMM150",
        0x06 => "LSM9DS1",
        0x08 => "LIS3MDL",
        0x09 => "AK09916",
        0x0A => "IST8310",
        0x0B => "ICM20948",
        0x0C => "MMC3416",
        0x0D => "QMC5883L",
        0x0E => "MAG3110",
        0x0F => "SITL",
        0x10 => "IST8308",
        0x11 => "RM3100",
        0x12 => "RM3100_2",
        0x13 => "MMC5883",
        0x14 => "AK09918",
        0x15 => "AK09915",
        0x16 => "QMC5883P",
        0x17 => "BMM350",
        0x18 => "IIS2MDC",
        0x19 => "LIS2MDL",
        _ => $"Unknown ({code})",
    };

    private static string imu_types(uint code) => code switch
    {
        0x09 => "BMI160",
        0x10 => "L3G4200D",
        0x11 => "ACC_LSM303D",
        0x12 => "ACC_BMA180",
        0x13 => "ACC_MPU6000",
        0x16 => "ACC_MPU9250",
        0x17 => "ACC_IIS328DQ",
        0x18 => "ACC_LSM9DS1",
        0x21 => "GYR_MPU6000",
        0x22 => "GYR_L3GD20",
        0x24 => "GYR_MPU9250",
        0x25 => "GYR_I3G4250D",
        0x26 => "GYR_LSM9DS1",
        0x27 => "INS_ICM20789",
        0x28 => "INS_ICM20689",
        0x29 => "INS_BMI055",
        0x2A => "SITL",
        0x2B => "INS_BMI088",
        0x2C => "INS_ICM20948",
        0x2D => "INS_ICM20648",
        0x2E => "INS_ICM20649",
        0x2F => "INS_ICM20602",
        0x30 => "INS_ICM20601",
        0x31 => "INS_ADIS1647X",
        0x32 => "SERIAL",
        0x33 => "INS_ICM40609",
        0x34 => "INS_ICM42688",
        0x35 => "INS_ICM42605",
        0x36 => "INS_ICM40605",
        0x37 => "INS_IIM42652",
        0x38 => "BMI270",
        0x39 => "INS_BMI085",
        0x3A => "INS_ICM42670",
        0x3B => "INS_ICM45686",
        0x3C => "INS_SCHA63T",
        0x3D => "INS_IIM42653",
        0x3E => "INS_LSM6DSV",
        0x3F => "INS_ASM330",
        _ => $"Unknown ({code})",
    };

    private static string baro_types(uint code) => code switch
    {
        0x01 => "BARO_SITL",
        0x02 => "BARO_BMP085",
        0x03 => "BARO_BMP280",
        0x04 => "BARO_BMP388",
        0x05 => "BARO_DPS280",
        0x06 => "BARO_DPS310",
        0x07 => "BARO_FBM320",
        0x08 => "BARO_ICM20789",
        0x09 => "BARO_KELLERLD",
        0x0A => "BARO_LPS2XH",
        0x0B => "BARO_MS5611",
        0x0C => "BARO_SPL06",
        0x0D => "BARO_DRONECAN",
        0x0E => "BARO_MSP",
        0x0F => "BARO_ICP101XX",
        0x10 => "BARO_ICP201XX",
        0x11 => "BARO_MS5607",
        0x12 => "BARO_MS5837_30BA",
        0x13 => "BARO_MS5637",
        0x14 => "BARO_BMP390",
        0x15 => "BARO_BMP581",
        0x16 => "BARO_SPA06",
        0x17 => "BARO_AUAV",
        0x18 => "BARO_MS5837_02BA",
        _ => $"Unknown ({code})",
    };

    private static string airspeed_types(uint code) => code switch
    {
        0x01 => "AIRSPEED_SITL",
        0x02 => "AIRSPEED_MS4525",
        0x03 => "AIRSPEED_MS5525",
        0x04 => "AIRSPEED_DLVR",
        0x05 => "AIRSPEED_MSP",
        0x06 => "AIRSPEED_SDP3X",
        0x07 => "AIRSPEED_DRONECAN",
        0x08 => "AIRSPEED_ANALOG",
        0x09 => "AIRSPEED_NMEA",
        0x0A => "AIRSPEED_ASP5033",
        _ => $"Unknown ({code})",
    };

}
