using System.Reflection;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dialogs.SubViews;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Protects nonblocking progress overlays and deferred cancellation semantics.</summary>
[Collection("Design preview")]
public sealed class ProgressOverlayTests
{
    /// <summary>The owner gets a handle before the overlay closes, without a native window.</summary>
    [Fact]
    public async Task ProgressReturnsHandleWhileOverlayIsStillOpen()
    {
        using var fixture = new Fixture();
        var requests = 0;
        var service = fixture.CreateService();
        using var handle = await service.DisplayProgressCancellableAsync(
            () => "Programming", new DialogOptions { RequestCancellation = () => requests++ }, TestContext.Current.CancellationToken);

        Assert.False(fixture.Completion.Task.IsCompleted);
        Assert.Equal(1, fixture.Dispatcher.OverlayRequests);

        // CloseAsync must request deferred cancellation, not dismiss the operation.
        await service.CloseAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, requests);
        handle.Dispose();
        handle.Dispose();
        Assert.Equal(1, requests);

        fixture.Completion.SetResult(null!);
        await fixture.CleanedUp.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        // Automatic cleanup unregisters the model and makes later handle disposal harmless.
        await service.CloseAsync(TestContext.Current.CancellationToken);
        handle.Dispose();
        Assert.Equal(1, requests);
    }

    /// <summary>A presentation failure is returned to the caller instead of a dead progress handle.</summary>
    [Fact]
    public async Task ImmediateOverlayFailureReachesCaller()
    {
        using var fixture = new Fixture();
        var failure = new InvalidOperationException("Overlay host unavailable");
        fixture.Completion.SetException(failure);

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.CreateService().DisplayProgressCancellableAsync(() => "Loading", new DialogOptions(), TestContext.Current.CancellationToken));

        Assert.Same(failure, observed);
    }

    /// <summary>Already cancelled calls do not start overlay presentation.</summary>
    [Fact]
    public async Task CancelledProgressDoesNotOpenOverlay()
    {
        using var fixture = new Fixture();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.CreateService().DisplayProgressCancellableAsync(
                () => "Loading", new DialogOptions(), new CancellationToken(true)));

        Assert.Equal(0, fixture.Dispatcher.OverlayRequests);
    }

    /// <summary>Cancel and close requests keep deferred progress visible until its owner ends it.</summary>
    [Fact]
    public void DeferredCancellationDoesNotRaiseDialogClose()
    {
        using var fixture = new Fixture();
        var requested = 0;
        using var model = new ProgressDialogViewModel(() => "Verifying", () => requested++);
        var closed = 0;
        model.RequestClose += (_, _) => closed++;

        Assert.Equal("Verifying", model.Message);
        Assert.True(model.CanRequestCancellation);
        model.CancelCommand.Execute(null);
        model.Close();

        Assert.Equal(2, requested);
        Assert.Equal(0, closed);
        Assert.False(model.Closed);
        model.Dispose();
        model.CancelCommand.Execute(null);
        Assert.Equal(2, requested);
    }

    /// <summary>Ordinary progress implements the overlay close contract.</summary>
    [Fact]
    public void OrdinaryProgressCanCloseWithoutCancellationCallback()
    {
        using var fixture = new Fixture();
        using var model = new ProgressDialogViewModel(() => "Loading");
        var closed = 0;
        model.RequestClose += (_, _) => closed++;

        Assert.False(model.CanRequestCancellation);
        model.Close();

        Assert.True(model.Closed);
        Assert.Equal(1, closed);
    }

    /// <summary>Completed indexing dismisses deferred progress without cancelling the replay that follows.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedProgressClosesUrsaOverlayWithoutCancellingFollowingWork(bool cancelFirst)
    {
        using var fixture = new Fixture();
        using var operation = new CancellationTokenSource();
        using var model = new ProgressDialogViewModel(() => "Indexing telemetry packets...", operation.Cancel);
        var overlay = new Ursa.Controls.CustomDialogControl { DataContext = model };
        var completion = overlay.ShowAsync<bool>();
        if (cancelFirst)
        {
            overlay.Close();
            Assert.True(operation.IsCancellationRequested);
            Assert.False(completion.IsCompleted);
        }

        model.Complete();
        await completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(model.Closed);
        Assert.Equal(cancelFirst, operation.IsCancellationRequested);

        // Late title-bar/owner close requests must not cancel the next operation.
        model.Complete();
        model.Close();
        Assert.Equal(cancelFirst, operation.IsCancellationRequested);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider services;
        private readonly IDisposable locatorScope;
        internal readonly OverlayDispatcher Dispatcher;
        internal readonly TaskCompletionSource<ProgressDialogViewModel> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource CleanedUp = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Fixture()
        {
            Dispatcher = new OverlayDispatcher(Completion, CleanedUp);
            services = new ServiceCollection().AddSingleton<IUiDispatcher>(Dispatcher)
                .AddSingleton(Substitute.For<IDomainEventHub>())
                .AddSingleton<ILogger<ViewModelBase>>(NullLogger<ViewModelBase>.Instance)
                .BuildServiceProvider();
            // Follow the existing parameter tests' isolated application-service scope.
            locatorScope = (IDisposable)typeof(AvaloniaLocator)
                .GetMethod("EnterScope", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
            var locator = typeof(AvaloniaLocator)
                .GetProperty("CurrentMutable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var binding = locator.GetType().GetMethod("Bind")!.MakeGenericMethod(typeof(Application)).Invoke(locator, null)!;
            binding.GetType().GetMethod("ToConstant")!.MakeGenericMethod(typeof(MissionPlanner.App.App))
                .Invoke(binding, [new MissionPlanner.App.App(services)]);
        }

        internal AvaloniaDialogService CreateService()
        {
            // An absent active window is valid: progress is presented through an overlay.
            return new AvaloniaDialogService(Dispatcher, Substitute.For<IWindowProvider>());
        }

        public void Dispose()
        {
            locatorScope.Dispose();
            services.Dispose();
        }
    }

    private sealed class OverlayDispatcher(
        TaskCompletionSource<ProgressDialogViewModel> completion,
        TaskCompletionSource cleanedUp) : IUiDispatcher
    {
        internal int OverlayRequests { get; private set; }

        public bool CheckAccess()
        {
            return true;
        }

        public void Dispatch(Action action)
        {
            action();
        }

        public T Dispatch<T>(Func<T> action)
        {
            return action();
        }

        public Task DispatchAsync(Action action)
        {
            action();
            if (completion.Task.IsCompleted)
            {
                cleanedUp.TrySetResult();
            }
            return Task.CompletedTask;
        }

        public Task DispatchAsync(Func<Task> action)
        {
            return action();
        }

        public Task<T> DispatchAsync<T>(Func<T> action)
        {
            return Task.FromResult(action());
        }

        public Task<T> DispatchAsync<T>(Func<Task<T>> action)
        {
            if (typeof(T) == typeof(ProgressDialogViewModel))
            {
                OverlayRequests++;
                // Simulate overlay lifetime without constructing platform UI controls.
                return (Task<T>)(object)completion.Task;
            }
            return action();
        }
    }
}
