using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Signing;

/// <summary>A non-secret snapshot of one connection's signing state.</summary>
public sealed record MavLinkSigningSnapshot(string State, string Fingerprint, byte LinkId, ulong Timestamp,
    long Verified, long Unsigned, long Invalid, long Replayed)
{
    /// <summary>Gets the most recent successfully authenticated inbound timestamp.</summary>
    public ulong? LastVerifiedTimestamp { get; init; }
}

/// <summary>Connection-owned signing state, with staged verification and durable timestamp reservations.</summary>
public sealed class MavLinkSigningSession(IMavLinkCrcExtraProvider crc, TimeProvider clock) : IDisposable
{
    private readonly object sync = new();
    private readonly SemaphoreSlim sends = new(1, 1);
    private MavLinkSigningContext? active;
    private MavLinkSigningContext? candidate;
    private TaskCompletionSource? confirmation;
    private byte targetSystem;
    private byte targetComponent;
    private ulong initial;
    private ulong reserved;
    private Func<ulong, CancellationToken, Task>? persistReservation;
    private long verified;
    private long unsigned;
    private long invalid;
    private long replayed;
    private ulong? lastVerifiedTimestamp;
    private MavLinkSignatureStatus lastSignature;

    /// <summary>Gets diagnostics without exposing a key.</summary>
    public MavLinkSigningSnapshot Snapshot()
    {
        lock (sync)
        {
            var context = active ?? candidate;
            return new(candidate is not null ? "Configuring" : active is not null
                    ? lastSignature is MavLinkSignatureStatus.Invalid or MavLinkSignatureStatus.Replay ? "Verification failing" : "Active" : "Disabled",
                context?.Fingerprint ?? "No key", context?.LinkId ?? 0, context?.Timestamp ?? 0,
                verified, unsigned, invalid, replayed) { LastVerifiedTimestamp = lastVerifiedTimestamp };
        }
    }

    /// <summary>Stages a key for verification without changing outbound signing or the existing active key.</summary>
    public Task BeginSetup(ReadOnlySpan<byte> key, byte linkId, byte systemId, byte componentId, ulong timestamp)
    {
        lock (sync)
        {
            if (candidate is not null)
            {
                throw new InvalidOperationException("A signing setup is already in progress.");
            }
            candidate = new(key, linkId, timestamp);
            targetSystem = systemId;
            targetComponent = componentId;
            initial = timestamp;
            confirmation = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return confirmation.Task;
        }
    }

    /// <summary>Activates only a verified candidate, with a reserved timestamp floor saved before sending.</summary>
    public async Task CommitSetupAsync(Func<ulong, CancellationToken, Task> reserve, CancellationToken token)
    {
        await sends.WaitAsync(token).ConfigureAwait(false);
        try
        {
            MavLinkSigningContext next;
            ulong upper;
            lock (sync)
            {
                if (candidate is null || confirmation?.Task.IsCompletedSuccessfully != true)
                {
                    throw new InvalidOperationException("Vehicle signing has not been verified.");
                }
                next = candidate;
                upper = Reservation(next.Timestamp);
            }
            await reserve(upper, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (candidate != next)
                {
                    throw new OperationCanceledException("Signing setup ended before activation.");
                }
                active?.Dispose();
                active = next;
                candidate = null;
                confirmation = null;
                reserved = upper;
                persistReservation = reserve;
            }
        }
        finally
        {
            sends.Release();
        }
    }

    /// <summary>Abandons only the staged key, preserving the previous operational configuration.</summary>
    public void CancelSetup()
    {
        lock (sync)
        {
            candidate?.Dispose();
            candidate = null;
            confirmation?.TrySetCanceled();
            confirmation = null;
        }
    }

    /// <summary>Authenticates a CRC-checked frame. Unsigned frames remain permitted.</summary>
    public MavLinkSignatureStatus Verify(ReadOnlySpan<byte> packet)
    {
        lock (sync)
        {
            if (packet.Length < 3 || packet[0] != 0xfd || (packet[2] & 1) == 0)
            {
                unsigned++;
                return MavLinkSignatureStatus.Unsigned;
            }
            var result = MavLinkSignatureStatus.Unverified;
            if (candidate is not null && packet.Length >= 25 && packet[5] == targetSystem && packet[6] == targetComponent)
            {
                result = candidate.Verify(packet);
                if (result == MavLinkSignatureStatus.Verified && MavLinkSigningCodec.ReadTimestamp(packet) >= initial)
                {
                    confirmation?.TrySetResult();
                }
                else if (result == MavLinkSignatureStatus.Verified)
                {
                    result = MavLinkSignatureStatus.Replay;
                }
            }
            if (result != MavLinkSignatureStatus.Verified && active is not null)
            {
                result = active.Verify(packet);
            }
            switch (result)
            {
                case MavLinkSignatureStatus.Verified:
                    verified++;
                    lastVerifiedTimestamp = MavLinkSigningCodec.ReadTimestamp(packet);
                    break;
                case MavLinkSignatureStatus.Invalid: invalid++; break;
                case MavLinkSignatureStatus.Replay: replayed++; break;
            }
            lastSignature = result;
            return result;
        }
    }

    /// <summary>Signs complete outbound packets, reserving future timestamps before they can be used.</summary>
    public async ValueTask<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        await sends.WaitAsync(token).ConfigureAwait(false);
        MemoryStream? output = null;
        byte[]? packet = null;
        try
        {
            lock (sync)
            {
                if (active is null)
                {
                    return bytes;
                }
            }
            output = new MemoryStream();
            var offset = 0;
            while (offset < bytes.Length)
            {
                if (packet is not null) { System.Security.Cryptography.CryptographicOperations.ZeroMemory(packet); }
                packet = ExtractPacket(bytes.Span[offset..]);
                offset += packet.Length;
                // Preserve already-signed forwarded traffic; it belongs to its original sender/link.
                if (packet[0] == 0xfd && (packet[2] & 1) != 0)
                {
                    output.Write(packet);
                    continue;
                }
                packet = Promote(packet);
                var id = (uint)(packet[7] | packet[8] << 8 | packet[9] << 16);
                if (!crc.TryGetCrcExtra(id, out var extra))
                {
                    throw new InvalidOperationException("Cannot sign a message with an unknown CRC extra.");
                }
                while (true)
                {
                    ulong upper;
                    Func<ulong, CancellationToken, Task>? persist;
                    MavLinkSigningContext context;
                    lock (sync)
                    {
                        context = active ?? throw new OperationCanceledException("Signing connection closed.");
                        var time = MavLinkSigningCodec.Timestamp(clock.GetUtcNow());
                        if (Math.Max(context.Timestamp, time) < reserved)
                        {
                            var signed = context.Sign(packet, extra, time);
                            output.Write(signed);
                            System.Security.Cryptography.CryptographicOperations.ZeroMemory(signed);
                            break;
                        }
                        upper = Reservation(Math.Max(context.Timestamp, time));
                        persist = persistReservation;
                    }
                    await persist!(upper, token).ConfigureAwait(false);
                    lock (sync)
                    {
                        if (active != context)
                        {
                            throw new OperationCanceledException("Signing configuration changed.");
                        }
                        reserved = upper;
                    }
                }
            }
            return output.ToArray();
        }
        finally
        {
            if (packet is not null) { System.Security.Cryptography.CryptographicOperations.ZeroMemory(packet); }
            if (output is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(output.GetBuffer());
                output.Dispose();
            }
            sends.Release();
        }
    }

    /// <summary>Erases both keys and all replay state when the owning connection closes.</summary>
    public void Dispose()
    {
        lock (sync)
        {
            CancelSetup();
            active?.Dispose();
            active = null;
            persistReservation = null;
            reserved = 0;
            verified = unsigned = invalid = replayed = 0;
            lastVerifiedTimestamp = null;
            lastSignature = MavLinkSignatureStatus.Unsigned;
        }
    }

    private static ulong Reservation(ulong timestamp)
    {
        if (timestamp >= MavLinkSigningCodec.MaximumTimestamp)
        {
            throw new InvalidOperationException("Signing timestamp exhausted.");
        }
        return Math.Min(MavLinkSigningCodec.MaximumTimestamp, timestamp + 6_000_000);
    }

    private static byte[] ExtractPacket(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8 || bytes[0] is not (0xfd or 0xfe))
        {
            throw new InvalidOperationException("Signing requires complete MAVLink packets.");
        }
        var length = bytes[1] + (bytes[0] == 0xfd ? 12 + ((bytes[2] & 1) != 0 ? 13 : 0) : 8);
        if (bytes.Length < length)
        {
            throw new InvalidOperationException("Signing received an incomplete packet.");
        }
        return bytes[..length].ToArray();
    }

    private static byte[] Promote(byte[] packet)
    {
        if (packet[0] == 0xfd)
        {
            return packet;
        }
        var output = new byte[packet.Length + 4];
        output[0] = 0xfd;
        output[1] = packet[1];
        output[4] = packet[2];
        output[5] = packet[3];
        output[6] = packet[4];
        output[7] = packet[5];
        packet.AsSpan(6, packet[1]).CopyTo(output.AsSpan(10));
        return output;
    }
}
