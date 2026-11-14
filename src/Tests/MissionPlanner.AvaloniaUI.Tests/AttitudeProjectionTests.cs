using System.Numerics;
using MissionPlanner.App.Views.FlightData.Hud;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class AttitudeProjectionTests
{
    [Fact]
    public void AerospaceAxesMatchNoseUpRightSideDownAndClockwiseYaw()
    {
        AssertVector(-Vector3.UnitZ, new AttitudeProjection(0, 90, 0).BodyToWorld(Vector3.UnitX));
        AssertVector(Vector3.UnitZ, new AttitudeProjection(90, 0, 0).BodyToWorld(Vector3.UnitY));
        AssertVector(Vector3.UnitY, new AttitudeProjection(0, 0, 90).BodyToWorld(Vector3.UnitX));
        Assert.True(AttitudeProjection.WorldToCamera(Vector3.UnitX).Y < 0); // Nose away is up on screen.
        Assert.True(AttitudeProjection.WorldToCamera(Vector3.UnitY).X > 0); // Right side stays screen right.
        Assert.True(AttitudeProjection.WorldToCamera(Vector3.UnitZ).Y > 0); // Down stays screen down.
    }

    [Theory]
    [InlineData(25, -17, 123)]
    [InlineData(180, 0, 270)]
    [InlineData(-90, 90, -179)]
    [InlineData(0, 0, 0)]
    public void CombinedAttitudeMatchesOrderedRotationsAndPreservesDimensions(float roll, float pitch, float yaw)
    {
        var point = new Vector3(.7f, -.8f, -.15f);
        const float radians = MathF.PI / 180;
        var expected = Vector3.Transform(point, Matrix4x4.CreateRotationX(roll * radians) *
            Matrix4x4.CreateRotationY(pitch * radians) * Matrix4x4.CreateRotationZ(yaw * radians));
        var actual = new AttitudeProjection(roll, pitch, yaw).BodyToWorld(point);
        AssertVector(expected, actual);
        Assert.Equal(point.Length(), actual.Length(), 5);
        AssertVector(actual, new AttitudeProjection(roll + 360, pitch - 360, yaw + 720).BodyToWorld(point));
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 5);
        Assert.Equal(expected.Y, actual.Y, 5);
        Assert.Equal(expected.Z, actual.Z, 5);
    }
}
