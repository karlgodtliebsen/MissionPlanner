using System.Net.Http.Headers;
using System.Text;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Setup.OptionalHardware;

public sealed class Rtcm3Framer
{
    private readonly List<byte> buffer = [];

    public IReadOnlyList<byte[]> Push(ReadOnlySpan<byte> bytes)
    {
        buffer.AddRange(bytes.ToArray());
        var frames = new List<byte[]>();
        while (buffer.Count >= 6)
        {
            var preamble = buffer.IndexOf(0xD3);
            if (preamble < 0) { buffer.Clear(); break; }
            if (preamble > 0) buffer.RemoveRange(0, preamble);
            if (buffer.Count < 6) break;
            var payloadLength = ((buffer[1] & 0x03) << 8) | buffer[2];
            var frameLength = payloadLength + 6;
            if (frameLength > 1029) { buffer.RemoveAt(0); continue; }
            if (buffer.Count < frameLength) break;
            var frame = buffer.Take(frameLength).ToArray();
            buffer.RemoveRange(0, frameLength);
            if (Crc24Q(frame.AsSpan(0, frameLength - 3)) == ((frame[^3] << 16) | (frame[^2] << 8) | frame[^1])) frames.Add(frame);
        }
        return frames;
    }

    public static int MessageType(ReadOnlySpan<byte> frame) => frame.Length >= 5 ? (frame[3] << 4) | (frame[4] >> 4) : 0;

    public static int Crc24Q(ReadOnlySpan<byte> data)
    {
        var crc = 0;
        foreach (var value in data)
        {
            crc ^= value << 16;
            for (var bit = 0; bit < 8; bit++) crc = (crc << 1) ^ ((crc & 0x1000000) != 0 ? 0x1864CFB : 0);
        }
        return crc & 0xFFFFFF;
    }
}

public sealed record RtcmFragment(byte Flags, byte[] Data);

public static class GpsRtcmFragmenter
{
    public const int PayloadSize = 180;
    public static IReadOnlyList<RtcmFragment> Fragment(ReadOnlySpan<byte> frame, byte sequence)
    {
        if (frame.Length > PayloadSize * 4) throw new ArgumentOutOfRangeException(nameof(frame), "GPS_RTCM_DATA supports at most four fragments.");
        if (frame.Length <= PayloadSize) return [new RtcmFragment(0, frame.ToArray())];
        var count = (frame.Length + PayloadSize - 1) / PayloadSize;
        var fragments = new List<RtcmFragment>(count);
        for (var index = 0; index < count; index++)
        {
            var length = Math.Min(PayloadSize, frame.Length - index * PayloadSize);
            var flags = (byte)(1 | (index << 1) | ((sequence & 0x1F) << 3));
            fragments.Add(new RtcmFragment(flags, frame.Slice(index * PayloadSize, length).ToArray()));
        }
        return fragments;
    }
}

public enum RtkSourceKind { Serial, Ntrip }
public sealed record RtkSourceOptions(RtkSourceKind Kind, string Endpoint, int PortOrBaud, string? MountPoint = null, string? Username = null, string? Password = null, bool UseTls = false);

public interface IRtkCorrectionSource : IAsyncDisposable
{
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}

public interface IRtkCorrectionSourceFactory
{
    Task<IRtkCorrectionSource> OpenAsync(RtkSourceOptions options, CancellationToken cancellationToken);
}

public sealed class RtkCorrectionSourceFactory(IDirectSerialSessionFactory serial, IHttpClientFactory clients) : IRtkCorrectionSourceFactory
{
    public async Task<IRtkCorrectionSource> OpenAsync(RtkSourceOptions options, CancellationToken cancellationToken)
    {
        if (options.Kind == RtkSourceKind.Serial)
            return new SerialSource(await serial.OpenAsync(options.Endpoint, options.PortOrBaud, cancellationToken).ConfigureAwait(false));

        var scheme = options.UseTls ? "https" : "http";
        var mount = (options.MountPoint ?? string.Empty).TrimStart('/');
        var request = new HttpRequestMessage(HttpMethod.Get, $"{scheme}://{options.Endpoint}:{options.PortOrBaud}/{mount}");
        request.Headers.UserAgent.ParseAdd("MissionPlanner-NextGen/1.0");
        request.Headers.TryAddWithoutValidation("Ntrip-Version", "Ntrip/2.0");
        if (!string.IsNullOrEmpty(options.Username))
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password ?? string.Empty}")));
        var response = await clients.CreateClient("RTK").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return new HttpSource(response, await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
    }

    private sealed class SerialSource(IDirectSerialSession session) : IRtkCorrectionSource
    {
        public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => session.ReadAsync(buffer, cancellationToken);
        public ValueTask DisposeAsync() => session.DisposeAsync();
    }

    private sealed class HttpSource(HttpResponseMessage response, Stream stream) : IRtkCorrectionSource
    {
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        public async ValueTask DisposeAsync() { await stream.DisposeAsync().ConfigureAwait(false); response.Dispose(); }
    }
}

public sealed record RtkInjectionSnapshot(bool IsRunning, string SourceStatus, string TargetStatus, long FramesSeen, long PacketsSent, DateTimeOffset? LastCorrection, IReadOnlyDictionary<int, long> MessageTypes)
{
    public static RtkInjectionSnapshot Idle { get; } = new(false, "Source disconnected", "No active vehicle target", 0, 0, null, new Dictionary<int, long>());
}

public interface IRtkInjectionService : IDisposable
{
    RtkInjectionSnapshot Current { get; }
    event EventHandler<RtkInjectionSnapshot>? Changed;
    Task StartAsync(RtkSourceOptions options, CancellationToken cancellationToken = default);
    Task StopAsync();
}

public sealed class RtkInjectionService(
    IRtkCorrectionSourceFactory sources,
    IActiveVehicleContext active,
    IVehicleRegistry registry,
    IMavLinkConnection connection,
    IMavLinkWireMessageEncoder encoder) : IRtkInjectionService
{
    private CancellationTokenSource? lifetime;
    private Task? worker;
    private byte sequence;
    public RtkInjectionSnapshot Current { get; private set; } = RtkInjectionSnapshot.Idle;
    public event EventHandler<RtkInjectionSnapshot>? Changed;

    public Task StartAsync(RtkSourceOptions options, CancellationToken cancellationToken = default)
    {
        if (worker is not null) throw new InvalidOperationException("An RTK correction source is already active.");
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        worker = RunAsync(options, lifetime.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        lifetime?.Cancel();
        if (worker is not null) try { await worker.ConfigureAwait(false); } catch (OperationCanceledException) { }
        lifetime?.Dispose(); lifetime = null; worker = null;
        Publish(Current with { IsRunning = false, SourceStatus = "Source disconnected" });
    }

    private async Task RunAsync(RtkSourceOptions options, CancellationToken token)
    {
        await using var source = await sources.OpenAsync(options, token).ConfigureAwait(false);
        var framer = new Rtcm3Framer();
        var buffer = new byte[4096];
        Publish(Current with { IsRunning = true, SourceStatus = "Correction source connected" });
        while (!token.IsCancellationRequested)
        {
            var read = await source.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0) break;
            foreach (var frame in framer.Push(buffer.AsSpan(0, read))) await ForwardAsync(frame, token).ConfigureAwait(false);
        }
    }

    private async Task ForwardAsync(byte[] frame, CancellationToken token)
    {
        var types = Current.MessageTypes.ToDictionary();
        var type = Rtcm3Framer.MessageType(frame);
        types[type] = types.GetValueOrDefault(type) + 1;
        var snapshot = Current with { FramesSeen = Current.FramesSeen + 1, LastCorrection = DateTimeOffset.UtcNow, MessageTypes = types };
        if (!active.IsOnline || active.VehicleId is not { } id || registry.GetRequired(id) is not { } target)
        {
            Publish(snapshot with { TargetStatus = "No active vehicle target" });
            return; // never queue stale corrections
        }
        var sent = 0;
        foreach (var fragment in GpsRtcmFragmenter.Fragment(frame, sequence++))
        {
            var data = new byte[180]; fragment.Data.CopyTo(data, 0);
            var message = new GpsRtcmDataMessage(255, 190, new TransportEndPoint("internal", "rtk"), fragment.Flags, checked((byte)fragment.Data.Length), data, DateTimeOffset.UtcNow);
            await connection.SendRawAsync(encoder.Encode(message), target.EndPoint, token).ConfigureAwait(false);
            sent++;
        }
        Publish(snapshot with { TargetStatus = $"Injecting to {id}", PacketsSent = Current.PacketsSent + sent });
    }

    private void Publish(RtkInjectionSnapshot snapshot) { Current = snapshot; Changed?.Invoke(this, snapshot); }
    public void Dispose() { lifetime?.Cancel(); lifetime?.Dispose(); }
}
