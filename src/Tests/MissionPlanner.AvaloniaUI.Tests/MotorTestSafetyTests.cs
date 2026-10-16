using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Verifies the page acknowledgement gates motor commands without repeated dialogs.</summary>
[Collection("Document rendering")]
public sealed class MotorTestSafetyTests
{
    /// <summary>Provides the application services needed by the Motor Test page.</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.CheckAccess().Returns(true);
        dispatcher.When(x => x.Dispatch(Arg.Any<Action>())).Do(x => x.Arg<Action>()!());
        dispatcher.DispatchAsync(Arg.Any<Action>()).Returns(x =>
        {
            x.Arg<Action>()!();
            return Task.CompletedTask;
        });
        dispatcher.DispatchAsync(Arg.Any<Func<Task>>()).Returns(x => x.Arg<Func<Task>>()!());
        var services = new ServiceCollection().AddLogging().AddSingleton(dispatcher)
            .AddSingleton(Substitute.For<IDomainEventHub>()).BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services))
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    /// <summary>The checkbox enables repeated manual tests, and clearing it blocks execution but not Stop.</summary>
    [Fact]
    public Task CheckboxGatesRepeatedManualTestsWithoutDialogs()
    {
        return RunAsync(async () =>
        {
            using var fixture = new Fixture();
            var model = fixture.Model;
            var motor = model.Motors[0];
            var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
            var previous = Design.IsDesignMode;
            MotorTestView view;
            try
            {
                designMode.SetValue(null, true);
                view = new MotorTestView { DataContext = model };
            }
            finally
            {
                designMode.SetValue(null, previous);
            }
            var checkbox = view.FindControl<CheckBox>("MotorTestSafetyCheckBox")!;
            Assert.False(checkbox.IsChecked);
            Assert.False(model.TestMotorCommand.CanExecute(motor));
            Assert.False(model.TestSequenceCommand.CanExecute(null));
            Assert.False(model.TestAllCommand.CanExecute(null));
            await TestAllModesAsync(model);
            Assert.Empty(fixture.Actuators.ReceivedCalls());

            checkbox.IsChecked = true;
            Assert.True(model.IsMotorTestSafetyAcknowledged);
            Assert.True(model.TestMotorCommand.CanExecute(motor));
            Assert.True(model.TestSequenceCommand.CanExecute(null));
            Assert.True(model.TestAllCommand.CanExecute(null));
            await TestAllModesAsync(model);
            await TestAllModesAsync(model);

            checkbox.IsChecked = false;
            Assert.False(model.IsMotorTestSafetyAcknowledged);
            Assert.False(model.TestMotorCommand.CanExecute(motor));
            Assert.False(model.TestSequenceCommand.CanExecute(null));
            Assert.False(model.TestAllCommand.CanExecute(null));
            await TestAllModesAsync(model);
            await fixture.Actuators.Received(2).TestMotorAsync(fixture.Id,
                Arg.Is<MotorTestRequest>(request => request != null && request.TestOrder == motor.TestOrder), Arg.Any<CancellationToken>());
            await fixture.Actuators.Received(2).TestSequenceAsync(fixture.Id, 10, 2, 4, Arg.Any<CancellationToken>());
            await fixture.Actuators.Received(2).TestAllAsync(fixture.Id, 10, 2, 4, Arg.Any<CancellationToken>());
            Assert.Empty(fixture.Confirmation.ReceivedCalls());
            Assert.True(model.StopCommand.CanExecute(null));
            await model.StopCommand.ExecuteAsync(null);
            await fixture.Actuators.Received(1).EmergencyStopAsync(Arg.Any<CancellationToken>());
        });
    }

    /// <summary>The guided assistant requires the same acknowledgement for starting and each subsequent pulse.</summary>
    [Fact]
    public Task CheckboxAlsoGatesGuidedPulsesAndLeavesCancelAvailable()
    {
        return RunAsync(async () =>
        {
            using var fixture = new Fixture();
            var model = fixture.Model;
            Assert.False(model.StartThresholdCommand.CanExecute(null));
            Assert.False(model.PulseThresholdCommand.CanExecute(null));
            await model.StartThresholdCommand.ExecuteAsync(null);
            await model.PulseThresholdCommand.ExecuteAsync(null);
            Assert.False(fixture.Assistant.HasSession);
            Assert.Empty(fixture.Actuators.ReceivedCalls());

            model.IsMotorTestSafetyAcknowledged = true;
            Assert.True(model.StartThresholdCommand.CanExecute(null));
            await model.StartThresholdCommand.ExecuteAsync(null);
            Assert.True(fixture.Assistant.HasSession);
            Assert.True(model.PulseThresholdCommand.CanExecute(null));
            await model.PulseThresholdCommand.ExecuteAsync(null);
            Assert.False(model.TestMotorCommand.CanExecute(model.Motors[0]));

            model.IsMotorTestSafetyAcknowledged = false;
            Assert.False(model.PulseThresholdCommand.CanExecute(null));
            await model.PulseThresholdCommand.ExecuteAsync(null);
            await fixture.Actuators.Received(1).TestMotorAsync(fixture.Id,
                Arg.Is<MotorTestRequest>(request => request != null && request.DurationSeconds == 1), Arg.Any<CancellationToken>());
            Assert.Empty(fixture.Confirmation.ReceivedCalls());
            Assert.True(model.CancelThresholdCommand.CanExecute(null));
            await model.CancelThresholdCommand.ExecuteAsync(null);
            Assert.False(fixture.Assistant.HasSession);
            await fixture.Actuators.Received(1).EmergencyStopAsync(Arg.Any<CancellationToken>());
        });
    }

    /// <summary>Refresh preserves acknowledgement; connection, vehicle and page boundaries reset it.</summary>
    [Fact]
    public Task AcknowledgementIsScopedToPageAndVehicleConnection()
    {
        return RunAsync(async () =>
        {
            using var fixture = new Fixture();
            var model = fixture.Model;
            await model.ActivateAsync();
            model.IsMotorTestSafetyAcknowledged = true;
            model.RefreshCommand.Execute(null);
            Assert.True(model.IsMotorTestSafetyAcknowledged);
            Assert.True(model.TestAllCommand.CanExecute(null));

            var online = new ActiveVehicleSnapshot(fixture.Id, fixture.Active.State);
            var offline = new ActiveVehicleSnapshot(fixture.Id, CreateVehicleState(fixture.Id, VehicleConnectionState.Offline));
            fixture.ChangeVehicle(online, offline);
            Assert.False(model.IsMotorTestSafetyAcknowledged);
            Assert.False(model.TestAllCommand.CanExecute(null));
            fixture.ChangeVehicle(offline, online);
            Assert.False(model.IsMotorTestSafetyAcknowledged);
            model.IsMotorTestSafetyAcknowledged = true;
            var replacementId = new VehicleId(2, 1);
            var replacement = new ActiveVehicleSnapshot(replacementId, CreateVehicleState(replacementId));
            fixture.ChangeVehicle(online, replacement);
            Assert.False(model.IsMotorTestSafetyAcknowledged);
            Assert.False(model.TestAllCommand.CanExecute(null));

            model.IsMotorTestSafetyAcknowledged = true;
            await model.DeactivateAsync();
            Assert.False(model.IsMotorTestSafetyAcknowledged);
            await model.ActivateAsync();
            Assert.False(model.IsMotorTestSafetyAcknowledged);
            Assert.False(model.TestAllCommand.CanExecute(null));
            await model.DeactivateAsync();
        });
    }

    private static async Task TestAllModesAsync(MotorTestViewModel model)
    {
        await model.TestMotorCommand.ExecuteAsync(model.Motors[0]);
        await model.TestSequenceCommand.ExecuteAsync(null);
        await model.TestAllCommand.ExecuteAsync(null);
    }

    private static VehicleState CreateVehicleState(VehicleId id, VehicleConnectionState connectionState = VehicleConnectionState.Online)
    {
        return new VehicleState(id, 0, 2, 3, 0, 4, 3, connectionState,
            DateTimeOffset.UtcNow, VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
    }

    private static async Task RunAsync(Func<Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MotorTestSafetyTests));
        try
        {
            await session.Dispatch(test, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal VehicleId Id { get; } = new(1, 1);
        internal IActiveVehicleContext Active { get; } = Substitute.For<IActiveVehicleContext>();
        internal IActuatorTestService Actuators { get; } = Substitute.For<IActuatorTestService>();
        internal IUserConfirmationService Confirmation { get; } = Substitute.For<IUserConfirmationService>();
        internal MotorStartThresholdService Assistant { get; }
        internal MotorTestViewModel Model { get; }

        internal Fixture()
        {
            Active.VehicleId.Returns(Id);
            Active.IsOnline.Returns(true);
            Active.State.Returns(CreateVehicleState(Id));
            var registry = Substitute.For<IVehicleParameterRegistry>();
            IReadOnlyDictionary<string, VehicleParameter> parameters = new Dictionary<string, VehicleParameter>
            {
                ["FRAME_CLASS"] = new("FRAME_CLASS", 1, MavParamType.Int32, 0, 6),
                ["FRAME_TYPE"] = new("FRAME_TYPE", 1, MavParamType.Int32, 1, 6),
                ["SERVO1_FUNCTION"] = new("SERVO1_FUNCTION", 33, MavParamType.Int32, 2, 6),
                ["SERVO2_FUNCTION"] = new("SERVO2_FUNCTION", 34, MavParamType.Int32, 3, 6),
                ["SERVO3_FUNCTION"] = new("SERVO3_FUNCTION", 35, MavParamType.Int32, 4, 6),
                ["SERVO4_FUNCTION"] = new("SERVO4_FUNCTION", 36, MavParamType.Int32, 5, 6)
            };
            registry.GetAllParameters(Arg.Any<VehicleId>()).Returns(parameters);
            registry.GetParameter(Arg.Any<VehicleId>(), Arg.Any<string>())
                .Returns(call => parameters.GetValueOrDefault(call.Arg<string>()!));
            Actuators.TestMotorAsync(Id, Arg.Any<MotorTestRequest>(), Arg.Any<CancellationToken>())
                .Returns(new MotorTestResult(true, "Accepted"));
            Actuators.TestSequenceAsync(Id, 10, 2, 4, Arg.Any<CancellationToken>())
                .Returns(new MotorTestResult(true, "Accepted"));
            Actuators.TestAllAsync(Id, 10, 2, 4, Arg.Any<CancellationToken>())
                .Returns(new MotorTestResult(true, "Accepted"));
            var spin = Substitute.For<IMotorSpinParameterService>();
            spin.GetState(Arg.Any<VehicleId>()).Returns(new MotorSpinParameterState(null, null));
            var resolver = new MotorLayoutResolver();
            Assistant = new(Active, registry, resolver, new MotorOutputResolver(registry), Actuators, spin, new VehicleOperationGate());
            var connection = Substitute.For<IVehicleConnectionSession>();
            connection.ParameterRegistry.Returns(registry);
            Model = new(connection, Active, Substitute.For<IParameterEditSessionFactory>(),
                Substitute.For<IVehicleParameterLoadStatusContext>(), Substitute.For<IDialogService>(),
                Substitute.For<IDomainFactory>(), Substitute.For<IDomainEventHub>(), NullLogger<MotorTestViewModel>.Instance,
                registry, Actuators, spin, resolver, Confirmation, Assistant);
            Model.RefreshCommand.Execute(null);
        }

        internal void ChangeVehicle(ActiveVehicleSnapshot previous, ActiveVehicleSnapshot current)
        {
            Active.VehicleId.Returns(current.VehicleId);
            Active.IsOnline.Returns(current.IsOnline);
            Active.State.Returns(current.State);
            Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(new ActiveVehicleChangedEventArgs(previous, current));
        }

        public void Dispose()
        {
            Model.Dispose();
        }
    }
}
