using System.Globalization;
using System.Text;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Setup.Advanced.Position;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>The standalone serial tracker protocol, separate from an ArduPilot tracker vehicle.</summary>
public enum SerialTrackerDriver
{
    /// <summary>Pololu Maestro compact protocol, PWM in quarter microseconds.</summary>
    Maestro,
    /// <summary>Legacy ArduTracker ASCII PWM commands.</summary>
    ArduTracker,
    /// <summary>Legacy ASCII pan/tilt angles in tenths of a degree.</summary>
    DegreeTracker
}

/// <summary>Reviewed mechanical travel and PWM mapping for one tracker axis.</summary>
public sealed record TrackerAxisCalibration
{
    /// <summary>Gets the lowest permitted mechanical angle, in degrees.</summary>
    public double MinimumDegrees { get; init; } = -90;
    /// <summary>Gets the highest permitted mechanical angle, in degrees.</summary>
    public double MaximumDegrees { get; init; } = 90;
    /// <summary>Gets the PWM at the minimum angle before reversal.</summary>
    public int MinimumPwm { get; init; } = 1000;
    /// <summary>Gets the PWM at the maximum angle before reversal.</summary>
    public int MaximumPwm { get; init; } = 2000;
    /// <summary>Gets whether the output direction is reversed.</summary>
    public bool Reversed { get; init; }
    /// <summary>Validates a nonempty travel range and conservative servo pulse limits.</summary>
    public void Validate()
    {
        if (!double.IsFinite(MinimumDegrees) || !double.IsFinite(MaximumDegrees) || MinimumDegrees < -180 || MaximumDegrees > 180 || MinimumDegrees >= MaximumDegrees ||
            MinimumPwm < 800 || MaximumPwm > 2200 || MinimumPwm >= MaximumPwm) throw new ArgumentException("Review tracker travel (-180 to 180 degrees) and PWM limits (800–2200 μs), with minimum below maximum.");
    }
    /// <summary>Maps an angle to a clamped, optionally reversed PWM value.</summary>
    public int Pwm(double angle)
    {
        Validate();
        if (!double.IsFinite(angle)) throw new ArgumentException("Tracker angle must be finite.");
        var ratio = (Math.Clamp(angle, MinimumDegrees, MaximumDegrees) - MinimumDegrees) / (MaximumDegrees - MinimumDegrees);
        if (Reversed) ratio = 1 - ratio;
        return (int)Math.Round(MinimumPwm + ratio * (MaximumPwm - MinimumPwm));
    }
    /// <summary>Returns the bounded angle, reflecting within the calibrated interval when reversed.</summary>
    public double Angle(double angle)
    {
        Validate();
        if (!double.IsFinite(angle)) throw new ArgumentException("Tracker angle must be finite.");
        var bounded = Math.Clamp(angle, MinimumDegrees, MaximumDegrees);
        return Reversed ? MinimumDegrees + MaximumDegrees - bounded : bounded;
    }
}

/// <summary>Local calibration; restoring it never opens a port or enables motion.</summary>
public sealed record SerialTrackerProfile
{
    /// <summary>Gets the explicit serial protocol.</summary>
    public SerialTrackerDriver Driver { get; init; }
    /// <summary>Gets yaw travel and PWM calibration.</summary>
    public TrackerAxisCalibration Pan { get; init; } = new();
    /// <summary>Gets pitch travel and PWM calibration.</summary>
    public TrackerAxisCalibration Tilt { get; init; } = new() { MinimumDegrees = 0, MaximumDegrees = 90 };
    /// <summary>Gets the true bearing corresponding to mechanical pan zero.</summary>
    public double HeadingZero { get; init; }
    /// <summary>Gets the elevation correction in degrees.</summary>
    public double TiltOffset { get; init; }
    /// <summary>Gets the zero-based Maestro pan channel.</summary>
    public int PanChannel { get; init; }
    /// <summary>Gets the zero-based Maestro tilt channel.</summary>
    public int TiltChannel { get; init; } = 1;
    /// <summary>Gets the Maestro speed limit, in controller units; zero means unlimited.</summary>
    public int Speed { get; init; } = 20;
    /// <summary>Gets the Maestro acceleration limit, in controller units; zero means unlimited.</summary>
    public int Acceleration { get; init; } = 5;
    /// <summary>Validates calibration before any port is opened.</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(Driver) || Pan is null || Tilt is null || !double.IsFinite(HeadingZero) || HeadingZero is < 0 or >= 360 ||
            !double.IsFinite(TiltOffset) || Math.Abs(TiltOffset) > 90 || PanChannel is < 0 or > 23 || TiltChannel is < 0 or > 23 || PanChannel == TiltChannel ||
            Speed is < 0 or > 16383 || Acceleration is < 0 or > 255) throw new ArgumentException("Review tracker driver, heading, tilt correction, distinct channels, speed and acceleration.");
        Pan.Validate(); Tilt.Validate();
    }
}

/// <summary>Runs explicit standalone tracking with a captured aircraft and static tracker origin.</summary>
public sealed class SerialAntennaTracker(IDirectSerialSessionFactory ports, IActiveVehicleContext active)
{
    private int running;
    /// <summary>Points toward fresh vehicle GPS at 5 Hz until stopped or the connection/position becomes invalid.</summary>
    public Task TrackAsync(string port, int baud, SerialTrackerProfile profile, GeoPosition origin, double originAltitudeMsl,
        VehicleId aircraft, bool confirmed, IProgress<string> status, CancellationToken token) =>
        RunAsync(port, baud, profile, origin, originAltitudeMsl, aircraft, null, confirmed, status, token);

    /// <summary>Applies one calibrated mechanical position for at most two seconds, then closes the port.</summary>
    public Task TestAsync(string port, int baud, SerialTrackerProfile profile, double pan, double tilt, bool confirmed, IProgress<string> status, CancellationToken token) =>
        RunAsync(port, baud, profile, default, 0, null, (pan, tilt), confirmed, status, token);

    private async Task RunAsync(string port, int baud, SerialTrackerProfile profile, GeoPosition origin, double altitude,
        VehicleId? aircraft, (double Pan, double Tilt)? test, bool confirmed, IProgress<string> status, CancellationToken token)
    {
        profile.Validate();
        if (!confirmed || string.IsNullOrWhiteSpace(port) || baud is < 1200 or > 1000000 || !double.IsFinite(altitude) || altitude is < -1000 or > 20000 ||
            test is null && !origin.IsValid || test is { } angles && (!double.IsFinite(angles.Pan) || !double.IsFinite(angles.Tilt)))
            throw new ArgumentException("Confirm the port, calibrated travel and valid static tracker coordinates/altitude.");
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) throw new InvalidOperationException("Stop the current tracker operation first.");
        var boundary = active.ConnectionCancellationToken;
        IDirectSerialSession? session = null;
        var outputAttempted = false;
        try
        {
            if (aircraft is { } id) _ = Position(id, boundary);
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, aircraft.HasValue ? boundary : CancellationToken.None);
            using var open = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); open.CancelAfter(TimeSpan.FromSeconds(5));
            session = await ports.OpenAsync(port, baud, open.Token).ConfigureAwait(false);
            if (profile.Driver == SerialTrackerDriver.Maestro)
                foreach (var channel in new[] { profile.PanChannel, profile.TiltChannel })
                {
                    await WriteAsync(session, Maestro(0x87, channel, profile.Speed), lifetime.Token).ConfigureAwait(false);
                    await WriteAsync(session, Maestro(0x89, channel, profile.Acceleration), lifetime.Token).ConfigureAwait(false);
                }
            var lastPan = 0d;
            do
            {
                lifetime.Token.ThrowIfCancellationRequested();
                var (pan, tilt) = test ?? Pointing(origin, altitude, Position(aircraft!.Value, boundary), profile, lastPan);
                lastPan = pan;
                outputAttempted = true;
                await WriteAsync(session, Encode(profile, pan, tilt), lifetime.Token).ConfigureAwait(false);
                status.Report($"Requested pan {Math.Clamp(pan, profile.Pan.MinimumDegrees, profile.Pan.MaximumDegrees):0.0}°, tilt {Math.Clamp(tilt, profile.Tilt.MinimumDegrees, profile.Tilt.MaximumDegrees):0.0}°. Serial protocols have no movement acknowledgement.");
                await Task.Delay(test is null ? 200 : 2000, lifetime.Token).ConfigureAwait(false);
            } while (test is null);
        }
        finally
        {
            try
            {
                if (session is not null)
                {
                    try
                    {
                        if (profile.Driver == SerialTrackerDriver.Maestro && outputAttempted)
                        {
                            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                            byte[] disable = [.. Maestro(0x84, profile.PanChannel, 0), .. Maestro(0x84, profile.TiltChannel, 0)];
                            await session.WriteBytesAsync(disable, stop.Token).ConfigureAwait(false);
                            status.Report("Tracker stopped; sent Maestro pulse-disable. Mechanical stopping is not acknowledged.");
                        }
                        else if (outputAttempted) status.Report("Serial tracker stopped; this ASCII driver retains its last position and has no disable command. Isolate servo power if necessary.");
                    }
                    catch (Exception ex) { throw new IOException("Tracker cleanup could not confirm pulse-disable. Isolate servo power if necessary.", ex); }
                    finally { await session.DisposeAsync().ConfigureAwait(false); }
                }
            }
            finally { Volatile.Write(ref running, 0); }
        }
    }

    private (GeoPosition Position, double Altitude) Position(VehicleId id, CancellationToken boundary)
    {
        var state = active.State;
        var now = DateTimeOffset.UtcNow;
        if (!active.IsOnline || active.VehicleId != id || boundary != active.ConnectionCancellationToken || boundary.IsCancellationRequested || state is null ||
            state.Identity.Firmware.Family == FirmwareFamily.AntennaTracker || state.LastHeartbeatAt > now || now - state.LastHeartbeatAt > TimeSpan.FromSeconds(3) ||
            state.Gps.FixType is not (GpsFixType.Fix3D or GpsFixType.DifferentialGps or GpsFixType.RtkFloat or GpsFixType.RtkFixed) ||
            state.Gps.ObservedAt is not { } observed || observed > now || now - observed > TimeSpan.FromSeconds(3) ||
            state.Gps.LatitudeDegrees is not { } lat || state.Gps.LongitudeDegrees is not { } lon || !new GeoPosition(lat, lon).IsValid ||
            state.Position.AltitudeMslMeters is not { } msl || !double.IsFinite(msl) || state.Position.ObservedAt is not { } positionTime || positionTime > now || now - positionTime > TimeSpan.FromSeconds(3))
            throw new InvalidOperationException("The captured aircraft connection, GPS and MSL altitude must remain fresh (three seconds). Connect an aircraft for standalone tracking.");
        return (new(lat, lon), msl);
    }

    /// <summary>Computes true-bearing-relative pan and MSL-based elevation; near the origin, retains the previous pan.</summary>
    public static (double Pan, double Tilt) Pointing(GeoPosition origin, double altitude, (GeoPosition Position, double Altitude) target, SerialTrackerProfile profile, double previousPan)
    {
        profile.Validate();
        if (!double.IsFinite(altitude) || !double.IsFinite(target.Altitude) || !double.IsFinite(previousPan)) throw new ArgumentException("Finite altitudes and previous pan are required.");
        const double radians = Math.PI / 180;
        var distance = OperatorNavigationService.Distance(origin, target.Position);
        var delta = (target.Position.LongitudeDegrees - origin.LongitudeDegrees) * radians;
        var y = Math.Sin(delta) * Math.Cos(target.Position.LatitudeDegrees * radians);
        var x = Math.Cos(origin.LatitudeDegrees * radians) * Math.Sin(target.Position.LatitudeDegrees * radians) - Math.Sin(origin.LatitudeDegrees * radians) * Math.Cos(target.Position.LatitudeDegrees * radians) * Math.Cos(delta);
        var pan = distance < 1 ? previousPan : (Math.Atan2(y, x) / radians - profile.HeadingZero + 540) % 360 - 180;
        return (pan, Math.Atan2(target.Altitude - altitude, distance) / radians + profile.TiltOffset);
    }

    /// <summary>Encodes a bounded mechanical angle for the selected serial driver.</summary>
    public static byte[] Encode(SerialTrackerProfile profile, double pan, double tilt)
    {
        profile.Validate();
        var p = profile.Pan.Pwm(pan); var t = profile.Tilt.Pwm(tilt);
        if (profile.Driver == SerialTrackerDriver.Maestro) return [.. Maestro(0x84, profile.PanChannel, p * 4), .. Maestro(0x84, profile.TiltChannel, t * 4)];
        if (profile.Driver == SerialTrackerDriver.DegreeTracker) { p = (int)Math.Round(profile.Pan.Angle(pan) * 10); t = (int)Math.Round(profile.Tilt.Angle(tilt) * 10); }
        return Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"!!!PAN:{p:0000},TLT:{t:0000}\n"));
    }
    private static byte[] Maestro(byte command, int channel, int value) => [command, (byte)channel, (byte)(value & 127), (byte)((value >> 7) & 127)];
    private static async Task WriteAsync(IDirectSerialSession session, byte[] bytes, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(1));
        await session.WriteBytesAsync(bytes, timeout.Token).ConfigureAwait(false);
    }
}
