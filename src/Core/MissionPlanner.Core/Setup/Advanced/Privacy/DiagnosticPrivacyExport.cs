using System.Globalization;
using System.Text;
using MissionPlanner.Core.Analysis;
using MissionPlanner.Core.Replay;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Setup.Advanced.Privacy;

/// <summary>Summarizes a deliberately reduced diagnostic export without exposing the source path or identity.</summary>
public sealed record DiagnosticPrivacyResult(long Rows, long ExaminedRecords, long ExcludedRecords);

/// <summary>Exports only approved numeric inertial/actuator diagnostics as CSV, never raw log records.</summary>
public sealed class DiagnosticPrivacyExport(IMavLinkMessageDefinitionRegistry definitions, IMavLinkMessageDecodeHandler decoder)
{
    /// <summary>Gets the input size bound for the indexed telemetry reader and offline export.</summary>
    public const long MaximumInputBytes = 256L * 1024 * 1024;
    private const long MaximumOutputBytes = 128L * 1024 * 1024;

    /// <summary>Writes policy-version-1 CSV with relative time, source ordinals and an allowlist of numeric fields.</summary>
    public async Task<DiagnosticPrivacyResult> ExportAsync(Stream input, string extension, Stream output, CancellationToken token)
    {
        if (!input.CanRead || !input.CanSeek || input.Length > MaximumInputBytes || !output.CanWrite || !output.CanSeek)
            throw new ArgumentException("Export requires a seekable input up to 256 MiB and a seekable writable output.");
        if (ReferenceEquals(input, output)) throw new ArgumentException("Export must use a separate destination.");
        extension = extension.ToLowerInvariant();
        if (extension is not (".tlog" or ".bin" or ".log")) throw new ArgumentException("Select a telemetry .tlog or DataFlash .bin/.log file.");
        input.Position = 0;
        long rows = 0, examined = 0, excluded = 0;
        double? origin = null;
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), 4096, leaveOpen: true);
        await writer.WriteLineAsync("elapsed_seconds,source,message,field,value".AsMemory(), token).ConfigureAwait(false);
        if (extension == ".tlog")
        {
            var reader = new TelemetryLogReader();
            var index = await reader.IndexAsync(input, "input.tlog", token).ConfigureAwait(false);
            if (index.Entries.Count > 2_000_000) throw new InvalidDataException("Telemetry log exceeds two million records.");
            var parser = new MavLinkV2FrameParser(definitions);
            var identities = new Dictionary<(byte, byte), int>();
            foreach (var entry in index.Entries)
            {
                token.ThrowIfCancellationRequested();
                examined++;
                var record = await reader.ReadAsync(input, entry, token).ConfigureAwait(false);
                parser.Reset();
                var frames = parser.Parse(record.Packet.Span, new TransportEndPoint("privacy-export"), entry.Timestamp);
                if (frames.Count != 1) { excluded++; continue; }
                var frame = frames[0];
                if (frame.MessageId is not (27 or 30) || !decoder.TryDecode(frame, out var message)) { excluded++; continue; }
                var fields = message switch
                {
                    AttitudeMessage a => new Dictionary<string, double> { ["roll_rad"] = a.Roll, ["pitch_rad"] = a.Pitch, ["yaw_rad"] = a.Yaw },
                    RawImuMessage a => new Dictionary<string, double> { ["acc_x_raw"] = a.XAcc, ["acc_y_raw"] = a.YAcc, ["acc_z_raw"] = a.ZAcc, ["gyro_x_raw"] = a.XGyro, ["gyro_y_raw"] = a.YGyro, ["gyro_z_raw"] = a.ZGyro },
                    _ => []
                };
                if (fields.Count == 0) { excluded++; continue; }
                if (!identities.TryGetValue((frame.SystemId, frame.ComponentId), out var identity))
                    identities[(frame.SystemId, frame.ComponentId)] = identity = identities.Count + 1;
                await WriteAsync(entry.Timestamp.ToUnixTimeMilliseconds() / 1000d, "S" + identity.ToString(CultureInfo.InvariantCulture),
                    message is AttitudeMessage ? "ATTITUDE" : "RAW_IMU", fields).ConfigureAwait(false);
            }
        }
        else
        {
            foreach (var record in new DataFlashRecordReader().Read(input, extension == ".bin", token))
            {
                examined++;
                var name = record.Name;
                var family = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                var allowed = family switch
                {
                    "IMU" => new[] { "AccX", "AccY", "AccZ", "GyrX", "GyrY", "GyrZ", "Temp" },
                    "ACC" => ["AccX", "AccY", "AccZ"],
                    "GYR" => ["GyrX", "GyrY", "GyrZ"],
                    "RPM" => ["rpm1", "rpm2", "RPM1", "RPM2"],
                    "RCOU" => Enumerable.Range(1, 16).Select(i => "C" + i.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    _ => []
                };
                var time = record.Number("TimeUS") / 1e6;
                if (!double.IsFinite(time)) time = record.Number("TimeMS") / 1e3;
                if (!double.IsFinite(time) || allowed.Length == 0 || name.Length > 6) { excluded++; continue; }
                var fields = allowed.Where(record.Fields.ContainsKey).ToDictionary(key => key, key => record.Number(key));
                await WriteAsync(time, "S1", name, fields).ConfigureAwait(false);
            }
        }
        await writer.FlushAsync(token).ConfigureAwait(false);
        return new(rows, examined, excluded);

        async Task WriteAsync(double time, string source, string message, IReadOnlyDictionary<string, double> fields)
        {
            origin ??= time;
            foreach (var (field, value) in fields)
            {
                token.ThrowIfCancellationRequested();
                if (!double.IsFinite(value)) continue;
                if (++rows > 2_000_000 || output.Position > MaximumOutputBytes) throw new InvalidDataException("Reduced export exceeds the output limit; select a shorter input log.");
                var line = string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, time - origin.Value):0.000000},{source},{message},{field},{value:R}");
                await writer.WriteLineAsync(line.AsMemory(), token).ConfigureAwait(false);
            }
        }
    }
}
