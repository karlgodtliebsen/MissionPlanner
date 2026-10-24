using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Receiver evidence from one directly owned serial connection.</summary>
public sealed record UbxBaseSnapshot(string Identity, string Status, string Mode, RtkSurveyStatus? Survey,
    string Fix, IReadOnlyList<RtkSatellite> Satellites, DateTimeOffset? SatellitesAt, long RtcmFrames,
    DateTimeOffset? LastCorrection, long ChecksumErrors, bool Ready);
/// <summary>A configuration review bound to one receiver session and its current time-mode payload.</summary>
public sealed record UbxBaseReview(Guid Session, string Identity, byte[] TimeMode);

/// <summary>Owns a u-blox TMODE3-capable receiver for configuration and diagnostic polling; forwarding is a separate session.</summary>
public sealed class UbxBaseSession(IDirectSerialSessionFactory ports)
{
    private Context? current;
    private int running;
    private int changing;
    /// <summary>Gets the latest session evidence, or null after disconnect.</summary>
    public UbxBaseSnapshot? Current => Volatile.Read(ref current)?.Snapshot;
    /// <summary>Captures receiver identity, connection generation and time mode before user confirmation.</summary>
    public UbxBaseReview Review()
    {
        var context = Volatile.Read(ref current);
        if (context is null || !context.Snapshot.Ready || context.Lifetime.IsCancellationRequested) throw new InvalidOperationException("Connect and identify a TMODE3-capable u-blox receiver first.");
        lock (context.Sync) return new(context.Id, context.Snapshot.Identity, context.TimeMode.ToArray());
    }
    /// <summary>Connects without changing receiver configuration; polls identity, current time mode, survey, fix and satellites until cancelled.</summary>
    public async Task RunAsync(string port, int baud, IProgress<UbxBaseSnapshot> progress, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(port) || baud is < 9600 or > 1000000) throw new ArgumentException("Choose a receiver port and its current baud rate (9600–1000000). No baud scan or baud change is performed.");
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) throw new InvalidOperationException("Disconnect the existing receiver first.");
        Context? context = null; Task? reader = null;
        try
        {
            using var open = CancellationTokenSource.CreateLinkedTokenSource(token); open.CancelAfter(TimeSpan.FromSeconds(5));
            var serial = await ports.OpenAsync(port, baud, open.Token).ConfigureAwait(false);
            context = new(serial, token); Volatile.Write(ref current, context);
            reader = ReadLoopAsync(context);
            var version = await RequestAsync(context, 0x0a, 4, [], packet => packet.Class == 0x0a && packet.Id == 4, context.Lifetime.Token).ConfigureAwait(false);
            if (version.Payload.Length < 40) throw new InvalidDataException("Invalid u-blox receiver identity.");
            var identity = Encoding.ASCII.GetString(version.Payload).Replace('\0', ' ').Trim();
            identity = new(identity.Where(character => !char.IsControl(character)).Take(500).ToArray());
            _ = await ReadModeAsync(context, context.Lifetime.Token).ConfigureAwait(false);
            var receiverPort = await RequestAsync(context, 6, 0, [], packet => packet.Class == 6 && packet.Id == 0, context.Lifetime.Token).ConfigureAwait(false);
            if (receiverPort.Payload.Length != 20 || receiverPort.Payload[0] > 4) throw new InvalidDataException("Receiver does not report a supported legacy UBX port configuration.");
            context.PortId = receiverPort.Payload[0];
            Update(context, snapshot => snapshot with { Identity = identity, Ready = true, Status = "Receiver identified. Monitoring only; no configuration has been written." });
            var cycle = 0;
            while (true)
            {
                context.Lifetime.Token.ThrowIfCancellationRequested();
                foreach (var id in new byte[] { 0x3B, 0x07, 0x35 }) await SendAsync(context, 1, id, [], context.Lifetime.Token).ConfigureAwait(false);
                if (++cycle % 10 == 0) _ = await ReadModeAsync(context, context.Lifetime.Token).ConfigureAwait(false);
                progress.Report(context.Snapshot);
                await Task.Delay(1000, context.Lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (context?.Fault is not null) { throw new IOException("Receiver read failed; the last configuration may still be active.", context.Fault); }
        finally
        {
            try
            {
                if (context is not null)
                {
                    context.Lifetime.Cancel();
                    if (reader is not null) await reader.ConfigureAwait(false);
                    // An in-flight configuration must finish cancellation before releasing its serial handle.
                    await context.Io.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    try { await context.Serial.DisposeAsync().ConfigureAwait(false); }
                    finally { context.Io.Release(); Volatile.Write(ref current, null); context.Lifetime.Dispose(); }
                }
            }
            finally { Volatile.Write(ref running, 0); }
        }
    }
    /// <summary>Starts a new survey (disabling a previous survey first), confirms readback and leaves it running in the receiver.</summary>
    public Task StartSurveyAsync(UbxBaseReview review, uint seconds, double accuracy, bool confirmed, CancellationToken token) =>
        SetModeAsync(review, UbxBaseProtocol.Survey(seconds, accuracy), confirmed, true, token);
    /// <summary>Sets a reviewed fixed antenna position in WGS84 with ellipsoid height, confirming TMODE3 readback.</summary>
    public Task SetFixedAsync(UbxBaseReview review, RtkBasePosition position, bool confirmed, CancellationToken token) =>
        SetModeAsync(review, UbxBaseProtocol.Fixed(position), confirmed, false, token);
    /// <summary>Disables time mode explicitly; disconnecting alone never disables an ongoing survey/base.</summary>
    public Task DisableAsync(UbxBaseReview review, bool confirmed, CancellationToken token) => SetModeAsync(review, new byte[40], confirmed, false, token);
    private async Task SetModeAsync(UbxBaseReview review, byte[] payload, bool confirmed, bool restart, CancellationToken token)
    {
        var context = Require(review, confirmed);
        if (Interlocked.CompareExchange(ref changing, 1, 0) != 0) throw new InvalidOperationException("Another receiver change is in progress.");
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, context.Lifetime.Token); lifetime.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            var previous = await ReadModeAsync(context, lifetime.Token).ConfigureAwait(false);
            if (!ModeEquals(previous, review.TimeMode)) throw new InvalidOperationException("Receiver time mode changed after review; read and confirm it again.");
            if (restart && previous[2] != 0) await WriteModeAsync(context, new byte[40], lifetime.Token).ConfigureAwait(false);
            await WriteModeAsync(context, payload, lifetime.Token).ConfigureAwait(false);
            Update(context, snapshot => snapshot with { Survey = null, Status = "Time-mode settings acknowledged and read back. Survey completion/position validity requires fresh diagnostics. Settings are RAM-only; no persistent save or reset was sent." });
        }
        catch { Update(context, snapshot => snapshot with { Status = "Configuration did not finish. Earlier settings may have changed; reconnect/read before trying again." }); throw; }
        finally { Volatile.Write(ref changing, 0); }
    }
    /// <summary>Enables base RTCM output on this receiver port, preserving other ports and existing message rates.</summary>
    public async Task EnableCorrectionsAsync(UbxBaseReview review, bool galileo, bool beidou, bool confirmed, CancellationToken token)
    {
        var context = Require(review, confirmed);
        if (Interlocked.CompareExchange(ref changing, 1, 0) != 0) throw new InvalidOperationException("Another receiver change is in progress.");
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, context.Lifetime.Token); lifetime.CancelAfter(TimeSpan.FromSeconds(40));
        try
        {
            var port = await RequestAsync(context, 6, 0, [], packet => packet.Class == 6 && packet.Id == 0, lifetime.Token).ConfigureAwait(false);
            if (port.Payload.Length != 20 || port.Payload[0] != context.PortId) throw new InvalidDataException("Receiver port changed.");
            var updated = port.Payload.ToArray(); var outputMask = BinaryPrimitives.ReadUInt16LittleEndian(updated.AsSpan(14));
            BinaryPrimitives.WriteUInt16LittleEndian(updated.AsSpan(14), (ushort)(outputMask | 0x21)); // Retain UBX and enable RTCM3 output only.
            await SetAsync(context, 0, updated, lifetime.Token).ConfigureAwait(false);
            var readback = await RequestAsync(context, 6, 0, [], packet => packet.Class == 6 && packet.Id == 0, lifetime.Token).ConfigureAwait(false);
            if (!readback.Payload.SequenceEqual(updated)) throw new IOException("Port configuration readback did not match; no baud change was requested.");
            var messages = new List<byte> { 0x05, 0x4A, 0x54, 0xE6 }; // RTCM 1005, 1074, 1084, 1230.
            if (galileo) messages.Add(0x5E); if (beidou) messages.Add(0x7C);
            foreach (var id in messages)
            {
                await SetAsync(context, 1, [0xF5, id, 1], lifetime.Token).ConfigureAwait(false);
                var rate = await RequestAsync(context, 6, 1, [0xF5, id], packet => packet.Class == 6 && packet.Id == 1 && packet.Payload.Length >= 3 && packet.Payload[0] == 0xF5 && packet.Payload[1] == id, lifetime.Token).ConfigureAwait(false);
                if (!(rate.Payload.Length == 3 && rate.Payload[2] == 1 || rate.Payload.Length == 8 && rate.Payload[2 + context.PortId] == 1)) throw new IOException("RTCM message-rate readback did not match.");
            }
            Update(context, snapshot => snapshot with { Status = "RTCM output settings confirmed on this port. Rates follow receiver navigation epochs. Corrections begin only when the receiver's base solution is valid. Disconnect monitoring before opening the same port for forwarding." });
        }
        catch { Update(context, snapshot => snapshot with { Status = "RTCM setup incomplete; earlier settings may remain applied. Read the receiver before retrying." }); throw; }
        finally { Volatile.Write(ref changing, 0); }
    }
    private Context Require(UbxBaseReview review, bool confirmed)
    {
        var context = Volatile.Read(ref current);
        if (!confirmed || context is null || context.Id != review.Session || !context.Snapshot.Ready || context.Lifetime.IsCancellationRequested) throw new InvalidOperationException("Confirm the currently connected receiver before changing it.");
        return context;
    }
    private static bool ModeEquals(byte[] a, byte[] b) => a.Length == 40 && b.Length == 40 && a.AsSpan(2, 17).SequenceEqual(b.AsSpan(2, 17)) && a.AsSpan(20, 12).SequenceEqual(b.AsSpan(20, 12));
    private static async Task<byte[]> ReadModeAsync(Context context, CancellationToken token)
    {
        var mode = await RequestAsync(context, 6, 0x71, [], packet => packet.Class == 6 && packet.Id == 0x71, token).ConfigureAwait(false);
        if (mode.Payload.Length != 40 || mode.Payload[0] != 0 || mode.Payload[2] > 2) throw new InvalidDataException("Receiver does not support this TMODE3 version.");
        lock (context.Sync) context.TimeMode = mode.Payload.ToArray();
        Update(context, snapshot => snapshot with { Mode = mode.Payload[2] switch { 0 => "Time mode disabled", 1 => "Survey-in configured", 2 => "Fixed base configured", _ => "Unknown" } });
        return mode.Payload;
    }
    private static async Task WriteModeAsync(Context context, byte[] payload, CancellationToken token)
    {
        await SetAsync(context, 0x71, payload, token).ConfigureAwait(false);
        if (!ModeEquals(payload, await ReadModeAsync(context, token).ConfigureAwait(false))) throw new IOException("Time-mode readback did not match the requested values.");
    }
    private static async Task SetAsync(Context context, byte id, byte[] payload, CancellationToken token)
    {
        var ack = await RequestAsync(context, 6, id, payload, packet => packet.Class == 5 && packet.Id is 0 or 1 && packet.Payload.Length == 2 && packet.Payload[0] == 6 && packet.Payload[1] == id, token).ConfigureAwait(false);
        if (ack.Id == 0) throw new NotSupportedException($"Receiver rejected UBX configuration 06/{id:X2}; no alternative protocol or automatic retry is used.");
    }
    private static async Task<UbxPacket> RequestAsync(Context context, byte messageClass, byte id, byte[] payload, Func<UbxPacket, bool> match, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, context.Lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(3));
        await context.Io.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var pending = new TaskCompletionSource<UbxPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (context.Sync) { context.Match = match; context.Pending = pending; }
            await context.Serial.WriteBytesAsync(UbxFramer.Encode(messageClass, id, payload), timeout.Token).ConfigureAwait(false);
            return await pending.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        finally { lock (context.Sync) { context.Match = null; context.Pending = null; } context.Io.Release(); }
    }
    private static async Task SendAsync(Context context, byte messageClass, byte id, byte[] payload, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, context.Lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(2));
        await context.Io.WaitAsync(timeout.Token).ConfigureAwait(false);
        try { await context.Serial.WriteBytesAsync(UbxFramer.Encode(messageClass, id, payload), timeout.Token).ConfigureAwait(false); }
        finally { context.Io.Release(); }
    }
    private static async Task ReadLoopAsync(Context context)
    {
        var ubx = new UbxFramer(); var rtcm = new Rtcm3Framer(); var buffer = new byte[2048];
        try
        {
            while (true)
            {
                using var read = CancellationTokenSource.CreateLinkedTokenSource(context.Lifetime.Token); read.CancelAfter(TimeSpan.FromSeconds(5));
                var count = await context.Serial.ReadAsync(buffer, read.Token).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException("The receiver closed the serial stream.");
                var now = DateTimeOffset.UtcNow;
                var corrections = rtcm.Push(buffer.AsSpan(0, count));
                if (corrections.Count > 0) Update(context, snapshot => snapshot with { RtcmFrames = snapshot.RtcmFrames + corrections.Count, LastCorrection = now });
                foreach (var packet in ubx.Push(buffer.AsSpan(0, count)))
                {
                    lock (context.Sync) { if (context.Match?.Invoke(packet) == true) context.Pending?.TrySetResult(packet); }
                    try
                    {
                        if (packet.Class == 1 && packet.Id == 0x3B) Update(context, snapshot => snapshot with { Survey = UbxBaseProtocol.DecodeSurvey(packet.Payload, now) });
                        if (packet.Class == 1 && packet.Id == 0x35) Update(context, snapshot => snapshot with { Satellites = UbxBaseProtocol.DecodeSatellites(packet.Payload), SatellitesAt = now });
                        if (packet.Class == 1 && packet.Id == 7 && packet.Payload.Length >= 92)
                        {
                            var bytes = packet.Payload;
                            var text = string.Create(CultureInfo.InvariantCulture, $"Fix type {bytes[20]}, valid {((bytes[21] & 1) != 0)}, {bytes[23]} satellites; lat {BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)) * 1e-7:F7}, lon {BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)) * 1e-7:F7}; ellipsoid {BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(32)) * .001:0.000} m; horizontal accuracy {BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(40)) * .001:0.000} m; received {now:HH:mm:ss} UTC.");
                            Update(context, snapshot => snapshot with { Fix = text });
                        }
                    }
                    catch (InvalidDataException) { /* Unknown diagnostic versions never become valid survey/fix evidence. */ }
                }
                Update(context, snapshot => snapshot with { ChecksumErrors = ubx.ChecksumErrors });
            }
        }
        catch (OperationCanceledException) when (context.Lifetime.IsCancellationRequested) { }
        catch (Exception ex) { context.Fault = ex; lock (context.Sync) context.Pending?.TrySetException(ex); context.Lifetime.Cancel(); }
    }
    private static void Update(Context context, Func<UbxBaseSnapshot, UbxBaseSnapshot> update) { lock (context.Sync) context.Snapshot = update(context.Snapshot); }
    private sealed class Context(IDirectSerialSession serial, CancellationToken token)
    {
        internal readonly Guid Id = Guid.NewGuid();
        internal readonly IDirectSerialSession Serial = serial;
        internal readonly CancellationTokenSource Lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        internal readonly SemaphoreSlim Io = new(1, 1);
        internal readonly object Sync = new();
        internal UbxBaseSnapshot Snapshot = new("Identifying…", "Opening receiver", "Unknown", null, "No fix received", [], null, 0, null, 0, false);
        internal byte[] TimeMode = [];
        internal byte PortId;
        internal TaskCompletionSource<UbxPacket>? Pending;
        internal Func<UbxPacket, bool>? Match;
        internal Exception? Fault;
    }
}
