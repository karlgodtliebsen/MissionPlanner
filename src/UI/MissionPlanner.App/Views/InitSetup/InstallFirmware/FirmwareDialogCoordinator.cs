using MissionPlanner.App.Utilities.Dispatching;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Keeps firmware progress and operator prompts mutually exclusive.</summary>
public sealed class FirmwareDialogCoordinator(IUiDispatcher dispatcher)
{
    private Session? current;

    public Task<IDisposable> BeginAsync(Func<Task<IDisposable>> showProgress, bool deferUntilConfirmed, CancellationToken token) =>
        dispatcher.DispatchAsync<IDisposable>(async () =>
        {
            token.ThrowIfCancellationRequested();
            if (current is not null) throw new InvalidOperationException("A firmware dialog session is already active.");
            var session = new Session(this, showProgress, token);
            current = session;
            try
            {
                if (!deferUntilConfirmed) await ShowAsync(session);
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        });

    public Task<bool> ConfirmAsync(Func<Task<bool>> confirm, CancellationToken token) =>
        dispatcher.DispatchAsync(async () =>
        {
            token.ThrowIfCancellationRequested();
            var session = current;
            session?.CloseProgress();
            var accepted = await confirm();
            // The prompt must have fully closed before opening another modal.
            token.ThrowIfCancellationRequested();
            if (accepted && session is not null && ReferenceEquals(current, session))
                await ShowAsync(session);
            return accepted;
        });

    private async Task ShowAsync(Session session)
    {
        session.Token.ThrowIfCancellationRequested();
        var handle = await session.ShowProgress();
        if (ReferenceEquals(current, session) && !session.Token.IsCancellationRequested)
            session.Handle = handle;
        else
            handle.Dispose();
    }

    private void EndSession(Session session) => dispatcher.Dispatch(() =>
    {
        if (ReferenceEquals(current, session)) current = null;
        session.CloseProgress();
    });

    private sealed class Session(FirmwareDialogCoordinator owner, Func<Task<IDisposable>> showProgress, CancellationToken token) : IDisposable
    {
        public Func<Task<IDisposable>> ShowProgress { get; } = showProgress;
        public CancellationToken Token { get; } = token;
        public IDisposable? Handle { get; set; }
        public void CloseProgress()
        {
            Handle?.Dispose();
            Handle = null;
        }
        public void Dispose() => owner.EndSession(this);
    }
}
