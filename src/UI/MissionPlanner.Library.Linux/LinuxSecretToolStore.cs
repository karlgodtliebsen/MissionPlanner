using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.Library.Linux;

/// <summary>
/// Stores planner secrets in the desktop keyring (GNOME Keyring, KWallet or any Secret Service provider)
/// through the <c>secret-tool</c> command from libsecret. Secrets are passed on standard input, never on the
/// command line.
/// </summary>
public sealed class LinuxSecretToolStore : IPlannerSecretStore
{
    private const string Executable = "secret-tool";
    private const string Service = "MissionPlanner.PlannerSecrets";
    private static readonly TimeSpan commandTimeout = TimeSpan.FromSeconds(15);
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Gets whether <c>secret-tool</c> is installed.</summary>
    public static bool IsAvailable()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        return !string.IsNullOrEmpty(path)
            && path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Any(directory => File.Exists(Path.Combine(directory, Executable)));
    }

    /// <summary>Gets the secret stored under the key, or null when there is none.</summary>
    public async ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await RunAsync(["lookup", "service", Service, "account", key], null, cancellationToken).ConfigureAwait(false);
            // An unknown key exits with status 1 and no output.
            return result.ExitCode == 0 && result.Output.Length > 0 ? result.Output : null;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Stores a secret under the key, replacing any existing secret.</summary>
    public async ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await RunAsync(["store", "--label=MissionPlanner " + key, "service", Service, "account", key], value, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                throw new IOException("The desktop keyring refused to store the secret. Check that a keyring service is running and unlocked.");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Removes the secret stored under the key.</summary>
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RunAsync(["clear", "service", Service, "account", key], null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string[] arguments, string? standardInput, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(Executable)
        {
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("The secret-tool command (package libsecret-tools) is not installed.", exception);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(commandTimeout);
        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), timeout.Token).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
            await process.StandardError.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return (process.ExitCode, output.TrimEnd('\n'));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(true);
            throw new TimeoutException("The desktop keyring did not answer in time.");
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }

            throw;
        }
    }
}
