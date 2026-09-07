using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.Advanced.Signing;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Core.Setup.Advanced.Signing;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.Browser;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using NSubstitute;
using Ursa.Controls;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class SigningPageTests
{
    [Fact]
    public async Task BrowserSigningStateIsSessionOnlyAndIsNeverActivatedByLoading()
    {
        var repository = new SigningKeyRepository(new BrowserPlannerSecretStore());
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        await repository.PrepareAsync(key, 2, 100, TestContext.Current.CancellationToken);
        using var loaded = await repository.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(key, loaded!.Key);
        Assert.Null(await new SigningKeyRepository(new BrowserPlannerSecretStore()).LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelledConfirmationNeverAccessesConnectionAndPageClearsKeyOnTenCycles()
    {
        var events = Substitute.For<IDomainEventHub>();
        var dispatcher = new InlineDispatcher();
        var repository = new SigningKeyRepository(new BrowserPlannerSecretStore());
        var dialogs = Substitute.For<IDialogService>();
        var accept = true;
        dialogs.ConfirmAsync(Arg.Any<OverlayDialogOptions>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => accept);
        using var keys = new SigningKeyViewModel(repository, Substitute.For<IFileOpenService>(), Substitute.For<IFileSaveService>(),
            dialogs, NullLogger<SigningKeyViewModel>.Instance, dispatcher, events);
        var connection = Substitute.For<IVehicleConnectionSession>();
        var setup = new SigningSetupService(connection, Substitute.For<IActiveVehicleContext>(), Substitute.For<IVehicleRegistry>(),
            new VehicleOperationGate(), Substitute.For<IMavLinkWireMessageEncoder>(), repository, TimeProvider.System, new());
        using var model = new SigningViewModel(keys, setup, new AdvancedPlatformCapabilitySource(), dialogs, TimeProvider.System,
            NullLogger<SigningViewModel>.Instance, dispatcher, events);
        for (var cycle = 0; cycle < 10; cycle++)
        {
            await model.ActivateAsync();
            await model.ActivateAsync();
            accept = true;
            await keys.GenerateCommand.ExecuteAsync(null);
            Assert.Equal(16, keys.Fingerprint.Length);
            Assert.Contains(keys.Fingerprint, model.Message);
            accept = false;
            await model.ConfigureCommand.ExecuteAsync(null);
            Assert.Contains("before transmission", model.Message);
            await model.DeactivateAsync();
            Assert.Throws<InvalidOperationException>(() => keys.CopyKey());
            var message = model.Message;
            await keys.ActivateAsync();
            accept = true;
            await keys.GenerateCommand.ExecuteAsync(null);
            Assert.Equal(message, model.Message); // Parent event subscription was released.
            keys.Clear();
            await keys.DeactivateAsync();
        }
        Assert.Empty(connection.ReceivedCalls());
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Dispatch(Action action) => action();
        public T Dispatch<T>(Func<T> action) => action();
        public Task DispatchAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> DispatchAsync<T>(Func<T> action) => Task.FromResult(action());
        public Task DispatchAsync(Func<Task> action) => action();
        public Task<T> DispatchAsync<T>(Func<Task<T>> action) => action();
    }
}
