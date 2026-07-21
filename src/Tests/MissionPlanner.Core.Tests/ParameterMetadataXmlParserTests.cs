using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Parameters.Metadata;

namespace MissionPlanner.Core.Tests;

/// <summary>
///  ParameterMetadataXmlParserTests
/// </summary>
public sealed class ParameterMetadataXmlParserTests
{
    /// <summary>
    /// ParseAsync_LoadsVehicleAndLibraryParameters
    /// </summary>
    [Fact]
    public async Task ParseAsync_LoadsVehicleAndLibraryParameters()
    {
        const string xml = """
                           <paramfile>
                             <vehicles>
                               <parameters name="ArduCopter">
                                 <param name="ArduCopter:FRAME_TYPE" humanName="Frame Type" documentation="Vehicle parameter" />
                               </parameters>
                               <parameters name="ArduPlane">
                                 <param name="ArduPlane:PLANE_ONLY" humanName="Plane Only" />
                               </parameters>
                             </vehicles>
                             <libraries>
                               <parameters name="AP_BattMonitor">
                                 <param name="BATT_MONITOR" humanName="Battery Monitor" documentation="Library parameter" />
                               </parameters>
                               <parameters name="AP_GPS">
                                 <param name="GPS_TYPE" humanName="GPS Type" />
                               </parameters>
                             </libraries>
                           </paramfile>
                           """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var parser = new ParameterMetadataXmlParser(NullLogger<ParameterMetadataXmlParser>.Instance);

        var result = await parser.ParseAsync(stream, VehicleType.ArduCopter);

        result.Keys.Should().BeEquivalentTo("FRAME_TYPE", "BATT_MONITOR", "GPS_TYPE");
        result.Should().NotContainKey("PLANE_ONLY");
        result["FRAME_TYPE"].DisplayName.Should().Be("Frame Type");
        result["BATT_MONITOR"].Description.Should().Be("Library parameter");
    }

    /// <summary>
    /// ParseAsync_VehicleMetadataOverridesDuplicateLibraryMetadata
    /// </summary>
    [Fact]
    public async Task ParseAsync_VehicleMetadataOverridesDuplicateLibraryMetadata()
    {
        const string xml = """
                           <paramfile>
                             <vehicles>
                               <parameters name="ArduCopter">
                                 <param name="ArduCopter:SHARED_PARAM" humanName="Vehicle Definition" />
                               </parameters>
                             </vehicles>
                             <libraries>
                               <parameters name="SomeLibrary">
                                 <param name="SHARED_PARAM" humanName="Library Definition" />
                               </parameters>
                             </libraries>
                           </paramfile>
                           """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var parser = new ParameterMetadataXmlParser(NullLogger<ParameterMetadataXmlParser>.Instance);

        var result = await parser.ParseAsync(stream, VehicleType.ArduCopter);

        result.Should().ContainSingle();
        result["SHARED_PARAM"].DisplayName.Should().Be("Vehicle Definition");
    }

    /// <summary>
    /// ParseAsync_LoadsLibrariesWhenVehicleSectionIsMissing
    /// </summary>
    [Fact]
    public async Task ParseAsync_LoadsLibrariesWhenVehicleSectionIsMissing()
    {
        const string xml = """
                           <paramfile>
                             <vehicles />
                             <libraries>
                               <parameters name="AP_SerialManager">
                                 <param name="SERIAL1_PROTOCOL" humanName="Serial Protocol" />
                               </parameters>
                             </libraries>
                           </paramfile>
                           """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var parser = new ParameterMetadataXmlParser(NullLogger<ParameterMetadataXmlParser>.Instance);

        var result = await parser.ParseAsync(stream, VehicleType.ArduCopter);

        result.Should().ContainKey("SERIAL1_PROTOCOL");
    }
}
