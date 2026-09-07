using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Setup.Advanced.Proximity;

/// <summary>Normalizes existing decoded range messages into a bounded horizontal body-frame snapshot.</summary>
public sealed class ProximityAggregator(TimeProvider clock, ProximityOptions options)
{
    private readonly object sync = new();
    private readonly Dictionary<string, IReadOnlyList<ProximityPoint>> sensors = [];
    private long malformed;
    private long unsupported;
    private long dropped;

    /// <summary>Accepts a promoted wire message; global arrays need a fresh clockwise heading from north.</summary>
    public void Observe(MavLinkMessage message, double? headingDegrees = null)
    {
        lock (sync)
        {
            switch (message)
            {
                case DistanceSensorMessage distance:
                    Distance(distance);
                    break;
                case ObstacleDistanceMessage obstacles:
                    Obstacles(obstacles, headingDegrees);
                    break;
            }
        }
    }

    /// <summary>Gets current freshness states and nearest valid obstacle without losing source identity.</summary>
    public ProximitySnapshot Snapshot(long queueDropped = 0)
    {
        if (options.StaleAfter <= TimeSpan.Zero || options.StaleAfter > TimeSpan.FromMinutes(1))
        {
            throw new InvalidOperationException("Proximity freshness must be greater than zero and at most one minute.");
        }
        lock (sync)
        {
            var now = clock.GetUtcNow();
            var points = sensors.OrderBy(item => item.Key, StringComparer.Ordinal).SelectMany(item => item.Value)
                .Select(point => point with
                {
                    AgeSeconds = Math.Max(0, (now - point.ObservedAt).TotalSeconds),
                    State = now < point.ObservedAt || now - point.ObservedAt > options.StaleAfter
                        ? ProximitySampleState.Stale : point.State
                }).ToArray();
            var nearest = points.Where(point => point.State == ProximitySampleState.Valid && point.BearingDegrees.HasValue)
                .OrderBy(point => point.DistanceMeters).ThenBy(point => point.Source, StringComparer.Ordinal).ThenBy(point => point.Index).FirstOrDefault();
            return new(points, nearest, malformed, unsupported, dropped + queueDropped);
        }
    }

    /// <summary>Clears all retained measurements and diagnostic counters.</summary>
    public void Clear()
    {
        lock (sync)
        {
            sensors.Clear();
            malformed = unsupported = dropped = 0;
        }
    }

    /// <summary>Normalizes a clockwise angle into [0, 360).</summary>
    public static double NormalizeAngle(double angle) => (angle % 360 + 360) % 360;

    private void Distance(DistanceSensorMessage message)
    {
        var source = $"{message.SystemId}/{message.ComponentId}/distance/{message.Id}";
        if (message.MaxDistance <= message.MinDistance || message.SignalQuality > 100
            || !float.IsFinite(message.HorizontalFov) || message.HorizontalFov < 0 || message.HorizontalFov > 2 * Math.PI)
        {
            malformed++;
            sensors.Remove(source);
            return;
        }
        double? bearing = message.Orientation <= 7 ? message.Orientation * 45d : null;
        if (message.Orientation == 12)
        {
            bearing = 180; // ROTATION_PITCH_180 points backwards.
        }
        if (message.Orientation == 100 && message.Quaternion is { Length: 4 } q && q.All(float.IsFinite))
        {
            var norm = Math.Sqrt(q.Sum(value => (double)value * value));
            if (norm > 0.001)
            {
                var w = q[0] / norm;
                var x = q[1] / norm;
                var y = q[2] / norm;
                var z = q[3] / norm;
                var vertical = 2 * (x * z - w * y);
                if (Math.Abs(vertical) < 0.01)
                {
                    bearing = NormalizeAngle(Math.Atan2(2 * (x * y + w * z), 1 - 2 * (y * y + z * z)) * 180 / Math.PI);
                }
            }
        }
        var state = message.SignalQuality == 1 || message.CurrentDistance == 0 ? ProximitySampleState.Unknown
            : message.CurrentDistance < message.MinDistance ? ProximitySampleState.TooClose
            : message.CurrentDistance > message.MaxDistance ? ProximitySampleState.OutOfRange : ProximitySampleState.Valid;
        if (!bearing.HasValue)
        {
            state = ProximitySampleState.Unsupported;
            unsupported++;
        }
        Store(source, [new(source, 0, message.Type.ToString(), $"Rotation {message.Orientation}", bearing,
            state == ProximitySampleState.Unknown ? null : message.CurrentDistance / 100d,
            message.MinDistance / 100d, message.MaxDistance / 100d, message.HorizontalFov * 180 / Math.PI,
            message.Covariance == 255 ? null : message.Covariance, message.SignalQuality == 0 ? null : message.SignalQuality,
            message.ReceivedAt, 0, state)]);
    }

    private void Obstacles(ObstacleDistanceMessage message, double? heading)
    {
        var source = $"{message.SystemId}/{message.ComponentId}/array/{message.SensorType}";
        var increment = message.IncrementF != 0 ? message.IncrementF : message.Increment;
        if (message.Distances is not { Length: 72 } || !float.IsFinite(increment) || increment == 0
            || Math.Abs(increment) > 360 || !float.IsFinite(message.AngleOffset) || message.MaxDistance <= message.MinDistance)
        {
            malformed++;
            sensors.Remove(source);
            return;
        }
        var frameSupported = message.Frame == 12 || message.Frame == 0 && heading.HasValue && double.IsFinite(heading.Value);
        if (!frameSupported)
        {
            unsupported++;
        }
        var offset = message.AngleOffset - (message.Frame == 0 && heading.HasValue ? heading.Value : 0);
        var count = (int)Math.Min(72d, Math.Ceiling(360d / Math.Abs(increment)));
        var points = new ProximityPoint[count];
        for (var index = 0; index < count; index++)
        {
            var value = message.Distances[index];
            var state = value == ushort.MaxValue ? ProximitySampleState.Unknown
                : value > message.MaxDistance ? ProximitySampleState.OutOfRange
                : value < message.MinDistance ? ProximitySampleState.TooClose : ProximitySampleState.Valid;
            points[index] = new(source, index, message.SensorType.ToString(), $"Frame {message.Frame}; offset {message.AngleOffset}; increment {increment}",
                frameSupported ? NormalizeAngle(offset + index * increment) : null,
                state == ProximitySampleState.Unknown ? null : value / 100d,
                message.MinDistance / 100d, message.MaxDistance / 100d, Math.Abs(increment), null, null,
                message.ReceivedAt, 0, frameSupported ? state : ProximitySampleState.Unsupported);
        }
        Store(source, points);
    }

    private void Store(string source, IReadOnlyList<ProximityPoint> points)
    {
        if (!sensors.ContainsKey(source) && sensors.Count >= 64)
        {
            dropped++;
            return;
        }
        if (sensors.TryGetValue(source, out var previous) && previous[0].ObservedAt > points[0].ObservedAt)
        {
            dropped++;
            return;
        }
        sensors[source] = points;
    }
}
