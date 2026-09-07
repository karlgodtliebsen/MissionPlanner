using System.Security.Cryptography;
using System.Text.Json;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.MavLink.Signing;

namespace MissionPlanner.Core.Setup.Advanced.Signing;

/// <summary>A loaded key owned by its caller; formatting never reveals the key.</summary>
public sealed class SavedSigningKey(byte[] key, byte linkId, ulong timestamp) : IDisposable
{
    /// <summary>Gets the caller-owned mutable key buffer.</summary>
    public byte[] Key { get; } = key;
    /// <summary>Gets the saved outbound link.</summary>
    public byte LinkId { get; } = linkId;
    /// <summary>Gets the durable timestamp reservation floor.</summary>
    public ulong Timestamp { get; } = timestamp;
    /// <inheritdoc />
    public void Dispose() => CryptographicOperations.ZeroMemory(Key);
    /// <inheritdoc />
    public override string ToString() => "Saved signing key (redacted)";
}

/// <summary>Stores keys and timestamp reservations exclusively through the existing platform secret store.</summary>
public sealed class SigningKeyRepository(IPlannerSecretStore store)
{
    private const string latest = "Advanced.Signing.Latest";
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Reads the last prepared key for explicit operator review, never automatic activation.</summary>
    public async Task<SavedSigningKey?> LoadAsync(CancellationToken token)
    {
        var id = await store.GetAsync(latest, token).ConfigureAwait(false);
        if (id is null)
        {
            return null;
        }
        var document = await ReadAsync(id, token).ConfigureAwait(false);
        return new(MavLinkSigningCodec.Import(document.Hex), document.LinkId, document.Reserved);
    }

    /// <summary>Preserves a recovery key before setup; prior key entries are retained in the credential store.</summary>
    public async Task<(string Id, ulong Floor)> PrepareAsync(byte[] key, byte linkId, ulong floor, CancellationToken token)
    {
        _ = MavLinkSigningCodec.Fingerprint(key);
        if (floor > MavLinkSigningCodec.MaximumTimestamp) { throw new ArgumentOutOfRangeException(nameof(floor)); }
        var id = $"Advanced.Signing.{Convert.ToHexString(SHA256.HashData(key))}.{linkId}";
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var existing = await store.GetAsync(id, token).ConfigureAwait(false);
            if (existing is not null)
            {
                floor = Math.Max(floor, Parse(existing).Reserved);
            }
            await store.SetAsync(id, JsonSerializer.Serialize(new Document(1, Convert.ToHexString(key), linkId, floor)), token).ConfigureAwait(false);
            await store.SetAsync(latest, id, token).ConfigureAwait(false);
            return (id, floor);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Durably reserves timestamps before transmission; failures stop new signed sends.</summary>
    public async Task ReserveAsync(string id, ulong upper, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var document = await ReadAsync(id, token).ConfigureAwait(false);
            await store.SetAsync(id, JsonSerializer.Serialize(document with { Reserved = Math.Max(document.Reserved, upper) }), token).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Document> ReadAsync(string id, CancellationToken token) =>
        Parse(await store.GetAsync(id, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Saved signing key is unavailable."));

    private static Document Parse(string json)
    {
        try
        {
            var document = json.Length <= 1024 ? JsonSerializer.Deserialize<Document>(json) : null;
            if (document is null || document.Version != 1 || document.Hex is not { Length: 64 } || !document.Hex.All(Uri.IsHexDigit)
                || document.Reserved > MavLinkSigningCodec.MaximumTimestamp)
            {
                throw new InvalidOperationException("Saved signing state is invalid.");
            }
            return document;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Saved signing state is invalid.");
        }
    }

    // This serialization is allowed only inside the platform credential store, never diagnostics or exports.
    private sealed record Document(int Version, string Hex, byte LinkId, ulong Reserved)
    {
        public override string ToString() => "Signing credential (redacted)";
    }
}
