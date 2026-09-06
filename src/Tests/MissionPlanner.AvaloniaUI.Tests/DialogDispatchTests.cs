using Avalonia.Controls;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using NSubstitute;
using Ursa.Controls;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class DialogDispatchTests
{
    [Fact]
    public async Task WorkerConfirmationQueuesUiWorkBeforeConstructingItsViewModel()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.DispatchAsync(Arg.Any<Func<Task<bool>>>()).Returns(completion.Task);
        var service = new AvaloniaDialogService(dispatcher, Substitute.For<IWindowProvider>());

        var request = Task.Run(() => service.ConfirmAsync(new OverlayDialogOptions(), "Flash custom firmware?"));
        // No Avalonia application exists in this test: constructing a dialog
        // before dispatch would fail. The caller must await the queued result.
        completion.SetResult(true);
        Assert.True(await request);
        await dispatcher.Received(1).DispatchAsync(Arg.Any<Func<Task<bool>>>());
    }

    [Fact]
    public async Task WorkerPromptQueuesUiWorkBeforeConstructingItsViewModel()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.DispatchAsync(Arg.Any<Func<Task<string?>>>()).Returns(Task.FromResult<string?>("CONFIRM"));
        var service = new AvaloniaDialogService(dispatcher, Substitute.For<IWindowProvider>());
        Assert.Equal("CONFIRM", await Task.Run(() => service.PromptAsync(new OverlayDialogOptions(), "Confirm")));
        await dispatcher.Received(1).DispatchAsync(Arg.Any<Func<Task<string?>>>());
    }

    [Fact]
    public async Task CancelledOverlayDoesNotTouchItsModelOrCreateControls()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.DispatchAsync(Arg.Any<Func<Task<DialogViewModelBase>>>())
            .Returns(call => call.Arg<Func<Task<DialogViewModelBase>>>()!());
        var service = new AvaloniaDialogService(dispatcher, Substitute.For<IWindowProvider>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ShowOverlayDialogAsync<UserControl, DialogViewModelBase>(null!, new OverlayDialogOptions(),
                cancellationToken: new CancellationToken(true)));
        await dispatcher.Received(1).DispatchAsync(Arg.Any<Func<Task<DialogViewModelBase>>>());
    }
}
