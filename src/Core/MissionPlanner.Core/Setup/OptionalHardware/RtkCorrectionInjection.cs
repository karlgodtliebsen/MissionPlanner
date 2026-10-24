using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Text;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Splits a byte stream into complete RTCM3 frames and checks their CRC.</summary>
public sealed class Rtcm3Framer
{
    private readonly List<byte> buffer = [];

    /// <summary>Adds bytes to the stream and returns every complete, CRC-valid frame found so far.</summary>
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
            if ((buffer[1] & 0xFC) != 0) { buffer.RemoveAt(0); continue; }
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

    /// <summary>Gets the RTCM3 message type of a frame.</summary>
    public static int MessageType(ReadOnlySpan<byte> frame) => frame.Length >= 5 ? (frame[3] << 4) | (frame[4] >> 4) : 0;

    /// <summary>Computes the CRC-24Q of the data.</summary>
    public static int Crc24Q(ReadOnlySpan<byte> data)
    {
        var crc = 0;
        foreach (var value in data)
        {
            crc ^= value << 16;
            for (var bit = 0; bit < 8; bit++) crc = (crc << 1) ^ ((crc & 0x800000) != 0 ? 0x1864CFB : 0);
        }
        return crc & 0xFFFFFF;
    }
}

/// <summary>One fragment of an RTCM frame in a GPS_RTCM_DATA message.</summary>
/// <param name="Flags">The fragmentation flags, sequence and fragment number.</param>
/// <param name="Data">The fragment payload.</param>
public sealed record RtcmFragment(byte Flags, byte[] Data);

/// <summary>Splits RTCM frames into GPS_RTCM_DATA fragments.</summary>
public static class GpsRtcmFragmenter
{
    /// <summary>The maximum payload size of one fragment in bytes.</summary>
    public const int PayloadSize = 180;
    /// <summary>Splits a frame into fragments tagged with the sequence number.</summary>
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
        if (frame.Length % PayloadSize == 0 && count < 4)
        {
            fragments.Add(new RtcmFragment((byte)(1 | (count << 1) | ((sequence & 0x1F) << 3)), []));
        }
        return fragments;
    }
}

/// <summary>Where RTK corrections come from.</summary>
public enum RtkSourceKind
{
    /// <summary>A serial port connected to a base station.</summary>
    Serial,
    /// <summary>An NTRIP caster over the network.</summary>
    Ntrip,
    /// <summary>A TCP client connected to a correction stream.</summary>
    Tcp,
    /// <summary>A UDP listener bound to an explicitly selected local address.</summary>
    Udp,
}
/// <summary>Options for opening an RTK correction source.</summary>
/// <param name="Kind">The source type.</param>
/// <param name="Endpoint">The serial port name or NTRIP host.</param>
/// <param name="PortOrBaud">The NTRIP port or the serial baud rate.</param>
/// <param name="MountPoint">The NTRIP mount point.</param>
/// <param name="Username">The NTRIP user name.</param>
/// <param name="Password">The NTRIP password.</param>
/// <param name="AllConnectedVehicles">Whether to capture all currently connected target endpoints at start.</param>
/// <param name="UseTls">Whether to use TLS for NTRIP.</param>
public sealed record RtkSourceOptions(RtkSourceKind Kind, string Endpoint, int PortOrBaud, string? MountPoint = null, string? Username = null, string? Password = null, bool UseTls = false, bool AllConnectedVehicles = false);

/// <summary>A source of RTK correction bytes.</summary>
public interface IRtkCorrectionSource : IAsyncDisposable
{
    /// <summary>Reads correction bytes into the buffer and returns the number of bytes read.</summary>
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}

/// <summary>Opens RTK correction sources.</summary>
public interface IRtkCorrectionSourceFactory
{
    /// <summary>Opens the source described by the options.</summary>
    Task<IRtkCorrectionSource> OpenAsync(RtkSourceOptions options, CancellationToken cancellationToken);
}

/// <summary>Opens serial and NTRIP correction sources.</summary>
public sealed class RtkCorrectionSourceFactory(IDirectSerialSessionFactory serial, IHttpClientFactory clients) : IRtkCorrectionSourceFactory
{
    /// <summary>Opens the source described by the options.</summary>
    public async Task<IRtkCorrectionSource> OpenAsync(RtkSourceOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(options.Endpoint) || options.PortOrBaud <= 0 || !Enum.IsDefined(options.Kind))
        {
            throw new ArgumentException("Select a source address and positive port or baud rate.");
        }
        if (options.Kind != RtkSourceKind.Serial && options.PortOrBaud > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Network ports must be between 1 and 65535.");
        }
        if (options.Kind == RtkSourceKind.Tcp)
        {
            var client = new TcpClient();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await client.ConnectAsync(options.Endpoint, options.PortOrBaud, timeout.Token).ConfigureAwait(false);
                return new TcpSource(client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }
        if (options.Kind == RtkSourceKind.Udp)
        {
            if (!IPAddress.TryParse(options.Endpoint, out var address))
            {
                throw new ArgumentException("UDP requires a local bind address, for example 127.0.0.1 or 0.0.0.0.");
            }
            return new UdpSource(new UdpClient(new IPEndPoint(address, options.PortOrBaud)));
        }
        if (options.Kind == RtkSourceKind.Serial)
            return new SerialSource(await serial.OpenAsync(options.Endpoint, options.PortOrBaud, cancellationToken).ConfigureAwait(false));

        var scheme = options.UseTls ? "https" : "http";
        var mount = (options.MountPoint ?? string.Empty).TrimStart('/');
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{scheme}://{options.Endpoint}:{options.PortOrBaud}/{mount}");
        request.Headers.UserAgent.ParseAdd("MissionPlanner-NextGen/1.0");
        request.Headers.TryAddWithoutValidation("Ntrip-Version", "Ntrip/2.0");
        if (!string.IsNullOrEmpty(options.Username))
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Username}:{options.Password ?? string.Empty}")));
        var response = await clients.CreateClient("RTK").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
            return new HttpSource(response, await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private sealed class TcpSource(TcpClient client) : IRtkCorrectionSource
    {
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken token) =>
            await client.GetStream().ReadAsync(buffer, token).ConfigureAwait(false);
        public ValueTask DisposeAsync()
        {
            client.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class UdpSource(UdpClient client) : IRtkCorrectionSource
    {
        private ReadOnlyMemory<byte> remaining;
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }
            while (remaining.IsEmpty)
            {
                remaining = (await client.ReceiveAsync(token).ConfigureAwait(false)).Buffer;
            }
            var count = Math.Min(buffer.Length, remaining.Length);
            remaining[..count].CopyTo(buffer);
            remaining = remaining[count..];
            return count;
        }
        public ValueTask DisposeAsync()
        {
            client.Dispose();
            return ValueTask.CompletedTask;
        }
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

/// <summary>A snapshot of the RTK correction injection state.</summary>
/// <param name="IsRunning">Whether injection is running.</param>
/// <param name="SourceStatus">The correction source status text.</param>
/// <param name="TargetStatus">The target vehicle status text.</param>
/// <param name="FramesSeen">The number of RTCM frames read.</param>
/// <param name="PacketsSent">The number of MAVLink packets sent.</param>
/// <param name="LastCorrection">When the last correction was seen.</param>
/// <param name="MessageTypes">The count of frames per RTCM message type.</param>
public sealed record RtkInjectionSnapshot(bool IsRunning, string SourceStatus, string TargetStatus, long FramesSeen, long PacketsSent, DateTimeOffset? LastCorrection, IReadOnlyDictionary<int, long> MessageTypes)
{
    /// <summary>Gets the snapshot used before injection has started.</summary>
    public static RtkInjectionSnapshot Idle { get; } = new(false, "Source disconnected", "No active vehicle target", 0, 0, null, new Dictionary<int, long>());
}

/// <summary>Reads RTK corrections and forwards them to the active vehicle.</summary>
public interface IRtkInjectionService : IDisposable
{
    /// <summary>Gets the latest injection snapshot.</summary>
    RtkInjectionSnapshot Current { get; }
    /// <summary>Occurs when the snapshot changes.</summary>
    event EventHandler<RtkInjectionSnapshot>? Changed;
    /// <summary>Starts reading corrections and injecting them into the vehicle.</summary>
    Task StartAsync(RtkSourceOptions options, CancellationToken cancellationToken = default);
    /// <summary>Stops injection.</summary>
    Task StopAsync();
}
