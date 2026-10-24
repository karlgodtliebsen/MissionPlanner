using System.Text;
using System.Threading.Channels;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Setup.Advanced.Scripting;

/// <summary>Selects the installed Lua applet or the older firmware's native FTP console.</summary>
public enum ScriptingConsoleMode
{
    /// <summary>Uses the official Lua REPL applet with PORT_IDX=nil and its required modules installed.</summary>
    LuaApplet,
    /// <summary>Uses MAV_CMD_SCRIPTING start/stop and the older firmware's repl/in and repl/out files.</summary>
    LegacyNative
}

/// <summary>Owns an explicitly opened, connection-bound scripting console with no command replay.</summary>
public sealed class ScriptingConsoleService(IActiveVehicleContext active, IVehicleRegistry registry,
    IVehicleParameterRegistry parameters, IVehicleConnectionSession sessions, IEventHub events,
    IMavLinkConnection connection, IMavLinkWireMessageEncoder encoder, IVehicleOperationGate operations)
{
    private readonly SemaphoreSlim io = new(1, 1);
    private int running;
    private VehicleSession? target;
    private CancellationToken boundary;
    private CancellationToken runToken;
    private IVehicleFileSystemService? files;
    private string directory = string.Empty;
    private ScriptingConsoleMode mode;

    /// <summary>Gets whether input can be sent; this does not establish that the applet is installed.</summary>
    public bool CanSend => target is not null;

    /// <summary>Runs until cancellation, disconnect, arming or target change; output is delivered in bounded chunks.</summary>
    public async Task RunAsync(ScriptingConsoleMode selectedMode, bool confirmed, IProgress<string> output, CancellationToken cancellationToken)
    {
        if (!confirmed || !Enum.IsDefined(selectedMode)) throw new InvalidOperationException("Review the scripting prerequisites and confirm before opening a console.");
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) throw new InvalidOperationException("A console is already open.");
        VehicleSession? captured = null;
        IDisposable? lease = null;
        var nativeStarted = false;
        try
        {
            captured = RequireSession();
            if (parameters.GetParameter(captured.Id, "SCR_ENABLE")?.Value != 1) throw new InvalidOperationException("This vehicle must report SCR_ENABLE=1. Configure scripting separately before opening the console.");
            if (!operations.TryAcquire(captured.Id, "Onboard scripting console", out lease)) throw new InvalidOperationException("Another vehicle operation is in progress.");
            boundary = active.ConnectionCancellationToken;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, boundary);
            runToken = lifetime.Token;
            mode = selectedMode;
            var inbox = Channel.CreateBounded<SerialControlMessage>(64);
            var overflow = 0;
            using var subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
            {
                if (message is SerialControlMessage serial && SameSource(serial, captured) && serial.Device == 10 && (serial.Flags & 1) != 0 &&
                    serial.TargetSystem is 0 or 255 && serial.TargetComponent is 0 or 190 && serial.Count <= 70 && serial.Data.Length >= serial.Count &&
                    !inbox.Writer.TryWrite(serial)) Interlocked.Exchange(ref overflow, 1);
                return Task.CompletedTask;
            });
            if (mode == ScriptingConsoleMode.LegacyNative)
            {
                await NativeCommandAsync(captured, false, lifetime.Token).ConfigureAwait(false);
                nativeStarted = true;
                files = sessions.CreateMavFtpConnection() ?? throw new InvalidOperationException("MAVFTP is unavailable on this connection.");
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                startup.CancelAfter(TimeSpan.FromSeconds(12));
                directory = await FindDirectoryAsync(captured, startup.Token).ConfigureAwait(false);
            }
            else
            {
                // An empty frame selects the reply channel without submitting a newline or executing Lua.
                await SendPacketAsync(captured, ReadOnlyMemory<byte>.Empty, lifetime.Token).ConfigureAwait(false);
                output.Report("Lua applet channel opened. Waiting for output; applet availability is not yet verified.\n");
            }
            target = captured;
            var decoder = Encoding.UTF8.GetDecoder();
            uint offset = 0;
            while (true)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                RequireCaptured(captured);
                if (Interlocked.Exchange(ref overflow, 0) != 0) throw new IOException("Console output exceeded the receive buffer. Output is incomplete; reopen explicitly.");
                if (mode == ScriptingConsoleMode.LegacyNative)
                {
                    await io.WaitAsync(lifetime.Token).ConfigureAwait(false);
                    try
                    {
                        using var read = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                        read.CancelAfter(TimeSpan.FromSeconds(5));
                        var range = await files!.ReadRangeAsync(captured.Id, directory + "out", offset, 8192, read.Token).ConfigureAwait(false);
                        if (range.Size < offset) { offset = 0; decoder.Reset(); output.Report("\n[Console output file restarted]\n"); }
                        else { offset += (uint)range.Data.Length; Report(range.Data); }
                    }
                    finally { io.Release(); }
                }
                else
                {
                    while (inbox.Reader.TryRead(out var packet)) Report(packet.Data.AsSpan(0, packet.Count));
                }
                await Task.Delay(250, lifetime.Token).ConfigureAwait(false);
            }
            void Report(ReadOnlySpan<byte> bytes)
            {
                var text = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
                var count = decoder.GetChars(bytes, text, false);
                if (count > 0) output.Report(new string(text, 0, count));
            }
        }
        finally
        {
            target = null;
            // Wait for an in-flight send before releasing its FTP client and operation lease.
            await io.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (nativeStarted && captured is not null && !boundary.IsCancellationRequested && active.VehicleId == captured.Id &&
                    active.ConnectionCancellationToken == boundary && ReferenceEquals(registry.GetRequired(captured.Id), captured))
                {
                    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    try { await NativeCommandAsync(captured, true, stop.Token).ConfigureAwait(false); }
                    catch (Exception ex) when (ex is IOException or OperationCanceledException) { output.Report("\nNative console stop was not acknowledged. Check its state before reopening.\n"); }
                }
                if (files is not null) await files.DisposeAsync().ConfigureAwait(false);
            }
            finally { files = null; lease?.Dispose(); io.Release(); Volatile.Write(ref running, 0); }
        }
    }

    /// <summary>Sends one explicitly entered line once; transport failure may leave its execution outcome unknown.</summary>
    public async Task SendAsync(string line, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Any(c => char.IsControl(c))) throw new ArgumentException("Enter a single line without control characters.");
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        if (bytes.Length > 4096) throw new ArgumentException("A console line may contain at most 4096 UTF-8 bytes including its newline.");
        var captured = target ?? throw new InvalidOperationException("Open the console first.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, boundary, runToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await io.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            RequireCaptured(captured);
            if (!ReferenceEquals(target, captured)) throw new InvalidOperationException("The console has closed.");
            if (mode == ScriptingConsoleMode.LegacyNative) await files!.AppendAsync(captured.Id, directory + "in", bytes, timeout.Token).ConfigureAwait(false);
            else for (var position = 0; position < bytes.Length; position += 70)
            {
                RequireCaptured(captured);
                await SendPacketAsync(captured, bytes.AsMemory(position, Math.Min(70, bytes.Length - position)), timeout.Token).ConfigureAwait(false);
                if (position + 70 < bytes.Length) await Task.Delay(50, timeout.Token).ConfigureAwait(false);
            }
        }
        finally { io.Release(); }
    }

    private async Task<string> FindDirectoryAsync(VehicleSession session, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            RequireCaptured(session);
            foreach (var path in new[] { "/repl/", "/APM/repl/" })
            {
                try
                {
                    await files!.GetFileInfoAsync(session.Id, path + "in", token).ConfigureAwait(false);
                    await files.GetFileInfoAsync(session.Id, path + "out", token).ConfigureAwait(false);
                    return path;
                }
                catch (MissionPlanner.MavLink.MavFtp.MavFtpRemoteException exception)
                    when (exception.Error == MissionPlanner.MavLink.MavFtp.MavFtpNakError.FileNotFound) { }
            }
            // Native REPL file creation follows the command acknowledgement asynchronously.
            await Task.Delay(200, token).ConfigureAwait(false);
        }
    }

    private async Task NativeCommandAsync(VehicleSession session, bool stop, CancellationToken token)
    {
        var reply = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = events.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is CommandAckMessage ack && SameSource(ack, session) && ack.Command == 42701 && ack.Result != 5 &&
                ack.TargetSystemId is null or 0 or 255 && ack.TargetComponentId is null or 0 or 190) reply.TrySetResult(ack.Result);
            return Task.CompletedTask;
        });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var command = new CommandIntMessage(255, 190, session.EndPoint, session.Id.SystemId, session.Id.ComponentId, 0, 42701, 0, 0, stop ? 1 : 0, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow);
        await connection.SendRawAsync(encoder.Encode(command), session.EndPoint, timeout.Token).ConfigureAwait(false);
        var result = await reply.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        if (result != 0) throw new IOException($"Firmware rejected the native REPL command (result {result}). Current firmware may require the Lua REPL applet instead.");
    }

    private async Task SendPacketAsync(VehicleSession session, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        var payload = new byte[70];
        bytes.CopyTo(payload);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        await connection.SendRawAsync(encoder.Encode(new SerialControlMessage(255, 190, session.EndPoint, 10, 2, 0, 0,
            (byte)bytes.Length, payload, session.Id.SystemId, session.Id.ComponentId, DateTimeOffset.UtcNow)), session.EndPoint, timeout.Token).ConfigureAwait(false);
    }

    private VehicleSession RequireSession()
    {
        if (!active.IsOnline || active.VehicleId is not { } id || active.State?.IsArmed != false || active.State.Autopilot != 3 ||
            active.State.Connection.LastHeartbeatAt > DateTimeOffset.UtcNow ||
            DateTimeOffset.UtcNow - active.State.Connection.LastHeartbeatAt > TimeSpan.FromSeconds(3))
            throw new InvalidOperationException("A freshly connected, disarmed ArduPilot vehicle is required for the scripting console.");
        return registry.GetRequired(id) ?? throw new InvalidOperationException("Vehicle session unavailable.");
    }

    private void RequireCaptured(VehicleSession session)
    {
        if (!ReferenceEquals(RequireSession(), session) || boundary.IsCancellationRequested || boundary != active.ConnectionCancellationToken)
            throw new InvalidOperationException("The console target or connection changed. Reopen explicitly.");
    }

    private static bool SameSource(MavLinkMessage message, VehicleSession session) =>
        message.SystemId == session.Id.SystemId && message.ComponentId == session.Id.ComponentId && Equals(message.EndPoint, session.EndPoint);
}
