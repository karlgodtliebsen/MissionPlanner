using System.Collections.Immutable;
using MissionPlanner.Analysis.Vibration;

namespace MissionPlanner.Core.Analysis;

/// <summary>Converts DataFlash records to uniform numerical segments without leaking log types into DSP.</summary>
public sealed class DataFlashImuSampleProvider(DataFlashRecordReader reader)
{
    private sealed class Batch(DataFlashRecord header)
    {
        internal DataFlashRecord Header { get; } = header;
        internal List<double>[] Axes { get; } = [[], [], []];
        internal int NextSequence { get; set; }
        internal bool Invalid { get; set; }
    }

    private sealed class Regular(int instance, string signal, VibrationAxis axis)
    {
        internal int Instance { get; } = instance;
        internal string Signal { get; } = signal;
        internal VibrationAxis Axis { get; } = axis;
        internal List<(double Time, double Value)> Samples { get; } = [];
    }

    /// <summary>Reads an existing artifact; never downloads a log or owns the supplied stream.</summary>
    /// <param name="stream">Readable binary .bin or FMT-based text .log stream.</param>
    /// <param name="name">Artifact display name.</param>
    /// <param name="binary">True for DataFlash binary, false for text export.</param>
    /// <param name="cancellationToken">Cancellation observed per record.</param>
    /// <returns>Uniform sample segments and quality diagnostics.</returns>
    public ImuLogData Read(Stream stream, string name, bool binary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.CanSeek && stream.Length - stream.Position > 256L * 1024 * 1024)
        {
            throw new InvalidDataException("Select a log or exported interval no larger than 256 MiB.");
        }
        var series = ImmutableArray.CreateBuilder<ImuSampleSeries>();
        var motors = ImmutableArray.CreateBuilder<MotorLogSample>();
        var diagnostics = new HashSet<string>(StringComparer.Ordinal);
        var batches = new Dictionary<int, Batch>();
        var regular = new Dictionary<string, Regular>();
        var totalValues = 0;
        var pendingValues = 0;

        void AddSeries(int instance, string signal, VibrationAxis axis, double rate, double start, IEnumerable<double> values, int? batch)
        {
            var samples = values.ToImmutableArray();
            totalValues += samples.Length;
            if (totalValues > 6_000_000 || series.Count >= 20000)
            {
                throw new InvalidDataException("Analysis sample limit exceeded; export a shorter log interval.");
            }
            if (samples.Length < 4)
            {
                diagnostics.Add("Segments shorter than four samples were discarded.");
                return;
            }
            series.Add(new ImuSampleSeries($"{series.Count}:{instance}:{signal}:{axis}", instance, signal, axis,
                signal == "Gyro" ? "rad/s" : "m/s²", rate, start, samples, batch));
        }

        void FlushBatch(int number, Batch batch)
        {
            pendingValues -= batch.Axes.Sum(a => a.Count);
            var header = batch.Header;
            // Older ISBH formats omit smp_cnt; their next header ends the batch.
            var count = header.Number("smp_cnt", batch.Axes[0].Count);
            var rate = header.Number("smp_rate");
            var multiplier = header.Number("mul");
            var start = header.Number("SampleUS") / 1e6;
            var instance = header.Number("instance");
            var type = header.Number("type");
            if (batch.Invalid || count != batch.Axes[0].Count || !double.IsFinite(rate) || rate <= 0 ||
                !double.IsFinite(multiplier) || multiplier <= 0 || !double.IsFinite(start) || start < 0 ||
                instance < 0 || instance > 255 || instance != Math.Truncate(instance) || type is not (0 or 1))
            {
                diagnostics.Add("Incomplete, out-of-order or invalid IMU batches were rejected.");
                return;
            }
            for (var axis = 0; axis < 3; axis++)
            {
                AddSeries((int)instance, type == 1 ? "Gyro" : "Accel", (VibrationAxis)axis, rate, start,
                    batch.Axes[axis].Select(v => v / multiplier), number);
            }
        }

        void FlushRegular(Regular state)
        {
            pendingValues -= state.Samples.Count;
            if (state.Samples.Count >= 4)
            {
                var rate = (state.Samples.Count - 1) / (state.Samples[^1].Time - state.Samples[0].Time);
                if (state.Samples.Select((p, i) => Math.Abs((p.Time - state.Samples[0].Time) * rate - i)).Max() <= 0.25)
                {
                    AddSeries(state.Instance, state.Signal, state.Axis, rate, state.Samples[0].Time, state.Samples.Select(p => p.Value), null);
                }
                else
                {
                    diagnostics.Add("Regular segments with cumulative timing drift over 0.25 sample were rejected; no implicit resampling was applied.");
                }
            }
            else if (state.Samples.Count > 0)
            {
                diagnostics.Add("Segments shorter than four samples were discarded.");
            }
            state.Samples.Clear();
        }

        foreach (var record in reader.Read(stream, binary, cancellationToken))
        {
            if (record.Name == "TRUNCATED")
            {
                diagnostics.Add("Truncated final DataFlash packet was discarded; incomplete IMU batches were rejected.");
                foreach (var batch in batches.Values)
                {
                    batch.Invalid = true;
                }
                continue;
            }
            if (record.Name == "ISBH")
            {
                foreach (var previousNumber in batches.Where(p => !p.Value.Header.Fields.ContainsKey("smp_cnt"))
                             .Select(p => p.Key).ToArray())
                {
                    FlushBatch(previousNumber, batches[previousNumber]);
                    batches.Remove(previousNumber);
                }
                var n = record.Number("N");
                if (!double.IsFinite(n) || n < 0 || n > 65535 || n != Math.Truncate(n))
                {
                    throw new InvalidDataException("Invalid batch identity.");
                }
                var number = (int)n;
                if (batches.Remove(number, out var previous))
                {
                    FlushBatch(number, previous);
                }
                if (batches.Count >= 1024)
                {
                    throw new InvalidDataException("Too many unfinished IMU batches.");
                }
                batches[number] = new Batch(record);
            }
            else if (record.Name == "ISBD")
            {
                if (!batches.TryGetValue((int)record.Number("N", -1), out var batch))
                {
                    diagnostics.Add("Batch data without a matching header was rejected.");
                    continue;
                }
                var arrays = new[] { "x", "y", "z" }.Select(key => record.Fields.GetValueOrDefault(key) as double[]).ToArray();
                var count = batch.Header.Number("smp_cnt", 65535);
                if (record.Number("seqno") != batch.NextSequence || arrays.Any(a => a is null || a.Length != 32 ||
                    a.Any(v => !double.IsFinite(v) || v < short.MinValue || v > short.MaxValue || v != Math.Truncate(v))) ||
                    count < 4 || count > 65535)
                {
                    batch.Invalid = true;
                }
                if (!batch.Invalid)
                {
                    var take = Math.Min(32, (int)count - batch.Axes[0].Count);
                    for (var axis = 0; axis < 3; axis++)
                    {
                        batch.Axes[axis].AddRange(arrays[axis]!.Take(take));
                        pendingValues += take;
                    }
                    batch.NextSequence++;
                    if (batch.Axes[0].Count == count)
                    {
                        var number = (int)record.Number("N");
                        FlushBatch(number, batch);
                        batches.Remove(number);
                    }
                }
            }
            else if (record.Name.StartsWith("IMU", StringComparison.Ordinal) || record.Name.StartsWith("ACC", StringComparison.Ordinal) || record.Name.StartsWith("GYR", StringComparison.Ordinal))
            {
                var suffix = record.Name.Length > 3 && int.TryParse(record.Name[3..], out var oldInstance) ? oldInstance - 1 : 0;
                var instanceValue = record.Number("I", suffix);
                if (instanceValue < 0 || instanceValue > 255 || instanceValue != Math.Truncate(instanceValue))
                {
                    diagnostics.Add("Invalid IMU instance was rejected.");
                    continue;
                }
                foreach (var signal in new[] { "Gyro", "Accel" })
                {
                    for (var axis = 0; axis < 3; axis++)
                    {
                        var field = (signal == "Gyro" ? "Gyr" : "Acc") + "XYZ"[axis];
                        if (!record.Fields.ContainsKey(field))
                        {
                            continue;
                        }
                        var key = $"{record.Name}:{instanceValue}:{signal}:{axis}";
                        if (!regular.TryGetValue(key, out var state))
                        {
                            state = new Regular((int)instanceValue, signal, (VibrationAxis)axis);
                            regular[key] = state;
                        }
                        var time = record.Number("SampleUS", record.Number("TimeUS")) / 1e6;
                        var value = record.Number(field);
                        if (!double.IsFinite(time) || time < 0 || !double.IsFinite(value))
                        {
                            FlushRegular(state);
                            diagnostics.Add("Non-finite IMU values or timestamps were rejected.");
                            continue;
                        }
                        if (state.Samples.Count > 0)
                        {
                            var delta = time - state.Samples[^1].Time;
                            var expected = state.Samples.Count > 1 ? state.Samples[1].Time - state.Samples[0].Time : delta;
                            if (delta <= 0 || Math.Abs(delta - expected) > Math.Max(2e-6, expected * 0.02))
                            {
                                FlushRegular(state);
                                diagnostics.Add("Gaps, duplicate timestamps or >2% interval jitter split regular IMU sequences.");
                            }
                        }
                        state.Samples.Add((time, value));
                        pendingValues++;
                        if (state.Samples.Count > 1_000_000)
                        {
                            throw new InvalidDataException("Regular segment exceeds one million samples; export a shorter interval.");
                        }
                    }
                }
            }
            else
            {
                var time = record.Number("TimeUS") / 1e6;
                if (!double.IsFinite(time) || time < 0)
                {
                    continue;
                }
                void AddMotor(int index, string source, double value, bool rpm)
                {
                    if (double.IsFinite(value) && value >= 0)
                    {
                        motors.Add(new MotorLogSample(index, source, time, value, rpm));
                        if (motors.Count > 1_000_000)
                        {
                            throw new InvalidDataException("Motor telemetry exceeds the analysis limit.");
                        }
                    }
                }
                if (record.Name.StartsWith("ESC", StringComparison.Ordinal))
                {
                    var index = record.Number("Instance", record.Number("I", -1));
                    if (index < 0 && int.TryParse(record.Name[3..], out var legacyIndex))
                    {
                        index = legacyIndex;
                    }
                    if (index >= 0)
                    {
                        AddMotor((int)index, "ESC RPM (logged index)", record.Number("RPM"), true);
                    }
                }
                else if (record.Name == "RPM")
                {
                    AddMotor(1, "RPM sensor (motor mapping unknown)", record.Number("rpm1"), true);
                    AddMotor(2, "RPM sensor (motor mapping unknown)", record.Number("rpm2"), true);
                }
                else if (record.Name == "RCOU")
                {
                    for (var channel = 1; channel <= 16; channel++)
                    {
                        AddMotor(channel, "Output PWM µs (not RPM; motor mapping unknown)", record.Number($"C{channel}"), false);
                    }
                }
            }
            if (pendingValues + totalValues > 6_000_000)
            {
                throw new InvalidDataException("Analysis sample limit exceeded; export a shorter interval.");
            }
        }
        foreach (var (number, batch) in batches)
        {
            FlushBatch(number, batch);
        }
        foreach (var state in regular.Values)
        {
            FlushRegular(state);
        }
        diagnostics.Add("Regular IMU values use logged physical units; batch values are divided by ISBH.mul. No gap interpolation is performed.");
        return new ImuLogData(name, series.ToImmutable(), motors.ToImmutable(), diagnostics.ToImmutableArray());
    }
}
