using MissionPlanner.Core.ConfigTuning.Planner;
using Windows.Security.Credentials;

namespace MissionPlanner.Library.Windows;

/// <summary>Stores Planner credentials and tokens through the platform secure store.</summary>
public sealed class SecurePlannerSecretStore : IPlannerSecretStore
{
    private const string VaultResource = "MissionPlanner.PlannerSecrets";
    private const int ElementNotFoundHResult = unchecked((int)0x80070490);
    private readonly PasswordVault vault = new();
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <inheritdoc />
    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var credential = FindCredential(key);
            if (credential is null)
            {
                return null;
            }

            credential.RetrievePassword();
            return credential.Password;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RemoveCredentials(key);
            vault.Add(new PasswordCredential(VaultResource, key, value));
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RemoveCredentials(key);
        }
        finally
        {
            gate.Release();
        }
    }

    private PasswordCredential? FindCredential(string key)
    {
        return FindCredentials()
            .FirstOrDefault(credential => string.Equals(credential.UserName, key, StringComparison.Ordinal));
    }

    private void RemoveCredentials(string key)
    {
        foreach (var credential in FindCredentials()
                     .Where(credential => string.Equals(credential.UserName, key, StringComparison.Ordinal))
                     .ToArray())
        {
            vault.Remove(credential);
        }
    }

    private IReadOnlyList<PasswordCredential> FindCredentials()
    {
        try
        {
            return vault.FindAllByResource(VaultResource);
        }
        catch (Exception exception) when (exception.HResult == ElementNotFoundHResult)
        {
            return [];
        }
    }
}
