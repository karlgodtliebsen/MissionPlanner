using System.Numerics;

namespace MissionPlanner.App.Views.FlightData.Hud;

/// <summary>A complete attitude sample, in degrees, for the illustrative airframe.</summary>
public readonly record struct FrameAttitude(double Roll, double Pitch, double Yaw, bool HasAttitude, bool IsFresh);

/// <summary>
/// Projects forward/right/down body coordinates using aerospace roll, pitch and yaw.
/// The fixed camera looks toward the nose from behind and above the vehicle.
/// </summary>
public readonly struct AttitudeProjection
{
    private readonly double sr, cr, sp, cp, sy, cy;
    private const double CameraElevation = 28 * Math.PI / 180;

    /// <summary>Creates a body-to-world rotation; positive pitch is nose up, positive roll is right side down.</summary>
    public AttitudeProjection(double roll, double pitch, double yaw)
    {
        (sr, cr) = Math.SinCos(ToRadians(roll));
        (sp, cp) = Math.SinCos(ToRadians(pitch));
        (sy, cy) = Math.SinCos(ToRadians(yaw));
    }

    /// <summary>Applies roll, then pitch, then clockwise yaw in the north/east/down frame.</summary>
    public Vector3 BodyToWorld(Vector3 p) => new(
        (float)(cp * cy * p.X + (sr * sp * cy - cr * sy) * p.Y + (cr * sp * cy + sr * sy) * p.Z),
        (float)(cp * sy * p.X + (sr * sp * sy + cr * cy) * p.Y + (cr * sp * sy - sr * cy) * p.Z),
        (float)(-sp * p.X + sr * cp * p.Y + cr * cp * p.Z));

    /// <summary>Returns screen right/down and depth (larger values are farther from the camera).</summary>
    public static Vector3 WorldToCamera(Vector3 p) => new(p.Y,
        (float)(-Math.Sin(CameraElevation) * p.X + Math.Cos(CameraElevation) * p.Z),
        (float)(Math.Cos(CameraElevation) * p.X + Math.Sin(CameraElevation) * p.Z));

    private static double ToRadians(double degrees) => double.IsFinite(degrees)
        ? Math.IEEERemainder(degrees, 360) * Math.PI / 180 : 0;
}
