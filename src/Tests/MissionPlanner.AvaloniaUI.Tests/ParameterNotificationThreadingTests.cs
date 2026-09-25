using System.Collections.Concurrent;
using System.Reflection;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.ConfigTuning.Sections;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Protects parameter command bindings from worker-thread write notifications.</summary>
[Collection("Design preview")]
public sealed class ParameterNotificationThreadingTests
{
    /// <summary>Both field and batch write notifications update commands only after UI dispatch.</summary>
    [Theory]
    [InlineData("TEST_PARAM")]
    [InlineData(null)]
    public async Task WorkerNotificationUpdatesRowsAndCommandsOnUiThread(string? fieldName)
    {
        var uiThread = Environment.CurrentManagedThreadId;
        var pending = new ConcurrentQueue<Action>();
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.CheckAccess().Returns(_ => Environment.CurrentManagedThreadId == uiThread);
        dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(call =>
        {
            var action = call.Arg<Action>()!;
            if (Environment.CurrentManagedThreadId == uiThread)
            {
                action();
            }
            else
            {
                pending.Enqueue(action);
            }
        });
        var events = Substitute.For<IDomainEventHub>();
        using var services = new ServiceCollection()
            .AddSingleton(dispatcher)
            .AddSingleton(events)
            .BuildServiceProvider();
        // Avalonia 12 hides locator mutation from its public reference assembly.
        // Scope the application used by the legacy ServiceHelper to this test.
        using var scope = (IDisposable)typeof(AvaloniaLocator)
            .GetMethod("EnterScope", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        var locator = typeof(AvaloniaLocator)
            .GetProperty("CurrentMutable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var binding = locator.GetType().GetMethod("Bind")!.MakeGenericMethod(typeof(Application)).Invoke(locator, null)!;
        binding.GetType().GetMethod("ToConstant")!.MakeGenericMethod(typeof(MissionPlanner.App.App))
            .Invoke(binding, [new MissionPlanner.App.App(services)]);

        var session = Substitute.For<IParameterEditSession>();
        var field = new ParameterEditField("TEST_PARAM", MavParamType.Real32, 1, 1, 2,
            ParameterFieldMetadata.Empty, null);
        session.Fields.Returns(_ => new[] { field });
        session.GetField("TEST_PARAM").Returns(_ => field);
        session.IsValid.Returns(true);
        session.IsDirty.Returns(_ => field.IsModified);
        using var model = new FullParametersListTabViewModel(
          Substitute.For<IDialogService>(),
            Substitute.For<IDomainFactory>(),
              events,
           Substitute.For<IVehicleConnectionSession>(),
            Substitute.For<IActiveVehicleContext>(),
            Substitute.For<IParameterEditSessionFactory>(),
            Substitute.For<ITextClipboardService>(),
                     new ParametersFileHandler(Substitute.For<IFileOpenService>(), Substitute.For<IFileSaveService>()),

             Substitute.For<IUserConfirmationService>(),
            Substitute.For<IParameterProfileRepository>(),
            Substitute.For<IParameterProfileService>(),
            Substitute.For<IVehicleParameterLoadStatusContext>(),
            NullLogger<FullParametersListTabViewModel>.Instance,
            Substitute.For<IVehicleConnectionService>());




        await model.ActivateAsync();
        typeof(ParametersViewModel).GetMethod("AttachSession", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(model, [session]);
        model.HasConnection = true;
        Assert.True(model.WriteParametersCommand.CanExecute(null));
        Assert.Equal(1, model.ModifiedParameterCount);

        var notifications = 0;
        model.WriteParametersCommand.CanExecuteChanged += (_, _) =>
        {
            Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
            notifications++;
        };
        field = field with
        {
            LiveValue = 2,
            WriteStatus = ParameterEditWriteStatus.Confirmed
        };
        await Task.Factory.StartNew(() => session.FieldChanged?.Invoke(fieldName),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        uiThread = Environment.CurrentManagedThreadId;
        Assert.Equal(0, notifications);
        Assert.Equal(1, model.ModifiedParameterCount);
        while (pending.TryDequeue(out var update))
        {
            update();
        }
        Assert.True(notifications > 0);
        Assert.Equal(0, model.ModifiedParameterCount);
        Assert.False(model.WriteParametersCommand.CanExecute(null));

        await model.DeactivateAsync();
        notifications = 0;
        await Task.Factory.StartNew(() => session.FieldChanged?.Invoke(fieldName),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        uiThread = Environment.CurrentManagedThreadId;
        Assert.Equal(0, notifications);
    }
}
