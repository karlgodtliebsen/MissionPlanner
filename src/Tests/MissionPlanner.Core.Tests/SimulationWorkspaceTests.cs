using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.Simulation;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies the Simulation workspace profile and owned-session lifecycle.</summary>
public sealed class SimulationWorkspaceTests
{
    /// <summary>Verifies successful startup traverses explicit states and stops only the owned session.</summary>
    [Fact]
    public async Task SessionManagerStartsAndStopsOwnedRuntime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var runtime = new FakeRuntime();
        await using var manager = Manager(runtime);
        var observed = new List<SimulationSessionState>();
        manager.Changed += (_, args) => observed.Add(args.Snapshot.State);

        var started = await manager.StartAsync(Profile(), cancellationToken);
        runtime.Session.Write("line 1");
        runtime.Session.Write("line 2");
        runtime.Session.Write("line 3");
        runtime.Session.Write("line 4");
        manager.Current.RecentOutput.Select(item => item.Text).Should().Equal("line 2", "line 3", "line 4");
        var stopped = await manager.StopAsync(cancellationToken);

        started.State.Should().Be(SimulationSessionState.Running);
        stopped.State.Should().Be(SimulationSessionState.Stopped);
        observed.Should().ContainInOrder(
            SimulationSessionState.Validating,
            SimulationSessionState.Starting,
            SimulationSessionState.WaitingForHeartbeat,
            SimulationSessionState.Running,
            SimulationSessionState.Stopping,
            SimulationSessionState.Stopped);
        runtime.Session.StopCount.Should().Be(1);
        runtime.Session.Identity.RuntimeId.Should().Be("owned-runtime-1");
    }

    /// <summary>Verifies an unexpected runtime exit becomes a failed session without affecting another process.</summary>
    [Fact]
    public async Task SessionManagerReportsOwnedRuntimeCrash()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var runtime = new FakeRuntime();
        await using var manager = Manager(runtime);
        await manager.StartAsync(Profile(), cancellationToken);

        runtime.Session.Exit(new SimulatorRuntimeExit(17, false, "SITL terminated"));
        await WaitUntilAsync(() => manager.Current.State == SimulationSessionState.Failed, cancellationToken);

        manager.Current.Failure.Should().Contain("SITL terminated");
        runtime.Session.StopCount.Should().Be(0);
    }

    /// <summary>Verifies a heartbeat timeout fails startup and cleans up the exact created session.</summary>
    [Fact]
    public async Task SessionManagerCleansUpAfterHeartbeatTimeout()
    {
        var runtime = new FakeRuntime { HeartbeatFailure = new TimeoutException("Heartbeat timed out") };
        await using var manager = Manager(runtime);

        var result = await manager.StartAsync(Profile(), TestContext.Current.CancellationToken);

        result.State.Should().Be(SimulationSessionState.Failed);
        result.Failure.Should().Contain("Heartbeat timed out");
        runtime.Session.StopCount.Should().Be(1);
        runtime.Session.DisposeCount.Should().Be(1);
    }

    /// <summary>Verifies application shutdown stops an active owned session.</summary>
    [Fact]
    public async Task ApplicationShutdownStopsOwnedRuntime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var runtime = new FakeRuntime();
        await using var manager = Manager(runtime);
        await manager.StartAsync(Profile(), cancellationToken);

        await manager.ShutdownAsync(cancellationToken);

        manager.Current.State.Should().Be(SimulationSessionState.Stopped);
        runtime.Session.StopCount.Should().Be(1);
    }

    /// <summary>Verifies profile validation reports duplicate and occupied ports plus host executable errors.</summary>
    [Fact]
    public async Task ValidatorRejectsPortConflictsAndInvalidExecutable()
    {
        var host = new FakeHostEnvironment
        {
            OccupiedPort = 14550,
            ExecutableIssue = new SimulationValidationIssue("host.executable-missing", "binary.executablePath", "Missing executable")
        };
        var validator = new SimulatorProfileValidator(host);
        var profile = Profile() with
        {
            Endpoints =
            [
                new SimulationEndpoint("First", SimulationEndpointTransport.Udp, "127.0.0.1", 14550),
                new SimulationEndpoint("Second", SimulationEndpointTransport.Udp, "127.0.0.1", 14550)
            ]
        };

        var issues = await validator.ValidateAsync(profile, TestContext.Current.CancellationToken);

        issues.Should().Contain(item => item.Code == "profile.port-duplicate");
        issues.Should().Contain(item => item.Code == "host.port-conflict");
        issues.Should().Contain(item => item.Code == "host.executable-missing");
    }

    /// <summary>Verifies persisted profiles round-trip and corrupt persistence recovers to a safe default.</summary>
    [Fact]
    public async Task ProfileServicePersistsAndRecoversCorruptData()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new MemoryProfileStore();
        var service = new SimulatorProfileService(store, Substitute.For<ILogger<SimulatorProfileService>>());
        var initialized = await service.InitializeAsync(cancellationToken);
        var saved = initialized[0] with { Name = "Persisted profile", Speedup = 4 };
        await service.SaveAsync(saved, cancellationToken);
        var reloaded = new SimulatorProfileService(store, Substitute.For<ILogger<SimulatorProfileService>>());

        (await reloaded.InitializeAsync(cancellationToken)).Single().Should().BeEquivalentTo(saved);

        store.Document = "{broken";
        var recovered = new SimulatorProfileService(store, Substitute.For<ILogger<SimulatorProfileService>>());
        var recovery = await recovered.InitializeAsync(cancellationToken);
        recovery.Should().ContainSingle();
        recovery[0].FirmwareFamily.Should().Be(FirmwareFamily.ArduCopter);
        store.Document.Should().Contain("schemaVersion", Exactly.Once());
    }

    /// <summary>Verifies diagnostics redact secret environment values and argument values.</summary>
    [Fact]
    public void DiagnosticsRedactSecrets()
    {
        var profile = Profile() with
        {
            AdditionalArguments = ["--model", "quad", "--api-key=sensitive-key"],
            Environment = new Dictionary<string, string>
            {
                ["NORMAL_VALUE"] = "visible",
                ["AUTH_TOKEN"] = "sensitive-token"
            }
        };
        var snapshot = SimulationSessionSnapshot.Stopped with
        {
            Profile = profile,
            RecentOutput =
            [
                new SimulatorOutputLine(
                    DateTimeOffset.UtcNow,
                    SimulatorOutputStream.StandardError,
                    "runtime echoed sensitive-token")
            ]
        };

        var document = new SimulationDiagnosticsService().CreateBundle(snapshot);

        document.Should().Contain("visible");
        document.Should().Contain("--api-key=***");
        document.Should().NotContain("sensitive-key");
        document.Should().NotContain("sensitive-token");
    }

    /// <summary>Verifies navigation deactivation does not stop an owned session.</summary>
    [Fact]
    public async Task ViewModelNavigationOnlyDetachesObservation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var profile = Profile();
        var profileService = Substitute.For<ISimulatorProfileService>();
        profileService.InitializeAsync(Arg.Any<CancellationToken>()).Returns([profile]);
        profileService.Profiles.Returns([profile]);
        var manager = Substitute.For<ISimulationSessionManager>();
        manager.Current.Returns(SimulationSessionSnapshot.Stopped);
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!();
            return true;
        });
        var platformService = Substitute.For<ISitlPlatformService>();
        platformService.Current.Returns(new SitlPlatformCapability(
            SitlPlatform.Windows,
            SitlArchitecture.X64,
            true,
            "Test platform."));
        using var viewModel = new SimulationViewModel(
            profileService,
            manager,
            new SimulationDiagnosticsService(),
            Substitute.For<ISitlInstallationService>(),
            platformService,
            new ParametersFileHandler(Substitute.For<IFileSaver>()),
            dispatcher,
            Substitute.For<ILogger<SimulationViewModel>>());

        viewModel.Activate();
        await viewModel.InitializeAsync().WaitAsync(cancellationToken);
        viewModel.Deactivate();

        await manager.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
        viewModel.Profiles.Should().ContainSingle();
        viewModel.SelectedProfile.Should().Be(profile);
    }

    private static SimulationSessionManager Manager(FakeRuntime runtime)
    {
        var options = Options.Create(new SimulationWorkspaceOptions
        {
            HeartbeatTimeoutSeconds = 1,
            StopTimeoutSeconds = 1,
            RecentOutputCapacity = 3,
            LogRootDirectory = Path.Combine(Path.GetTempPath(), "MissionPlannerTests", "Simulation")
        });
        return new SimulationSessionManager(
            new SimulatorProfileValidator(new FakeHostEnvironment()),
            runtime,
            new DateTimeProvider(DateTimeOffset.Parse("2026-07-23T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
            options,
            Substitute.For<ILogger<SimulationSessionManager>>());
    }

    private static SimulatorProfile Profile() => SimulatorProfile.CreateDefault() with
    {
        Binary = new SimulatorBinaryReference("4.6.0", Path.GetFullPath("arducopter-test"), "test")
    };

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10, cancellationToken);
        }

        condition().Should().BeTrue();
    }

    private sealed class MemoryProfileStore : ISimulatorProfileStore
    {
        public string? Document { get; set; }

        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Document);
        }

        public ValueTask WriteAsync(string document, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document = document;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeHostEnvironment : ISimulatorHostEnvironment
    {
        public int? OccupiedPort { get; init; }

        public SimulationValidationIssue? ExecutableIssue { get; init; }

        public ValueTask<SimulationValidationIssue?> ValidateExecutableAsync(
            string executablePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ExecutableIssue);
        }

        public ValueTask<bool> IsPortAvailableAsync(
            SimulationEndpoint endpoint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(endpoint.Port != OccupiedPort);
        }
    }

    private sealed class FakeRuntime : ISimulatorRuntime
    {
        public string Name => "Fake";

        public FakeRuntimeSession Session { get; } = new();

        public Exception? HeartbeatFailure
        {
            get => Session.HeartbeatFailure;
            init => Session.HeartbeatFailure = value;
        }

        public ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(
            SimulatorProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<SimulationValidationIssue>>([]);
        }

        public Task<ISimulatorRuntimeSession> StartAsync(
            SimulatorStartRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ISimulatorRuntimeSession>(Session);
        }
    }

    private sealed class FakeRuntimeSession : ISimulatorRuntimeSession
    {
        private readonly TaskCompletionSource<SimulatorRuntimeExit> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SimulatorRuntimeIdentity Identity { get; } = new("owned-runtime-1", "Fake", 1234);

        public IReadOnlyList<SimulationEndpoint> ConnectionEndpoints { get; } =
        [new SimulationEndpoint("MAVLink", SimulationEndpointTransport.Udp, "127.0.0.1", 14550)];

        public Task<SimulatorRuntimeExit> Completion => completion.Task;

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Exception? HeartbeatFailure { get; set; }

        public event EventHandler<SimulatorOutputLine>? OutputReceived;

        public Task WaitForHeartbeatAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return HeartbeatFailure is null ? Task.CompletedTask : Task.FromException(HeartbeatFailure);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            completion.TrySetResult(new SimulatorRuntimeExit(0, true, null));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public void Exit(SimulatorRuntimeExit exit) => completion.TrySetResult(exit);

        public void Write(string text) => OutputReceived?.Invoke(
            this,
            new SimulatorOutputLine(DateTimeOffset.UtcNow, SimulatorOutputStream.StandardOutput, text));
    }
}
