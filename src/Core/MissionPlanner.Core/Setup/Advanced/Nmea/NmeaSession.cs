using System.Text;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Setup.Advanced.Nmea;

/// <summary>Schedules latest-state NMEA batches on the shared output session, independently of telemetry rate.</summary>
public sealed class NmeaSession(IActiveVehicleContext active, BoundedOutputSession output, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? cancellation;
    private Task worker = Task.CompletedTask;
    private NmeaBatch preview = new("", 0, false, "NMEA stopped.");
    private long scheduledSentences;
    private int sentencesPerBatch;
    /// <summary>Gets the latest complete sentence set without exposing an unbounded history.</summary>
    public NmeaBatch Preview => Volatile.Read(ref preview);
    /// <summary>Gets the number of scheduled sentences, including batches later dropped by the endpoint.</summary>
    public long ScheduledSentences => Interlocked.Read(ref scheduledSentences);
    /// <summary>Gets sentences in successfully written complete batches.</summary>
    public long WrittenSentences => output.Snapshot().Frames * sentencesPerBatch;
    /// <summary>Gets sentences in dropped or partially delivered batches.</summary>
    public long DroppedSentences => output.Snapshot().DroppedFrames * sentencesPerBatch;
    /// <summary>Gets shared output status and counters.</summary>
    public OutputSessionSnapshot Snapshot() => output.Snapshot();

    /// <summary>Starts one monotonic timer; disconnect or endpoint failure ends the session.</summary>
    public async Task StartAsync(OutputEndpoint endpoint, OutputSessionOptions outputOptions, NmeaOptions options, CancellationToken token)
    {
        options.Validate();
        if (!active.IsOnline || active.VehicleId is null) { throw new InvalidOperationException("Select a connected vehicle for NMEA output."); }
        await gate.WaitAsync(token).ConfigureAwait(false);
        var created = false;
        try
        {
            if (cancellation is not null) { throw new InvalidOperationException("Stop NMEA before restarting it."); }
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, active.ConnectionCancellationToken);
            created = true;
            scheduledSentences = 0;
            sentencesPerBatch = (options.Gga ? 1 : 0) + (options.Rmc ? 1 : 0);
            await output.StartAsync(endpoint, outputOptions, cancellation.Token).ConfigureAwait(false);
            worker = RunAsync(options, cancellation.Token);
        }
        catch
        {
            if (created)
            {
                cancellation?.Cancel();
                await output.StopAsync().ConfigureAwait(false);
                cancellation?.Dispose();
                cancellation = null;
            }
            throw;
        }
        finally { gate.Release(); }
    }

    /// <summary>Stops and joins the scheduler and the shared writer.</summary>
    public async Task StopAsync()
    {
        cancellation?.Cancel();
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (cancellation is null) { return; }
            await worker.ConfigureAwait(false);
            await output.StopAsync().ConfigureAwait(false);
            cancellation.Dispose();
            cancellation = null;
            Volatile.Write(ref preview, new("", 0, false, "NMEA stopped."));
        }
        finally { gate.Release(); }
    }

    private async Task RunAsync(NmeaOptions options, CancellationToken token)
    {
        using var scheduling = CancellationTokenSource.CreateLinkedTokenSource(token);
        async Task ScheduleAsync()
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1d / options.RateHz), clock);
                while (await timer.WaitForNextTickAsync(scheduling.Token).ConfigureAwait(false))
                {
                    var batch = NmeaFormatter.Format(active.IsOnline ? active.State : null, clock.GetUtcNow(), options);
                    Volatile.Write(ref preview, batch);
                    Interlocked.Add(ref scheduledSentences, batch.SentenceCount);
                    output.Offer(Encoding.ASCII.GetBytes(batch.Text));
                }
            }
            catch (OperationCanceledException) when (scheduling.IsCancellationRequested) { }
        }
        var schedule = ScheduleAsync();
        try { await Task.WhenAny(schedule, output.Completion).ConfigureAwait(false); }
        finally
        {
            scheduling.Cancel();
            await schedule.ConfigureAwait(false);
            await output.StopAsync().ConfigureAwait(false);
            Volatile.Write(ref preview, Preview with { ValidFix = false, Status = "NMEA output stopped; displayed sentences are historical." });
        }
    }
}
