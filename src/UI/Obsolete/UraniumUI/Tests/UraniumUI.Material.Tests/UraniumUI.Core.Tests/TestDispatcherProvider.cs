using Microsoft.Maui.Dispatching;

namespace UraniumUI.Tests.Core;

/// <summary>
/// Supplies a deterministic dispatcher to MAUI controls hosted by the headless
/// net10.0 test process.
/// </summary>
internal sealed class TestDispatcherProvider : IDispatcherProvider
{
    public static TestDispatcherProvider Instance { get; } = new();

    public IDispatcher Dispatcher { get; } = new TestDispatcher();

    public IDispatcher GetForCurrentThread() => Dispatcher;

    private sealed class TestDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;

        public bool Dispatch(Action action)
        {
            action();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action)
        {
            action();
            return true;
        }

        public IDispatcherTimer CreateTimer() => new TestDispatcherTimer();
    }

    private sealed class TestDispatcherTimer : IDispatcherTimer
    {
        public TimeSpan Interval { get; set; }

        public bool IsRepeating { get; set; }

        public bool IsRunning { get; private set; }

        public event EventHandler? Tick;

        public void Start()
        {
            IsRunning = true;
            Tick?.Invoke(this, EventArgs.Empty);

            if (!IsRepeating)
            {
                IsRunning = false;
            }
        }

        public void Stop()
        {
            IsRunning = false;
        }
    }
}
