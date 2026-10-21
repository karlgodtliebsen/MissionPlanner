using FluentAssertions;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Tests.Missions;

public sealed class MissionCommandCatalogTests
{
    [Fact]
    public void Commands_CoverTheEditorSet_InDisplayOrder()
    {
        MissionCommandCatalog.CommandNames.Should().Equal(
            "WAYPOINT", "LOITER_UNLIM", "LOITER_TURNS", "LOITER_TIME", "RETURN_TO_LAUNCH", "LAND",
            "TAKEOFF", "SPLINE_WAYPOINT", "DO_JUMP", "DO_CHANGE_SPEED", "DO_SET_ROI_LOCATION");
    }

    [Fact]
    public void Commands_UseTheDomainEnumValues()
    {
        MissionCommandCatalog.Commands.Should().OnlyContain(command => command.Id == (ushort)command.Command);
        MissionCommandCatalog.Commands.Select(command => command.Id).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData((ushort)16, "WAYPOINT")]
    [InlineData((ushort)22, "TAKEOFF")]
    [InlineData((ushort)177, "DO_JUMP")]
    [InlineData((ushort)195, "DO_SET_ROI_LOCATION")]
    public void CommandNameFor_KnownId_ReturnsName(ushort id, string name)
    {
        MissionCommandCatalog.CommandNameFor(id).Should().Be(name);
        MissionCommandCatalog.CommandIdFor(name).Should().Be(id);
    }

    [Fact]
    public void CommandNameFor_UnknownId_FallsBackToNumericLabel()
    {
        MissionCommandCatalog.CommandNameFor(999).Should().Be("ID 999");
        MissionCommandCatalog.CommandIdFor("NOT_A_COMMAND").Should().BeNull();
        MissionCommandCatalog.CommandIdFor(null).Should().BeNull();
    }

    [Theory]
    [InlineData((byte)0, "Absolute")]
    [InlineData((byte)2, "Mission")]
    [InlineData((byte)3, "Relative")]
    [InlineData((byte)10, "Terrain")]
    public void FrameNameFor_KnownId_ReturnsName(byte id, string name)
    {
        MissionCommandCatalog.FrameNameFor(id).Should().Be(name);
        MissionCommandCatalog.FrameIdFor(name).Should().Be(id);
    }

    [Fact]
    public void FrameNameFor_UnknownId_FallsBackToNumber()
    {
        MissionCommandCatalog.FrameNameFor(11).Should().Be("11");
        MissionCommandCatalog.FrameIdFor("Nope").Should().BeNull();
    }

    [Fact]
    public void Frames_UseTheDomainEnumValues()
    {
        MissionCommandCatalog.Frames.Should().OnlyContain(frame => frame.Id == (byte)frame.Frame);
    }

    [Fact]
    public void ItemGeometry_ReadsPositionAndAltitude_OfPositionedItems()
    {
        var altitude = new MissionAltitude(50, MissionAltitudeReference.Home);
        var item = new WaypointMissionItem(MissionItemId.New(), 0, new GeoPosition(56, 10), altitude, TimeSpan.Zero, 10);

        MissionItemGeometry.PositionOf(item).Should().Be(new GeoPosition(56, 10));
        MissionItemGeometry.AltitudeOf(item).Should().Be(altitude);
    }

    [Fact]
    public void ItemGeometry_ReturnsNull_ForItemsWithoutPosition()
    {
        var item = new ReturnToLaunchMissionItem(MissionItemId.New(), 0);

        MissionItemGeometry.PositionOf(item).Should().BeNull();
        MissionItemGeometry.AltitudeOf(item).Should().BeNull();
    }
}
