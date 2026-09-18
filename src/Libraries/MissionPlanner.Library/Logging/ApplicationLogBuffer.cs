using System.Collections.ObjectModel;
using Serilog.Core;
using Serilog.Events;

namespace MissionPlanner.Library.Logging;

/// <summary>A structured diagnostic captured before file formatting.</summary>
/// <param name="Sequence">Monotonic buffer sequence.</param>
/// <param name="Timestamp">Event timestamp.</param>
/// <param name="Level">Serilog severity.</param>
/// <param name="MessageTemplate">Original template.</param>
/// <param name="RenderedMessage">Formatted message.</param>
/// <param name="SourceContext">Logger category.</param>
/// <param name="Exception">Original exception, if supplied.</param>
/// <param name="Properties">Structured Serilog values.</param>
public sealed record ApplicationLogEntry(long Sequence, DateTimeOffset Timestamp, LogEventLevel Level,
    string MessageTemplate, string RenderedMessage, string? SourceContext, Exception? Exception,
    IReadOnlyDictionary<string, LogEventPropertyValue> Properties);

/// <summary>A thread-safe bounded structured sink with coalesced asynchronous change notifications.</summary>
public sealed class ApplicationLogBuffer : ILogEventSink
{
    private readonly object gate = new();
    private readonly Queue<ApplicationLogEntry> entries = new();
    private long sequence;
    private int notificationPending;

    /// <summary>Creates a buffer retaining at most the specified number of events.</summary>
    public ApplicationLogBuffer(int capacity = 5000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    /// <summary>Maximum retained event count.</summary>
    public int Capacity { get; }

    /// <summary>Current retained event count.</summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return entries.Count;
            }
        }
    }

    /// <summary>Signals that a new snapshot may be available. Delivered off the logging call; exceptions are isolated.</summary>
    public event EventHandler? Changed;

    /// <summary>Copies retained events in insertion order, optionally after a sequence cursor.</summary>
    public IReadOnlyList<ApplicationLogEntry> Snapshot(long afterSequence = 0)
    {
        lock (gate)
        {
            return entries.Where(entry => entry.Sequence > afterSequence).ToArray();
        }
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        var properties = new ReadOnlyDictionary<string, LogEventPropertyValue>(
            new Dictionary<string, LogEventPropertyValue>(logEvent.Properties));
        var source = logEvent.Properties.TryGetValue("SourceContext", out var value)
            ? (value as ScalarValue)?.Value?.ToString() : null;
        lock (gate)
        {
            entries.Enqueue(new(++sequence, logEvent.Timestamp, logEvent.Level, logEvent.MessageTemplate.Text,
                logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture), source, logEvent.Exception, properties));
            while (entries.Count > Capacity)
            {
                entries.Dequeue();
            }
        }

        if (Changed is not null && Interlocked.CompareExchange(ref notificationPending, 1, 0) == 0)
        {
            ThreadPool.QueueUserWorkItem(_ => Notify());
        }
    }

    private void Notify()
    {
        var observedSequence = Interlocked.Read(ref sequence);
        try
        {
            var subscribers = Changed;
            if (subscribers is null)
            {
                return;
            }

            foreach (EventHandler subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber(this, EventArgs.Empty);
                }
                catch
                {
                    // Diagnostics consumers must never break producers or other consumers.
                }
            }
        }
        finally
        {
            Volatile.Write(ref notificationPending, 0);
            if (Interlocked.Read(ref sequence) != observedSequence && Changed is not null &&
                Interlocked.CompareExchange(ref notificationPending, 1, 0) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => Notify());
            }
        }
    }
}
