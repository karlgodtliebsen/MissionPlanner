using System.Threading.Channels;

namespace MissionPlanner.Core.Setup.Advanced.Output;

/// <summary>Single-writer output pump with drop-newest overflow and a finite reconnect budget.</summary>
public sealed class BoundedOutputSession(IOutputSinkFactory factory, OutputEndpointOwners owners, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object sync = new();
    private CancellationTokenSource? cancellation;
    private Channel<byte[]>? queue;
    private Task worker = Task.CompletedTask;
    private OutputSessionSnapshot snapshot = new("Stopped", "No endpoint", 0, 0, 0, 0, null, null);
    /// <summary>Gets completion of the current writer lifetime.</summary>
    public Task Completion => worker;
    /// <summary>Gets a consistent diagnostic snapshot.</summary>
    public OutputSessionSnapshot Snapshot() { lock (sync) { return snapshot; } }

    /// <summary>Starts a bounded output lifetime and waits for its first connection or terminal failure.</summary>
    public async Task StartAsync(OutputEndpoint endpoint, OutputSessionOptions options, CancellationToken token)
    {
        var unavailable = endpoint.Validate() ?? factory.UnavailableReason(endpoint);
        if (unavailable is not null) { throw new InvalidOperationException(unavailable); }
        options.Validate();
        await gate.WaitAsync(token).ConfigureAwait(false);
        var created = false;
        try
        {
            if (cancellation is not null) { throw new InvalidOperationException("Stop this output session before starting another."); }
            var owner = owners.Acquire(endpoint.Identity);
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            created = true;
            queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(options.Capacity)
            {
                SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false
            });
            lock (sync) { snapshot = new("Starting", endpoint.Summary, 0, 0, 0, 0, null, null); }
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            worker = RunAsync(endpoint, options, queue, owner, started, cancellation.Token);
            await started.Task.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        catch
        {
            if (created)
            {
                cancellation?.Cancel();
                await worker.ConfigureAwait(false);
                cancellation?.Dispose();
                cancellation = null;
                queue = null;
            }
            throw;
        }
        finally { gate.Release(); }
    }

    /// <summary>Offers one bounded batch without waiting for the sink; overflow drops the new batch.</summary>
    public bool Offer(ReadOnlySpan<byte> bytes)
    {
        lock (sync)
        {
            if (bytes.Length is < 1 or > 65536 || snapshot.State is not ("Active" or "Reconnecting") || queue is null
                || !queue.Writer.TryWrite(bytes.ToArray()))
            {
                Drop(bytes.Length);
                return false;
            }
            return true;
        }
    }

    /// <summary>Records a batch intentionally omitted by a producer's loop guard.</summary>
    public void RecordDrop(int bytes) { lock (sync) { Drop(bytes); } }

    /// <summary>Cancels pending I/O and joins the writer before releasing its endpoint.</summary>
    public async Task StopAsync()
    {
        cancellation?.Cancel();
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (cancellation is null) { return; }
            queue?.Writer.TryComplete();
            await worker.ConfigureAwait(false);
            cancellation.Dispose();
            cancellation = null;
            queue = null;
        }
        finally { gate.Release(); }
    }

    private async Task RunAsync(OutputEndpoint endpoint, OutputSessionOptions options, Channel<byte[]> channel,
        IDisposable owner, TaskCompletionSource started, CancellationToken token)
    {
        IOutputSink? sink = null;
        var reconnects = 0;
        try
        {
            while (true)
            {
                try
                {
                    using var openTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.WriteTimeoutSeconds), clock);
                    using var opening = CancellationTokenSource.CreateLinkedTokenSource(token, openTimeout.Token);
                    sink = await factory.OpenAsync(endpoint, opening.Token).ConfigureAwait(false);
                    SetState("Active");
                    started.TrySetResult();
                    await foreach (var bytes in channel.Reader.ReadAllAsync(token).ConfigureAwait(false))
                    {
                        try
                        {
                            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.WriteTimeoutSeconds), clock);
                            using var writing = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
                            using var abort = writing.Token.Register(sink.Abort);
                            await sink.WriteAsync(bytes, writing.Token).ConfigureAwait(false);
                            lock (sync) { snapshot = snapshot with { Frames = snapshot.Frames + 1, Bytes = snapshot.Bytes + bytes.Length, LastWrite = clock.GetUtcNow() }; }
                        }
                        catch
                        {
                            lock (sync) { Drop(bytes.Length); }
                            throw;
                        }
                    }
                    break;
                }
                catch (Exception) when (!token.IsCancellationRequested && reconnects < options.ReconnectAttempts)
                {
                    if (sink is not null) { sink.Abort(); await sink.DisposeAsync().ConfigureAwait(false); sink = null; }
                    reconnects++;
                    SetState("Reconnecting", "Output interrupted; retrying within the configured attempt limit.");
                    await Task.Delay(TimeSpan.FromSeconds(options.ReconnectDelaySeconds), clock, token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            SetState("Faulted", "Output failed or timed out. Check endpoint, permissions and connection; retry budget exhausted.");
            started.TrySetException(new IOException("The output endpoint could not be opened."));
        }
        finally
        {
            started.TrySetCanceled();
            channel.Writer.TryComplete();
            if (Snapshot().State != "Faulted") { SetState("Stopping"); }
            try
            {
                if (sink is not null) { sink.Abort(); await sink.DisposeAsync().ConfigureAwait(false); }
            }
            catch (Exception) { SetState("Faulted", "Output handle cleanup failed."); }
            finally
            {
                while (channel.Reader.TryRead(out var pending)) { lock (sync) { Drop(pending.Length); } }
                owner.Dispose();
                if (Snapshot().State != "Faulted") { SetState("Stopped"); }
            }
        }
    }

    private void SetState(string state, string? error = null) { lock (sync) { snapshot = snapshot with { State = state, Error = error }; } }
    private void Drop(int bytes) => snapshot = snapshot with { DroppedFrames = snapshot.DroppedFrames + 1, DroppedBytes = snapshot.DroppedBytes + Math.Max(0, bytes) };
}
