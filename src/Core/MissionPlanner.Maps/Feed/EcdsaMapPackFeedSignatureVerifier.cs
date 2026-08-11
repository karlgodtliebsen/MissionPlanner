using System.Security.Cryptography;

namespace MissionPlanner.Maps.Feed;

/// <summary>Verifies feed signatures using a pinned ECDSA public key.</summary>
public sealed class EcdsaMapPackFeedSignatureVerifier : IMapPackFeedSignatureVerifier, IDisposable
{
    private readonly ECDsa algorithm = ECDsa.Create();

    /// <summary>Initializes a verifier from a PEM-encoded public key.</summary>
    public EcdsaMapPackFeedSignatureVerifier(string publicKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        algorithm.ImportFromPem(publicKeyPem);
    }

    /// <inheritdoc />
    public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature) =>
        algorithm.VerifyData(payload, signature, HashAlgorithmName.SHA256);

    /// <inheritdoc />
    public void Dispose() => algorithm.Dispose();
}
