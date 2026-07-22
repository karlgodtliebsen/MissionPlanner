using System.Reflection;
using FluentAssertions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Generator;
using DomainCapability = MissionPlanner.Core.Vehicles.Models.MavProtocolCapability;
using ProtocolCapability = MissionPlanner.MavLink.Generated.MavProtocolCapability;
using ProtocolGpsFixType = MissionPlanner.MavLink.Generated.GpsFixType;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Validates generated MAVLink protocol enums, flags, commands, and domain mappings.
/// </summary>
public sealed class MavLinkGeneratedEnumTests
{
    private const string SourceRevision = "de1e078a3a7c53c9262a95b7417959a0f8bf4150";

    /// <summary>Verifies representative values and storage types from the official XML.</summary>
    [Fact]
    public void RepresentativeProtocolValuesMatchOfficialDialect()
    {
        ((byte)MavType.FixedWing).Should().Be(1);
        ((byte)MavAutopilot.ArduPilotMega).Should().Be(3);
        ((byte)MavComponent.MissionPlanner).Should().Be(190);
        ((byte)MavState.Active).Should().Be(4);
        ((byte)ProtocolGpsFixType.Value3dFix).Should().Be(3);
        ((byte)FirmwareVersionType.Official).Should().Be(255);
        ((byte)MavMissionResult.Accepted).Should().Be(0);
        ((byte)MavMissionType.Rally).Should().Be(2);
        Enum.GetUnderlyingType(typeof(ProtocolCapability)).Should().Be(typeof(ulong));
        Enum.GetUnderlyingType(typeof(MavCmd)).Should().Be(typeof(ushort));
    }

    /// <summary>Verifies only XML bitmasks receive flags semantics and combine numerically.</summary>
    [Fact]
    public void GeneratedBitmasksCombineAsFlags()
    {
        typeof(MavModeFlag).GetCustomAttribute<FlagsAttribute>().Should().NotBeNull();
        typeof(MavResult).GetCustomAttribute<FlagsAttribute>().Should().BeNull();

        var mode = MavModeFlag.CustomModeEnabled | MavModeFlag.GuidedEnabled | MavModeFlag.SafetyArmed;
        ((byte)mode).Should().Be(137);
        mode.HasFlag(MavModeFlag.GuidedEnabled).Should().BeTrue();
    }

    /// <summary>Verifies undefined protocol enum values survive a wire-value round trip.</summary>
    [Fact]
    public void UnknownNumericEnumValueSurvivesRoundTrip()
    {
        const byte futureWireValue = 250;
        var decoded = (MavResult)futureWireValue;

        Enum.IsDefined(decoded).Should().BeFalse();
        var encoded = (byte)decoded;
        var decodedAgain = (MavResult)encoded;

        encoded.Should().Be(futureWireValue);
        decodedAgain.Should().Be(decoded);
    }

    /// <summary>Verifies command facades retain existing command and workflow values.</summary>
    [Fact]
    public void CommandConstantsDelegateToGeneratedMavCmd()
    {
        MavLinkCommandIds.DoSetMode.Should().Be(176);
        MavLinkCommandIds.ComponentArmDisarm.Should().Be(400);
        MavLinkCommandIds.GetHomePosition.Should().Be(410);
        MavLinkCommandIds.RequestMessage.Should().Be(512);
        ((ushort)MavCmd.DoSetParameter).Should().Be(180);
        ((ushort)MavCmd.DoSetMissionCurrent).Should().Be(224);
        ((ushort)MavCmd.MissionStart).Should().Be(300);
    }

    /// <summary>Verifies generated protocol concepts map explicitly into smaller domain concepts.</summary>
    [Fact]
    public void GeneratedProtocolEnumsMapToDomainConcepts()
    {
        VehicleFirmwareIdentityFactory.MapFamily(MavType.Airship, MavAutopilot.ArduPilotMega)
            .Should().Be(FirmwareFamily.Blimp);
        VehicleFirmwareIdentityFactory.MapFamily(MavType.Quadrotor, MavAutopilot.Px4)
            .Should().Be(FirmwareFamily.Unknown);

        var protocol = ProtocolCapability.ParamFloat | ProtocolCapability.ParamEncodeBytewise | ProtocolCapability.Ftp;
        protocol.ToDomainCapabilities().Should().Be(
            DomainCapability.ParamFloat | DomainCapability.ParamUnion | DomainCapability.Ftp);
    }

    /// <summary>Verifies the committed enum source exactly matches deterministic generation.</summary>
    [Fact]
    public void GeneratedEnumSourceIsCurrent()
    {
        var definitions = MavLinkDialectEnumLoader.Load(Path.Combine(
            RepositoryRoot(),
            "src",
            "Core",
            "MissionPlanner.MavLink",
            "Dialects",
            "ardupilotmega.xml"));
        var expected = MavLinkEnumSourceGenerator.Generate(definitions, SourceRevision);
        var generatedPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Core",
            "MissionPlanner.MavLink",
            "Generated",
            "MavLinkEnums.g.cs");

        definitions.Should().HaveCount(221);
        definitions.Select(definition => definition.Name).Should().OnlyHaveUniqueItems();
        File.ReadAllText(generatedPath).Should().Be(expected);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "MissionPlanner.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the MissionPlanner repository root.");
    }
}
