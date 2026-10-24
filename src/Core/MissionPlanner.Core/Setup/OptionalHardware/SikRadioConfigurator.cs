using System.Globalization;
using System.Text.RegularExpressions;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Implements bounded SiK command conversations with register validation and readback.</summary>
public sealed class SikRadioConfigurator(IDirectSerialSessionFactory sessions) : ISikRadioConfigurator
{
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<SikRadioSnapshot> ReadAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        await using var session = await sessions.OpenAsync(portName, baudRate, cancellationToken).ConfigureAwait(false);
        await EnterAsync(session, cancellationToken).ConfigureAwait(false);
        try
        {
            await session.WriteAsync("ATI\r\n", cancellationToken).ConfigureAwait(false);
            var identity = await ResponseAsync(session, "ATI", cancellationToken).ConfigureAwait(false);
            var names = new Dictionary<string, string>();
            var local = await ReadSettingsAsync(session, "ATI5", names, cancellationToken).ConfigureAwait(false);
            var remoteNames = new Dictionary<string, string>();
            IReadOnlyDictionary<string, string> remote;
            try
            {
                remote = await ReadSettingsAsync(session, "RTI5", remoteNames, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or NotSupportedException)
            {
                remote = new Dictionary<string, string>();
            }
            return new(identity, local, remote) { Names = names, RemoteNames = remoteNames };
        }
        finally
        {
            await LeaveAsync(session).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task ApplyAsync(string portName, int baudRate, IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default) =>
        ApplyPairedAsync(portName, baudRate, settings, new Dictionary<string, string>(), cancellationToken);

    /// <inheritdoc />
    public Task ApplyPairedAsync(string portName, int baudRate, IReadOnlyDictionary<string, string> local,
        IReadOnlyDictionary<string, string> remote, CancellationToken cancellationToken = default) => ApplyCoreAsync(portName, baudRate, null, local, remote, cancellationToken);

    /// <inheritdoc />
    public Task ApplyReviewedAsync(string portName, int baudRate, SikRadioSnapshot expected, IReadOnlyDictionary<string, string> local,
        IReadOnlyDictionary<string, string> remote, CancellationToken cancellationToken = default) => ApplyCoreAsync(portName, baudRate, expected, local, remote, cancellationToken);

    private async Task ApplyCoreAsync(string portName, int baudRate, SikRadioSnapshot? expected, IReadOnlyDictionary<string, string> local,
        IReadOnlyDictionary<string, string> remote, CancellationToken cancellationToken)
    {
        Validate(local);
        Validate(remote);
        await using var session = await sessions.OpenAsync(portName, baudRate, cancellationToken).ConfigureAwait(false);
        await EnterAsync(session, cancellationToken).ConfigureAwait(false);
        var localRebootRequested = false;
        try
        {
            if (expected is not null)
            {
                await session.WriteAsync("ATI\r\n", cancellationToken).ConfigureAwait(false);
                if (await ResponseAsync(session, "ATI", cancellationToken).ConfigureAwait(false) != expected.Identity)
                    throw new InvalidOperationException("Radio identity/firmware changed. Read both radios again; nothing written.");
                var localNames = new Dictionary<string, string>();
                var actualLocal = await ReadSettingsAsync(session, "ATI5", localNames, cancellationToken).ConfigureAwait(false);
                CheckBaseline(local, expected.LocalSettings, actualLocal, expected.Names, localNames);
                if (remote.Count > 0)
                {
                    var remoteNames = new Dictionary<string, string>();
                    var actualRemote = await ReadSettingsAsync(session, "RTI5", remoteNames, cancellationToken).ConfigureAwait(false);
                    CheckBaseline(remote, expected.RemoteSettings, actualRemote, expected.RemoteNames, remoteNames);
                }
            }
            // Save the remote side before changing the local link; a failed ACK aborts the remaining operations.
            await WriteSideAsync(session, "RT", remote, cancellationToken).ConfigureAwait(false);
            await WriteSideAsync(session, "AT", local, cancellationToken).ConfigureAwait(false);
            if (remote.Count > 0)
            {
                await session.WriteAsync("RTZ\r\n", cancellationToken).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            if (local.Count > 0)
            {
                localRebootRequested = true;
                await session.WriteAsync("ATZ\r\n", cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (!localRebootRequested)
            {
                await LeaveAsync(session).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task RestoreDefaultsAsync(string portName, int baudRate, bool remote, CancellationToken cancellationToken = default)
    {
        await using var session = await sessions.OpenAsync(portName, baudRate, cancellationToken).ConfigureAwait(false);
        await EnterAsync(session, cancellationToken).ConfigureAwait(false);
        var localRebootRequested = false;
        try
        {
            var prefix = remote ? "RT" : "AT";
            await ExpectOkAsync(session, prefix + "&F", cancellationToken).ConfigureAwait(false);
            await ExpectOkAsync(session, prefix + "&W", cancellationToken).ConfigureAwait(false);
            localRebootRequested = !remote;
            await session.WriteAsync(prefix + "Z\r\n", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!localRebootRequested)
            {
                await LeaveAsync(session).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Parses both real S-register lines (S1:SERIAL_SPEED=57) and older numeric fixtures.</summary>
    public static bool TryParseSetting(string line, out string register, out string name, out string value)
    {
        register = name = value = string.Empty;
        var match = Regex.Match(CleanResponse(line), @"^(S\d+):\s*(?:(\w+)\s*=\s*)?(\d+)\s*$", RegexOptions.CultureInvariant);
        if (!match.Success || !uint.TryParse(match.Groups[3].Value, out _))
        {
            return false;
        }
        register = match.Groups[1].Value;
        name = match.Groups[2].Success ? match.Groups[2].Value : register;
        value = match.Groups[3].Value;
        return true;
    }

    private static void CheckBaseline(IReadOnlyDictionary<string, string> changes, IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual, IReadOnlyDictionary<string, string> expectedNames, IReadOnlyDictionary<string, string> actualNames)
    {
        foreach (var key in changes.Keys)
            if (!expected.TryGetValue(key, out var old) || !actual.TryGetValue(key, out var current) || old != current ||
                expectedNames.GetValueOrDefault(key, key) != actualNames.GetValueOrDefault(key, key))
                throw new InvalidOperationException("A radio register value or meaning changed. Read both radios again; nothing written.");
    }

    private static string CleanResponse(string value)
    {
        if (value.Length > 512) throw new InvalidDataException("Radio response line exceeds its bound.");
        return Regex.Replace(value.Trim(), @"^\[\d{1,3}\]\s*", "", RegexOptions.CultureInvariant);
    }

    private static void Validate(IReadOnlyDictionary<string, string> settings)
    {
        foreach (var (key, value) in settings)
        {
            if (!Regex.IsMatch(key, "^S[1-9][0-9]{0,2}$") || !uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                throw new ArgumentException("SiK writes require a writable S-register and an unsigned integer value; S0 is read-only.");
            }
        }
    }

    private static async Task WriteSideAsync(IDirectSerialSession session, string prefix, IReadOnlyDictionary<string, string> settings, CancellationToken token)
    {
        foreach (var (key, value) in settings)
        {
            await ExpectOkAsync(session, $"{prefix}{key}={value}", token).ConfigureAwait(false);
            var query = $"{prefix}{key}?";
            await session.WriteAsync(query + "\r\n", token).ConfigureAwait(false);
            var response = await ResponseAsync(session, query, token).ConfigureAwait(false);
            if (!uint.TryParse(response, out var actual) || actual != uint.Parse(value, CultureInfo.InvariantCulture))
            {
                throw new IOException($"{prefix}{key} readback did not match. Earlier settings may have changed; read both radios before retrying.");
            }
        }
        if (settings.Count > 0)
        {
            await ExpectOkAsync(session, prefix + "&W", token).ConfigureAwait(false);
        }
    }

    private static async Task EnterAsync(IDirectSerialSession session, CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1100), token).ConfigureAwait(false);
        await session.WriteAsync("+++", token).ConfigureAwait(false);
        var response = await ResponseAsync(session, "+++", token).ConfigureAwait(false);
        if (response != "OK")
        {
            throw new IOException("The radio did not confirm SiK command mode.");
        }
    }

    private static async Task ExpectOkAsync(IDirectSerialSession session, string command, CancellationToken token)
    {
        await session.WriteAsync(command + "\r\n", token).ConfigureAwait(false);
        if (await ResponseAsync(session, command, token).ConfigureAwait(false) != "OK")
        {
            throw new IOException($"The radio rejected {command[..Math.Min(command.Length, 4)]}. Previous writes may remain; reconnect and read before retrying.");
        }
    }

    private static async Task<string> ResponseAsync(IDirectSerialSession session, string command, CancellationToken token)
    {
        for (var line = 0; line < 32; line++)
        {
            var response = CleanResponse(await session.ReadLineAsync(timeout, token).ConfigureAwait(false));
            if (response.Length != 0 && response != command)
            {
                return response;
            }
        }
        throw new IOException("Too many unexpected radio response lines.");
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadSettingsAsync(IDirectSerialSession session, string command,
        Dictionary<string, string> names, CancellationToken token)
    {
        await session.WriteAsync(command + "\r\n", token).ConfigureAwait(false);
        var result = new Dictionary<string, string>();
        for (var index = 0; index < 256; index++)
        {
            string line;
            try
            {
                line = CleanResponse(await session.ReadLineAsync(timeout, token).ConfigureAwait(false));
            }
            catch (TimeoutException) when (result.Count > 0)
            {
                // Standard SiK ATI5 ends at the final register, without an OK terminator.
                return result;
            }
            if (line == "OK")
            {
                if (result.Count > 0) return result;
                continue; // Do not mistake a preceding command's final OK for an empty settings list.
            }
            if (line == "ERROR")
            {
                throw new NotSupportedException("Radio settings request was rejected or remote radio unavailable.");
            }
            if (TryParseSetting(line, out var register, out var name, out var value))
            {
                result[register] = value;
                names[register] = name;
            }
        }
        throw new IOException("Radio settings response exceeded the bounded register list.");
    }

    private static async Task LeaveAsync(IDirectSerialSession session)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            await session.WriteAsync("ATO\r\n", timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // A requested reboot may have closed the port already.
        }
    }
}
