using System.Buffers.Binary;
using FluentAssertions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Tests for the HOME_POSITION decoding and the distance helpers behind the Quick tab readouts.
/// </summary>
public class HomePositionAndDistanceTests
{
    private static readonly TransportEndPoint EndPoint = new("test", "test", 0);

    /// <summary>
    /// Decodes a HOME_POSITION payload into home coordinates.
    /// </summary>
    [Fact]
    public void Should_Decode_Home_Position_Message()
    {
        var payload = new byte[52];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0), 551234567);  // lat 55.1234567
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), 107654321);  // lon 10.7654321
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8), 42_000);     // alt 42 m (mm)

        var frame = new MavLinkFrame(1, 1, EndPoint, MessageIds.HomePosition, 0, payload, ReadOnlyMemory<byte>.Empty, DateTimeOffset.UtcNow);

        var decoded = new HomePositionMessageDecoder().TryDecode(frame, out var message);

        decoded.Should().BeTrue();
        var home = message.Should().BeOfType<HomePositionMessage>().Subject;
        home.LatitudeDegrees.Should().BeApproximately(55.1234567, 1e-7);
        home.LongitudeDegrees.Should().BeApproximately(10.7654321, 1e-7);
        home.AltitudeMslMeters.Should().BeApproximately(42.0, 1e-3);
    }

    /// <summary>
    /// Rejects payloads that do not carry the required lat/lon/alt fields.
    /// </summary>
    [Fact]
    public void Should_Reject_Too_Short_Home_Position_Payload()
    {
        var frame = new MavLinkFrame(1, 1, EndPoint, MessageIds.HomePosition, 0, new byte[8], ReadOnlyMemory<byte>.Empty, DateTimeOffset.UtcNow);

        new HomePositionMessageDecoder().TryDecode(frame, out var message).Should().BeFalse();
        message.Should().BeNull();
    }

    /// <summary>
    /// NAV_CONTROLLER_OUTPUT with a MAVLink v2 zero-truncated payload must still decode,
    /// with the missing trailing fields (including wp_dist) read as zero.
    /// </summary>
    [Fact]
    public void Should_Decode_Truncated_Nav_Controller_Output()
    {
        // Full payload is 26 bytes; v2 truncation strips trailing zeros (wp_dist = 0).
        var payload = new byte[20];
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(0), 1.5f);  // nav_roll
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(4), -2.5f); // nav_pitch

        var frame = new MavLinkFrame(1, 1, EndPoint, MessageIds.NavControllerOutput, 0, payload, ReadOnlyMemory<byte>.Empty, DateTimeOffset.UtcNow);

        var decoded = new NavControllerOutputMessageDecoder().TryDecode(frame, out var message);

        decoded.Should().BeTrue();
        var nav = message.Should().BeOfType<NavControllerOutputMessage>().Subject;
        nav.NavRoll.Should().BeApproximately(1.5f, 1e-6f);
        nav.NavPitch.Should().BeApproximately(-2.5f, 1e-6f);
        nav.DistanceToWaypoint.Should().Be(0);
    }

    /// <summary>
    /// The equirectangular distance approximation matches known distances.
    /// </summary>
    [Theory]
    [InlineData(55.0, 10.0, 55.0, 10.0, 0.0, 0.1)]              // same point
    [InlineData(55.0, 10.0, 55.009, 10.0, 1001.9, 5.0)]         // ~1 km north
    [InlineData(55.0, 10.0, 55.0, 10.0156, 996.0, 10.0)]        // ~1 km east at 55°N (cos scaling)
    public void Should_Approximate_Ground_Distance(double latA, double lonA, double latB, double lonB, double expectedMeters, double tolerance)
    {
        GeoMath.ApproximateDistanceMeters(latA, lonA, latB, lonB)
            .Should().BeApproximately(expectedMeters, tolerance);
    }
}
