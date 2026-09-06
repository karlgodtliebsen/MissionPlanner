using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class FirmwareDialogCoordinatorTests
{
    private static FirmwareDialogCoordinator Create()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.DispatchAsync(Arg.Any<Func<Task<IDisposable>>>()).Returns(c => c.Arg<Func<Task<IDisposable>>>()!());
        dispatcher.DispatchAsync(Arg.Any<Func<Task<bool>>>()).Returns(c => c.Arg<Func<Task<bool>>>()!());
        dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(c => c.Arg<Action>()!());
        return new FirmwareDialogCoordinator(dispatcher);
    }

    [Fact]
    public async Task ConfirmationClosesBeforeProgressOpensAndLaterPromptSuspendsProgress()
    {
        var coordinator = Create();
        var events = new List<string>();
        Task<IDisposable> Show()
        {
            events.Add("progress opened");
            return Task.FromResult<IDisposable>(new Handle(() => events.Add("progress closed")));
        }
        using (await coordinator.BeginAsync(Show, true, CancellationToken.None))
        {
            Assert.Empty(events);
            await coordinator.ConfirmAsync(() =>
            {
                events.Add("confirmation closed");
                return Task.FromResult(true);
            }, CancellationToken.None);
            Assert.Equal(new[] { "confirmation closed", "progress opened" }, events);
            await coordinator.ConfirmAsync(() =>
            {
                Assert.Equal("progress closed", events.Last());
                events.Add("manual prompt closed");
                return Task.FromResult(true);
            }, CancellationToken.None);
        }
        Assert.Equal(new[] { "confirmation closed", "progress opened", "progress closed", "manual prompt closed", "progress opened", "progress closed" }, events);
    }

    [Fact]
    public async Task DeclinedConfirmationNeverOpensProgress()
    {
        var coordinator = Create();
        using var session = await coordinator.BeginAsync(() => throw new InvalidOperationException("Must not show progress"), true, CancellationToken.None);
        Assert.False(await coordinator.ConfirmAsync(() => Task.FromResult(false), CancellationToken.None));
    }

    [Fact]
    public async Task CompletedOperationDoesNotReopenProgressAfterPendingConfirmation()
    {
        var coordinator = Create();
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = await coordinator.BeginAsync(() => throw new InvalidOperationException("Operation already ended"), true, CancellationToken.None);
        var pending = coordinator.ConfirmAsync(() => answer.Task, CancellationToken.None);
        session.Dispose();
        answer.SetResult(true);
        Assert.True(await pending);
    }

    [Fact]
    public async Task CancellationDuringConfirmationDoesNotOpenProgress()
    {
        var coordinator = Create();
        using var cancellation = new CancellationTokenSource();
        using var session = await coordinator.BeginAsync(() => throw new InvalidOperationException("Must not show progress"), true, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.ConfirmAsync(() =>
        {
            cancellation.Cancel();
            return Task.FromResult(true);
        }, cancellation.Token));
    }

    [Fact]
    public async Task RefreshProgressOpensWithoutConfirmationAndClosesWhenOperationEnds()
    {
        var coordinator = Create();
        var events = new List<string>();
        using (await coordinator.BeginAsync(() =>
        {
            events.Add("opened");
            return Task.FromResult<IDisposable>(new Handle(() => events.Add("closed")));
        }, false, CancellationToken.None))
        {
            Assert.Equal(new[] { "opened" }, events);
        }
        Assert.Equal(new[] { "opened", "closed" }, events);
    }

    private sealed class Handle(Action close) : IDisposable
    {
        public void Dispose() => close();
    }
}
