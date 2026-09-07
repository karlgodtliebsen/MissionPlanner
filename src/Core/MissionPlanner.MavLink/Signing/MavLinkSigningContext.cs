using System.Security.Cryptography;

namespace MissionPlanner.MavLink.Signing;

/// <summary>Describes inbound authentication without exposing key material.</summary>
public enum MavLinkSignatureStatus
{
    /// <summary>No signature is present; the current policy permits unsigned traffic.</summary>
    Unsigned,
    /// <summary>A signature is present but no key is configured.</summary>
    Unverified,
    /// <summary>Signature and replay checks passed.</summary>
    Verified,
    /// <summary>The signature or signed frame structure is invalid.</summary>
    Invalid,
    /// <summary>The signature is valid but its timestamp violates the replay policy.</summary>
    Replay
}

/// <summary>Owns one key and bounded replay state. The owner serializes access and disposes replaced contexts.</summary>
public sealed class MavLinkSigningContext : IDisposable
{
    private readonly byte[] key;
    private readonly Dictionary<(byte System, byte Component, byte Link), ulong> received = [];
    private bool disposed;
    /// <summary>Gets the outbound link identifier.</summary>
    public byte LinkId { get; }
    /// <summary>Gets the latest accepted or generated timestamp.</summary>
    public ulong Timestamp { get; private set; }
    /// <summary>Gets a non-secret identifier for the key.</summary>
    public string Fingerprint { get; }

    /// <summary>Copies a validated key and restores the platform-approved timestamp floor.</summary>
    public MavLinkSigningContext(ReadOnlySpan<byte> secret, byte linkId, ulong initialTimestamp)
    {
        Fingerprint = MavLinkSigningCodec.Fingerprint(secret);
        if (initialTimestamp > MavLinkSigningCodec.MaximumTimestamp)
        {
            throw new ArgumentOutOfRangeException(nameof(initialTimestamp));
        }
        key = secret.ToArray();
        LinkId = linkId;
        Timestamp = initialTimestamp;
    }

    /// <summary>Verifies a frame without changing replay state on an invalid signature.</summary>
    public MavLinkSignatureStatus Verify(ReadOnlySpan<byte> packet)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!MavLinkSigningCodec.Verify(packet, key))
        {
            return MavLinkSignatureStatus.Invalid;
        }
        var timestamp = MavLinkSigningCodec.ReadTimestamp(packet);
        var source = (packet[5], packet[6], packet[^13]);
        if (timestamp < Timestamp && Timestamp - timestamp > 6_000_000
            || received.TryGetValue(source, out var previous) && timestamp <= previous
            || !received.ContainsKey(source) && received.Count >= 1024)
        {
            return MavLinkSignatureStatus.Replay;
        }
        received[source] = timestamp;
        Timestamp = Math.Max(Timestamp, timestamp);
        return MavLinkSignatureStatus.Verified;
    }

    /// <summary>Signs using the maximum of the restored floor, clock and last accepted timestamp, without wraparound.</summary>
    public byte[] Sign(ReadOnlySpan<byte> packet, byte crcExtra, ulong clockTimestamp)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Timestamp == MavLinkSigningCodec.MaximumTimestamp || clockTimestamp > MavLinkSigningCodec.MaximumTimestamp)
        {
            throw new InvalidOperationException("Signing timestamp exhausted; reconnect with a reviewed configuration.");
        }
        var next = Math.Max(Timestamp + 1, clockTimestamp);
        var signed = MavLinkSigningCodec.Sign(packet, key, crcExtra, LinkId, next);
        Timestamp = next;
        return signed;
    }

    /// <summary>Erases mutable key material and replay history.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(key);
        received.Clear();
        disposed = true;
    }
}
