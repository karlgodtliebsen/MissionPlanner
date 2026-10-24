using System.Globalization;
using System.Text;
using System.Threading.Channels;

namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

/// <summary>Owns a LAWICEL-compatible classic SLCAN serial connection and closes its CAN channel on disposal.</summary>
public sealed class SlcanFrameLink(IDirectSerialSessionFactory ports, DroneCanConnectionOptions options) : IDroneCanFrameLink
{
    /// <summary>Gets standard S0–S8 CAN bit rates.</summary>
    public static IReadOnlyList<int> BitRates { get; } = [10_000, 20_000, 50_000, 100_000, 125_000, 250_000, 500_000, 800_000, 1_000_000];
    private readonly Channel<DroneCanFrame> frames = Channel.CreateBounded<DroneCanFrame>(2048);
    private readonly SemaphoreSlim writes = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private IDirectSerialSession? port;
    private Task? reader;
    private TaskCompletionSource<bool>? acknowledgement;
    private Exception? failure;

    /// <inheritdoc />
    public async Task OpenAsync(CancellationToken token)
    {
        options.Validate(DroneCanTransportKind.DirectSlcan);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(8));
        port = await ports.OpenAsync(options.Port, options.Baud, timeout.Token).ConfigureAwait(false);
        reader = ReadLoopAsync(lifetime.Token);
        try
        {
            await CommandAsync("C", timeout.Token).ConfigureAwait(false);
            await CommandAsync($"S{BitRates.ToList().IndexOf(options.CanBitRate)}", timeout.Token).ConfigureAwait(false);
            await CommandAsync("O", timeout.Token).ConfigureAwait(false);
        }
        catch { await DisposeAsync().ConfigureAwait(false); throw; }
    }
    /// <inheritdoc />
    public void Validate()
    {
        if (failure is not null) throw new IOException("The SLCAN connection failed.", failure);
        if (port is null || lifetime.IsCancellationRequested) throw new InvalidOperationException("The SLCAN adapter is disconnected.");
    }
    /// <inheritdoc />
    public async Task<DroneCanFrame> ReadAsync(CancellationToken token) => await frames.Reader.ReadAsync(token).ConfigureAwait(false);
    /// <inheritdoc />
    public Task SendAsync(DroneCanFrame frame, CancellationToken token)
    {
        Validate();
        if (frame.Identifier > 0x1FFFFFFF || frame.Data.Length > 8) throw new ArgumentException("Classic extended CAN frames are required.");
        return CommandAsync($"T{frame.Identifier:X8}{frame.Data.Length:X1}{Convert.ToHexString(frame.Data)}", token);
    }
    private async Task CommandAsync(string command, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(2));
        await writes.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var ack = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref acknowledgement, ack);
            await port!.WriteAsync(command + "\r", timeout.Token).ConfigureAwait(false);
            await ack.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex; frames.Writer.TryComplete(ex); throw;
        }
        finally { Volatile.Write(ref acknowledgement, null); writes.Release(); }
    }
    private async Task ReadLoopAsync(CancellationToken token)
    {
        var buffer = new byte[512]; var line = new StringBuilder();
        try
        {
            while (true)
            {
                var count = await port!.ReadAsync(buffer, token).ConfigureAwait(false);
                if (count == 0) throw new EndOfStreamException("SLCAN adapter closed.");
                for (var i = 0; i < count; i++)
                {
                    var value = buffer[i];
                    if (value == 7) { Volatile.Read(ref acknowledgement)?.TrySetException(new IOException("The SLCAN adapter rejected a command or transmission.")); line.Clear(); }
                    else if (value == 13)
                    {
                        var text = line.ToString(); line.Clear();
                        if (text is "" or "z" or "Z") Volatile.Read(ref acknowledgement)?.TrySetResult(true);
                        else if (Parse(text) is { } frame && !frames.Writer.TryWrite(frame)) throw new IOException("SLCAN receive overflow; reconnect at a suitable rate.");
                    }
                    else if (value != 10)
                    {
                        if (value < 32 || value > 126 || line.Length >= 64) throw new InvalidDataException("Invalid or oversized SLCAN line.");
                        line.Append((char)value);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { failure = ex; Volatile.Read(ref acknowledgement)?.TrySetException(ex); frames.Writer.TryComplete(ex); }
        finally { frames.Writer.TryComplete(); }
    }
    /// <summary>Parses an extended data frame; standard IDs, remote frames and diagnostic lines are ignored.</summary>
    public static DroneCanFrame? Parse(string line)
    {
        if (!line.StartsWith('T')) return null;
        if (line.Length < 10 || !uint.TryParse(line.AsSpan(1, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id) || id > 0x1FFFFFFF || line[9] is < '0' or > '8')
            throw new InvalidDataException("Invalid SLCAN CAN identifier or length.");
        var length = line[9] - '0';
        if (line.Length != 10 + length * 2 && line.Length != 14 + length * 2) throw new InvalidDataException("Invalid SLCAN payload length.");
        try { return new(id, Convert.FromHexString(line.Substring(10, length * 2))); }
        catch (FormatException ex) { throw new InvalidDataException("Invalid SLCAN data.", ex); }
    }
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (port is null) return;
        try { if (!lifetime.IsCancellationRequested && failure is null) await CommandAsync("C", CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidOperationException) { }
        lifetime.Cancel();
        if (reader is not null) await reader.ConfigureAwait(false);
        var current = port; port = null;
        await current.DisposeAsync().ConfigureAwait(false);
    }
}
