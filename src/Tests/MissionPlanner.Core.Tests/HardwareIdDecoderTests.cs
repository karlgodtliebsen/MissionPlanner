using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.Core.Tests;

public sealed class HardwareIdDecoderTests
{
    [Fact]
    public void DecodesSensorBusAddressAndType()
    {
        var item = HardwareIdDecoder.Decode("COMPASS_DEV_ID", 1 | (2 << 3) | (30 << 8) | (7 << 16));
        Assert.Equal("I2C", item.BusType);
        Assert.Equal("2", item.Bus);
        Assert.Equal("30", item.Address);
        Assert.Equal("HMC5883", item.DeviceType);
    }

    [Fact]
    public void CanSensorIdsAndUnknownTypesRemainExplicit()
    {
        Assert.Equal("Sensor ID 12", HardwareIdDecoder.Decode("BARO1_DEVID", 3 | (12 << 16)).DeviceType);
        Assert.Equal("Unknown (255)", HardwareIdDecoder.Decode("INS_ACC_ID", 2 | (255 << 16)).DeviceType);
        Assert.Equal("—", HardwareIdDecoder.Decode("MAV_SYSID", 16).BusType);
        Assert.Equal("—", HardwareIdDecoder.Decode("INS_ACC_ID", double.NaN).BusType);
    }
}
