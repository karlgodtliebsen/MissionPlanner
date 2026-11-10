using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

[Collection("Document rendering")]
public sealed class MotorSetupTabsTests
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.CheckAccess().Returns(_ => Dispatcher.UIThread.CheckAccess());
        dispatcher.When(item => item.Dispatch(Arg.Any<Action>())).Do(call =>
        {
            if (Dispatcher.UIThread.CheckAccess()) call.Arg<Action>()();
            else Dispatcher.UIThread.Post(call.Arg<Action>());
        });
        dispatcher.DispatchAsync(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()();
            return Task.CompletedTask;
        });
        dispatcher.DispatchAsync(Arg.Any<Func<Task>>()).Returns(call => call.Arg<Func<Task>>()());
        var services = new ServiceCollection().AddLogging().AddSingleton(dispatcher)
            .AddSingleton(Substitute.For<IDomainEventHub>()).BuildServiceProvider();
        return AppBuilder.Configure(() => new MissionPlanner.App.App(services)).UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    [Fact]
    public Task SharedDiagramAndTabsSupportRepeatingBurstsStagedPreviewAndVerifiedSave() => RunAsync(async () =>
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
        var previousDesignMode = Design.IsDesignMode;
        designMode.SetValue(null, true);
        var view = CreateView(fixture.Model);
        var window = new Window { Content = view, Width = 1000, Height = 1450, RequestedThemeVariant = ThemeVariant.Dark };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var tabs = view.FindControl<TabControl>("MotorSetupTabs")!;
            Assert.Equal(new[] { "Manual setup", "Guided motor setup", "Wizard setup" }, tabs.Items.OfType<TabItem>().Select(item => item.Header));
            Assert.Equal(0, tabs.SelectedIndex);
            var diagram = view.FindControl<MotorLayoutDiagram>("SharedMotorDiagram")!;
            Assert.True(diagram.TranslatePoint(default, view)!.Value.Y < tabs.TranslatePoint(default, view)!.Value.Y);
            var arm = view.FindControl<Control>("MotorSpinArmSection")!;
            var min = view.FindControl<Control>("MotorSpinMinimumSection")!;
            Assert.True(arm.TranslatePoint(default, view)!.Value.Y >= tabs.TranslatePoint(default, view)!.Value.Y + tabs.Bounds.Height);
            Assert.True(min.TranslatePoint(default, view)!.Value.Y > arm.TranslatePoint(default, view)!.Value.Y);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<Button>(), button => AutomationProperties.GetName(button) == "Set motor order");
            SaveScreenshot(window, "motor-setup-manual.png");

            fixture.Model.IsMotorTestSafetyAcknowledged = true;
            await ClickMotorAsync(window, diagram, fixture, 1);
            await fixture.Model.TestMotorCommand.ExecutionTask!;
            Assert.Equal(1, fixture.Pulses.Last().TestOrder);
            Assert.True(fixture.Active.State!.IsArmed);
            await ClickMotorAsync(window, diagram, fixture, 2);
            await fixture.Model.TestMotorCommand.ExecutionTask!;
            Assert.Equal(3, fixture.Pulses.Last().TestOrder);
            Assert.True(fixture.Model.DiagramMotors.Single(item => item.Motor.MotorNumber == 2).IsActive);
            Assert.Equal(new[] { "test:1", "stop", "disarmed", "test:3" }, fixture.Actions);
            var manualCount = fixture.Pulses.Count;
            tabs.SelectedIndex = 2;
            await fixture.Model.SetupNavigationTask;
            Assert.All(fixture.Model.DiagramMotors, point => Assert.Contains("Not identified", point.OutputLabel));
            await UntilAsync(() => fixture.Pulses.Count >= manualCount + 2);
            Assert.Equal(fixture.Pulses[manualCount].TestOrder, fixture.Pulses[manualCount + 1].TestOrder);
            Assert.All(fixture.Pulses.Skip(manualCount), pulse => Assert.Equal(1, pulse.DurationSeconds));
            var wizard = fixture.Model.Wizard!;
            await SelectAsync(window, diagram, fixture, 2);
            Assert.Equal(2, fixture.Service.Current!.OutputChannel);
            Assert.Equal(3, fixture.Pulses.Last().TestOrder); // Output 2 is Motor2 / test position C.
            Assert.Equal(33, fixture.Parameters.GetParameter(fixture.Id, "SERVO1_FUNCTION")!.Value);
            Assert.Equal(1, fixture.Model.DiagramMotors.Single(item => item.Motor.MotorNumber == 2).PendingOutputChannel);
            Assert.True(fixture.Model.DiagramMotors.Single(item => item.Motor.MotorNumber == 2).IsChanged);
            Assert.Equal(0, fixture.Writes);
            window.UpdateLayout();
            SaveScreenshot(window, "motor-setup-wizard.png");

            tabs.SelectedIndex = 0;
            await fixture.Model.SetupNavigationTask;
            var stoppedCount = fixture.Pulses.Count;
            await Task.Delay(2200, TestContext.Current.CancellationToken);
            Assert.Equal(stoppedCount, fixture.Pulses.Count);
            Assert.False(wizard.IsPulsing);
            Assert.Empty(wizard.SelectedMotors);
            Assert.True(diagram.IsInteractive);
            window.UpdateLayout();
            SaveScreenshot(window, "motor-setup-manual.png");

            tabs.SelectedIndex = 2;
            await fixture.Model.SetupNavigationTask;
            Assert.Empty(wizard.SelectedMotors);
            Assert.Equal(1, fixture.Service.Current!.OutputChannel);
            Assert.All(fixture.Model.DiagramMotors, point => Assert.Contains("Not identified", point.OutputLabel));
            foreach (var number in new[] { 2, 3, 4, 1 }) await SelectAsync(window, diagram, fixture, number);
            Assert.True(wizard.IsComplete);
            Assert.False(wizard.IsPulsing);
            Assert.Equal(0, fixture.Writes);
            Assert.Equal("SERVO1_FUNCTION = 34\nSERVO2_FUNCTION = 35\nSERVO3_FUNCTION = 36\nSERVO4_FUNCTION = 33", wizard.Review);
            await wizard.SaveCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(wizard.Finished);
            Assert.Equal(4, fixture.Writes);
            Assert.All(fixture.Model.DiagramMotors, point => Assert.Null(point.PendingOutputChannel));
            Assert.All(fixture.Model.DiagramMotors, point => Assert.True(point.IsChanged));
            Assert.All(fixture.Model.DiagramMotors, point => Assert.Contains("Resolved", point.OutputLabel));
            tabs.SelectedIndex = 1;
            await fixture.Model.SetupNavigationTask;
            SaveScreenshot(window, "motor-setup-guided.png");
            tabs.SelectedIndex = 2;
            await fixture.Model.SetupNavigationTask;
            Assert.False(wizard.Finished);
            Assert.Empty(wizard.SelectedMotors);
            Assert.All(fixture.Model.DiagramMotors, point => Assert.Contains("Not identified", point.OutputLabel));
            Assert.All(fixture.Model.DiagramMotors, point => Assert.False(point.IsChanged));
            Assert.Equal(4, fixture.Writes);
        }
        finally
        {
            await fixture.Model.DeactivateAsync();
            window.Close();
            designMode.SetValue(null, previousDesignMode);
        }
    });

    [Theory]
    [InlineData("page")]
    [InlineData("safety")]
    [InlineData("information")]
    [InlineData("disconnect")]
    public Task LeavingTheWorkflowStopsBurstsWithoutWriting(string boundary) => RunAsync(async () =>
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        fixture.Model.SelectedSetupTab = 2;
        await fixture.Model.SetupNavigationTask;
        Assert.Empty(fixture.Pulses);
        fixture.Model.IsMotorTestSafetyAcknowledged = true;
        await fixture.Model.SetupNavigationTask;
        await UntilAsync(() => fixture.Pulses.Count > 0 && fixture.Service.CanObserve);
        switch (boundary)
        {
            case "page": await fixture.Model.DeactivateAsync(); break;
            case "safety": fixture.Model.IsMotorTestSafetyAcknowledged = false; break;
            case "information": fixture.Model.SelectedPageTab = 0; break;
            case "disconnect": fixture.Disconnect(); break;
        }
        await fixture.Model.SetupNavigationTask;
        await fixture.Model.Wizard!.BurstsCompletion;
        var stoppedCount = fixture.Pulses.Count;
        await Task.Delay(2200, TestContext.Current.CancellationToken);
        Assert.Equal(stoppedCount, fixture.Pulses.Count);
        Assert.False(fixture.Model.Wizard.IsPulsing);
        Assert.Equal(0, fixture.Writes);
        await fixture.Model.DeactivateAsync();
    });

    [Fact]
    public Task NavigatingDuringAManualMotorSwitchCancelsTheQueuedMotorAndStartsAFreshWizard() => RunAsync(async () =>
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        fixture.Model.IsMotorTestSafetyAcknowledged = true;
        await fixture.Model.TestMotorCommand.ExecuteAsync(fixture.Model.Motors.Single(motor => motor.MotorNumber == 1));
        var switching = fixture.Model.TestMotorCommand.ExecuteAsync(fixture.Model.Motors.Single(motor => motor.MotorNumber == 2));
        Assert.False(switching.IsCompleted); // Waiting for the previous motor's disarmed heartbeat.
        fixture.Model.SelectedSetupTab = 2;
        await switching;
        await fixture.Model.SetupNavigationTask;
        await UntilAsync(() => fixture.Pulses.Count == 2);
        Assert.Equal(new[] { 1, 1 }, fixture.Pulses.Select(pulse => pulse.TestOrder));
        Assert.True(fixture.Model.Wizard!.IsPulsing);
        Assert.Empty(fixture.Model.Wizard.SelectedMotors);
        Assert.Equal(0, fixture.Writes);
        await fixture.Model.DeactivateAsync();
    });

    [Fact]
    public Task LiveAssignmentsRefreshTheSharedDiagramAndDuplicatesNeverPulse() => RunAsync(async () =>
    {
        using var fixture = new Fixture();
        await fixture.Model.ActivateAsync();
        fixture.Store("SERVO1_FUNCTION", 34);
        Assert.True(fixture.Model.DiagramMotors.Single(item => item.Motor.MotorNumber == 1).IsChanged);
        Assert.Equal(MotorOutputResolutionStatus.Ambiguous, fixture.Model.DiagramMotors.Single(item => item.Motor.MotorNumber == 2).Output!.Status);
        fixture.Model.IsMotorTestSafetyAcknowledged = true;
        fixture.Model.SelectedSetupTab = 2;
        await fixture.Model.SetupNavigationTask;
        Assert.True(fixture.Model.Wizard!.NeedsRepair);
        Assert.True(fixture.Model.Wizard.RepairCommand.CanExecute(null));
        Assert.Empty(fixture.Pulses);
        Assert.Equal(0, fixture.Writes);
        await fixture.Model.DeactivateAsync();
    });

    private static async Task SelectAsync(Window window, MotorLayoutDiagram diagram, Fixture fixture, int number)
    {
        var wizard = fixture.Model.Wizard!;
        await UntilAsync(() => diagram.Motors.Any(point => point.Motor.MotorNumber == number && wizard.SelectMotorCommand.CanExecute(point.Motor)));
        await ClickMotorAsync(window, diagram, fixture, number);
        Assert.NotNull(wizard.SelectMotorCommand.ExecutionTask);
        await wizard.SelectMotorCommand.ExecutionTask;
        Assert.Contains(number, wizard.SelectedMotors);
    }

    private static Task ClickMotorAsync(Window window, MotorLayoutDiagram diagram, Fixture fixture, int number)
    {
        window.UpdateLayout();
        var button = diagram.Children.OfType<Button>().Single(item => ((MotorLayoutMotor)item.CommandParameter!).MotorNumber == number);
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
        Assert.True(button.IsEffectivelyEnabled);
        var point = button.TranslatePoint(new Point(27, 27), window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        fixture.Store("FRAME_CLASS", 1); // A live parameter refresh must not swallow a click.
        Assert.Same(button, diagram.Children.OfType<Button>().Single(item => ((MotorLayoutMotor)item.CommandParameter!).MotorNumber == number));
        window.MouseUp(point, MouseButton.Left);
        return Task.CompletedTask;
    }

    private static async Task UntilAsync(Func<bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        while (!predicate()) await Task.Delay(20, timeout.Token);
    }

    private static MotorTestView CreateView(MotorTestViewModel model)
    {
        var property = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
        var previous = Design.IsDesignMode;
        try { property.SetValue(null, true); return new MotorTestView { DataContext = model }; }
        finally { property.SetValue(null, previous); }
    }

    private static void SaveScreenshot(Window window, string name)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "motor-setup-tabs");
        Directory.CreateDirectory(directory);
        using var bitmap = new RenderTargetBitmap(new PixelSize(1000, 1450));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, name), PngBitmapEncoderOptions.Default);
    }

    private static async Task RunAsync(Func<Task> action)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(MotorSetupTabsTests));
        try { await session.Dispatch(async () => { await action(); return true; }, TestContext.Current.CancellationToken); }
        finally { await Task.Run(session.Dispose, CancellationToken.None); }
    }

    private sealed class Fixture : IDisposable
    {
        public VehicleId Id { get; } = new(1, 1);
        public VehicleParameterRegistry Parameters { get; } = new();
        public IActiveVehicleContext Active { get; } = Substitute.For<IActiveVehicleContext>();
        public MotorOrderService Service { get; }
        public MotorTestViewModel Model { get; }
        public List<MotorTestRequest> Pulses { get; } = [];
        public List<string> Actions { get; } = [];
        public int Writes { get; private set; }
        private VehicleState state;
        private MotorTestSnapshot snapshot = MotorTestSnapshot.Initial;

        public Fixture()
        {
            Active.VehicleId.Returns(Id);
            Active.IsOnline.Returns(true);
            state = new VehicleState(Id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
                DateTimeOffset.UtcNow, VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
            Active.State.Returns(_ => state);
            Store("FRAME_CLASS", 1);
            Store("FRAME_TYPE", 1);
            for (var i = 1; i <= 4; i++) Store($"SERVO{i}_FUNCTION", 32 + i);
            var actuators = Substitute.For<IActuatorTestService>();
            actuators.Current.Returns(_ => snapshot);
            actuators.TestMotorAsync(Id, Arg.Any<MotorTestRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                Assert.False(state.IsArmed); // Never overlap pulses or start before disarmed telemetry.
                var request = call.Arg<MotorTestRequest>();
                Pulses.Add(request);
                Actions.Add($"test:{request.TestOrder}");
                state = state with { Flight = state.Flight with { IsArmed = true } };
                snapshot = new(Id, MotorTestState.Running, request.TestOrder, "Running", []);
                actuators.StateChanged += Raise.Event<Action<MotorTestStateChangedEventArgs>>(new MotorTestStateChangedEventArgs(snapshot));
                _ = CompleteBoundedTestAsync(snapshot, request.DurationSeconds, actuators);
                return new MotorTestResult(true, "Accepted");
            });
            actuators.EmergencyStopAsync(Arg.Any<CancellationToken>()).Returns(call =>
            {
                if (snapshot.State == MotorTestState.Running)
                {
                    Actions.Add("stop");
                    snapshot = snapshot with { State = MotorTestState.Stopped, ActiveMotor = null };
                    actuators.StateChanged += Raise.Event<Action<MotorTestStateChangedEventArgs>>(new MotorTestStateChangedEventArgs(snapshot));
                    _ = ReportDisarmedAsync();
                }
                return Task.CompletedTask;
            });
            var protocol = Substitute.For<IVehicleParameterService>();
            protocol.SetParameterAsync(Id, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                Writes++;
                Store(call.Arg<string>(), call.Arg<float>());
                return true;
            });
            protocol.RequestParameterAsync(Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var parameter = Parameters.GetParameter(Id, call.Arg<string>())!;
                Store(parameter.Name, parameter.Value);
                return true;
            });
            var resolver = new MotorLayoutResolver();
            var outputs = new MotorOutputResolver(Parameters);
            var gate = new VehicleOperationGate();
            var spin = Substitute.For<IMotorSpinParameterService>();
            spin.GetState(Id).Returns(new MotorSpinParameterState(.12f, .16f));
            Service = new(Active, Parameters, protocol, resolver, actuators, gate);
            var threshold = new MotorStartThresholdService(Active, Parameters, resolver, outputs, actuators, spin, gate);
            var connection = Substitute.For<IVehicleConnectionSession>();
            connection.ParameterRegistry.Returns(Parameters);
            Model = new(connection, Active, Substitute.For<IParameterEditSessionFactory>(),
                Substitute.For<IVehicleParameterLoadStatusContext>(), Substitute.For<IDialogService>(),
                Substitute.For<IDomainFactory>(), Substitute.For<IDomainEventHub>(), NullLogger<MotorTestViewModel>.Instance,
                Parameters, actuators, spin, resolver, Substitute.For<IUserConfirmationService>(), threshold, outputs, motorOrder: Service);
            Model.RefreshCommand.Execute(null);
        }

        public void Store(string name, float value) => Parameters.StoreParameter(Id, new(name, value, MavParamType.Int32, 0, 6), CancellationToken.None);
        private async Task CompleteBoundedTestAsync(MotorTestSnapshot running, double duration, IActuatorTestService actuators)
        {
            await Task.Delay(TimeSpan.FromSeconds(duration));
            if (!ReferenceEquals(snapshot, running)) return;
            // The production service completes its timer before the next heartbeat arrives.
            snapshot = snapshot with { State = MotorTestState.Stopped, ActiveMotor = null };
            actuators.StateChanged += Raise.Event<Action<MotorTestStateChangedEventArgs>>(new MotorTestStateChangedEventArgs(snapshot));
            await ReportDisarmedAsync();
        }
        private async Task ReportDisarmedAsync()
        {
            await Task.Delay(150);
            Actions.Add("disarmed");
            state = state with { Flight = state.Flight with { IsArmed = false } };
        }
        public void Disconnect()
        {
            var before = new ActiveVehicleSnapshot(Id, Active.State);
            state = state with { Connection = state.Connection with { State = VehicleConnectionState.Offline } };
            Active.IsOnline.Returns(false);
            Active.Changed += Raise.Event<Action<ActiveVehicleChangedEventArgs>>(new ActiveVehicleChangedEventArgs(before, new(Id, Active.State)));
        }
        public void Dispose() => Model.Dispose();
    }
}
