using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies dynamic grouping and metadata-driven serial receiver diagnostics.</summary>
public sealed class SerialPortConfigurationTests
{
    [Fact]
    public void DiscoversSparseNumericGroupsAndFutureParameters()
    {
        var ports = SerialPortConfiguration.Discover([
            "SERIAL10_PROTOCOL", "SERIAL1_PROTOCOL", "SERIAL1_BAUD", "SERIAL1_OPTIONS",
            "SERIAL3_PROTOCOL", "SERIAL3_OPTIONS", "SERIAL20_FUTURE",
            "SERIAL_PASS1", "SERIAL_PROTOCOL", "SERIAL1", "SERIAL01_BAUD", "SERIAL-1_BAUD", "RC_OPTIONS"]);
        Assert.Equal(new[] { 1, 3, 10, 20 }, ports.Select(port => port.Index));
        Assert.Equal(new[] { "SERIAL1_PROTOCOL", "SERIAL1_BAUD", "SERIAL1_OPTIONS" }, ports[0].Names);
        Assert.Null(ports[1].Baud);
        Assert.Null(ports[2].Options);
        Assert.Null(ports[2].Baud);
        Assert.Empty(ports[3].Names);
        Assert.Empty(SerialPortConfiguration.Discover([]));
    }

    [Theory]
    [InlineData(1, "RCIN", null, false)]
    [InlineData(2, "RCIN", null, true)]
    [InlineData(2, "RCIN", 0, true)]
    [InlineData(2, "RCIN", 1024, true)]
    [InlineData(2, "MAVLink2", 0, false)]
    [InlineData(2, "GPS", 0, false)]
    public void DiagnosticsUseMetadataLabelsAndReceiverSupport(int count, string label, int? support, bool warning)
    {
        // Deliberately use a nonstandard protocol number to prove diagnostics are metadata-driven.
        var fields = Enumerable.Range(1, count).Select(index => Field($"SERIAL{index}_PROTOCOL", 77,
            ParameterFieldMetadata.Empty with { Options = [new ParameterValueOption(77, label)] })).ToList();
        if (support.HasValue)
        {
            fields.Add(Field("RC_OPTIONS", support.Value, ParameterFieldMetadata.Empty with
            {
                Bitmask = [new ParameterBitOption(10, "Multiple Receiver Support")]
            }));
        }
        var diagnostic = SerialPortConfiguration.Diagnose(fields);
        Assert.Equal(warning, diagnostic.Length > 0);
        Assert.Equal(warning && support == 0, diagnostic.Contains("support is not enabled"));
    }

    [Fact]
    public void ModuleIncludesOptionsAndPortsAboveEight()
    {
        var parameters = new Dictionary<string, VehicleParameter>
        {
            ["SERIAL12_OPTIONS"] = new("SERIAL12_OPTIONS", 128, MavParamType.Int32, 0, 1)
        };
        var module = new SerialPortsModule();
        Assert.True(module.IsAvailable(parameters));
        Assert.Equal("SERIAL12_OPTIONS", Assert.Single(module.Build(parameters,
            new Dictionary<string, ParameterMetadata>()).Settings).Name);
    }

    private static ParameterEditField Field(string name, double value, ParameterFieldMetadata metadata) =>
        new(name, MavParamType.Int32, value, value, value, metadata, null);
}
