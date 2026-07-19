using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.MavFtp;

public sealed class MavFtpClient : IMavFtpClient
{
    private readonly IMavLinkConnection connection;
    private readonly IMavFtpMessageEncoder messageEncoder;
    private readonly IMavFtpPacketCodec packetCodec;
    private readonly IEventHub eventHub;
    private readonly ILogger<MavFtpClient> logger;
    private readonly MavFtpOptions options;
    private readonly ConcurrentDictionary<MavFtpTarget, SemaphoreSlim> targetLocks = new();
    private int nextSequence;

    public MavFtpClient(IMavLinkConnection connection, IMavFtpMessageEncoder messageEncoder,
        IMavFtpPacketCodec packetCodec, IEventHub eventHub, IOptions<MavFtpOptions> options,
        ILogger<MavFtpClient> logger)
    {
        this.connection = connection;
        this.messageEncoder = messageEncoder;
        this.packetCodec = packetCodec;
        this.eventHub = eventHub;
        this.logger = logger;
        this.options = options.Value;
        this.options.Validate();
    }

    public Task ResetSessionsAsync(MavFtpTarget target, CancellationToken cancellationToken = default)
    {
        return InOperationLockAsync(target, async ct =>
        {
            await RequestAsync(target, 0, MavFtpOpcode.ResetSessions, 0, ReadOnlyMemory<byte>.Empty, null, ct).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    public Task<MavFtpFileInfo> GetFileInfoAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default)
    {
        return InOperationLockAsync(target, async ct =>
        {
            var opened = await OpenAsync(target, remotePath, ct).ConfigureAwait(false);
            try
            {
                return new MavFtpFileInfo(remotePath, opened.Size);
            }
            finally { await CleanupSessionAsync(target, opened.Session).ConfigureAwait(false); }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<MavFtpDirectoryEntry>> ListDirectoryAsync(MavFtpTarget target, string remotePath, CancellationToken cancellationToken = default)
    {
        return InOperationLockAsync(target, async ct =>
        {
            var entries = new List<MavFtpDirectoryEntry>();
            uint offset = 0;
            while (true)
            {
                MavFtpPacket response;
                try
                {
                    response = await RequestAsync(target, 0, MavFtpOpcode.ListDirectory, offset, PathBytes(remotePath), remotePath, ct).ConfigureAwait(false);
                }
                catch (MavFtpRemoteException ex) when (ex.Error == MavFtpNakError.EndOfFile) { break; }

                var page = MavFtpDirectoryCodec.Decode(response.Data.Span);
                if (page.Count == 0)
                {
                    break;
                }

                entries.AddRange(page.Where(x => x.Type != MavFtpDirectoryEntryType.Skip));
                offset += checked((uint)page.Count);
            }

            return (IReadOnlyList<MavFtpDirectoryEntry>)entries;
        }, cancellationToken);
    }

    public async Task<byte[]> DownloadFileAsync(MavFtpTarget target, string remotePath, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await DownloadFileAsync(target, remotePath, stream, progress, cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    public Task DownloadFileAsync(MavFtpTarget target, string remotePath, Stream destination, IProgress<MavFtpProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return InOperationLockAsync(target, async ct =>
        {
            if (!destination.CanWrite)
            {
                throw new ArgumentException("Destination stream must be writable.", nameof(destination));
            }

            var opened = await OpenAsync(target, remotePath, ct).ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();
            long written = 0;
            try
            {
                while (written < opened.Size)
                {
                    var requested = Math.Min(options.ReadChunkSize, checked((int)(opened.Size - written)));
                    var opcode = options.PreferBurstRead && written == 0 ? MavFtpOpcode.BurstReadFile : MavFtpOpcode.ReadFile;
                    MavFtpPacket response;
                    try
                    {
                        response = await RequestAsync(target, opened.Session, opcode, checked((uint)written), new byte[requested], remotePath, ct).ConfigureAwait(false);
                    }
                    catch (MavFtpRemoteException ex) when (opcode == MavFtpOpcode.BurstReadFile && ex.Error == MavFtpNakError.UnknownCommand)
                    {
                        logger.LogInformation("MAVFTP burst read is unsupported for {RemotePath}; falling back to regular reads.", remotePath);
                        response = await RequestAsync(target, opened.Session, MavFtpOpcode.ReadFile, checked((uint)written), new byte[requested], remotePath, ct).ConfigureAwait(false);
                    }

                    if (response.Offset != written || response.Data.IsEmpty)
                    {
                        throw new MavFtpProtocolException("MAVFTP returned an impossible or empty read block.");
                    }

                    await destination.WriteAsync(response.Data, ct).ConfigureAwait(false);
                    written += response.Data.Length;
                    progress?.Report(new MavFtpProgress(remotePath, written, opened.Size, written / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001)));
                }

                progress?.Report(new MavFtpProgress(remotePath, written, opened.Size, written / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001)));
            }
            finally { await CleanupSessionAsync(target, opened.Session).ConfigureAwait(false); }

            return 0;
        }, cancellationToken);
    }

    private async Task<(byte Session, long Size)> OpenAsync(MavFtpTarget target, string path, CancellationToken ct)
    {
        var response = await RequestAsync(target, 0, MavFtpOpcode.OpenFileReadOnly, 0, PathBytes(path), path, ct).ConfigureAwait(false);
        return response.Data.Length != sizeof(uint)
            ? throw new MavFtpProtocolException("OpenFileRO ACK did not contain a 32-bit file size.")
            : ((byte Session, long Size))(response.Session, BinaryPrimitives.ReadUInt32LittleEndian(response.Data.Span));
    }

    private async Task<MavFtpPacket> RequestAsync(MavFtpTarget target, byte session, MavFtpOpcode opcode, uint offset, ReadOnlyMemory<byte> data, string? path, CancellationToken ct)
    {
        var sequence = unchecked((ushort)Interlocked.Increment(ref nextSequence));
        var request = new MavFtpPacket(sequence, session, opcode, MavFtpOpcode.None, false, offset, data);
        for (var attempt = 1; ; attempt++)
        {
            var completion = new TaskCompletionSource<MavFtpPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
            {
                if (message is not FileTransferProtocolMessage ftp || ftp.SystemId != target.SystemId || ftp.ComponentId != target.ComponentId || !ftp.EndPoint.Equals(target.EndPoint))
                {
                    return Task.CompletedTask;
                }

                try
                {
                    var packet = packetCodec.Decode(ftp.Payload);
                    if (packet.Sequence == unchecked((ushort)(sequence + 1)) && packet.RequestedOpcode == opcode && (packet.Session == session || opcode == MavFtpOpcode.OpenFileReadOnly))
                    {
                        completion.TrySetResult(packet);
                    }
                }
                catch (MavFtpProtocolException ex) { logger.LogWarning(ex, "Malformed MAVFTP response from {SystemId}/{ComponentId}.", target.SystemId, target.ComponentId); }

                return Task.CompletedTask;
            });
            await connection.SendRawAsync(messageEncoder.Encode(target.SystemId, target.ComponentId, packetCodec.Encode(request)), target.EndPoint, ct).ConfigureAwait(false);
            try
            {
                var response = await completion.Task.WaitAsync(options.RequestTimeout, ct).ConfigureAwait(false);
                if (response.Opcode == MavFtpOpcode.Nak)
                {
                    if (response.Data.IsEmpty)
                    {
                        throw new MavFtpProtocolException("MAVFTP NAK has no error code.");
                    }

                    throw new MavFtpRemoteException((MavFtpNakError)response.Data.Span[0], response, path);
                }

                return response.Opcode != MavFtpOpcode.Ack ? throw new MavFtpProtocolException("Expected MAVFTP ACK or NAK.") : response;
            }
            catch (TimeoutException) when (attempt < options.MaximumRequestAttempts)
            {
                logger.LogWarning("MAVFTP {Opcode} timed out; retrying attempt {Attempt}/{MaximumAttempts}.", opcode, attempt + 1, options.MaximumRequestAttempts);
            }
        }
    }

    private async Task CleanupSessionAsync(MavFtpTarget target, byte session)
    {
        try
        {
            await RequestAsync(target, session, MavFtpOpcode.TerminateSession, 0, ReadOnlyMemory<byte>.Empty, null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) { logger.LogWarning(ex, "MAVFTP session {Session} cleanup failed.", session); }
    }

    private async Task<T> InOperationLockAsync<T>(MavFtpTarget target, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var gate = targetLocks.GetOrAdd(target, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await action(ct).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private static byte[] PathBytes(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Remote path cannot contain NUL.", nameof(path));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(path);
        return bytes.Length > 239
            ? throw new ArgumentOutOfRangeException(nameof(path), "UTF-8 remote path exceeds MAVFTP payload capacity.")
            : bytes;
    }
}
