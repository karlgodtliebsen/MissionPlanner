using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;
using System.Text.Json;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Setup.Advanced.Warnings;

namespace MissionPlanner.Core.Tests;

public sealed class WarningManagerTests
{
    private readonly WarningSources sources = new(new TelemetryFieldCatalog());
    private static WarningRule Rule()
    {
        return new(Guid.NewGuid(), "Battery", true, "battery-voltage",
        WarningComparison.Less, 10, 20, WarningSeverity.Warning, "{name}: {value}");
    }

    [Fact]
    public void AndConditionsRequireEveryFreshSourceAndClearIndependently()
    {
        var clock = new TestClock();
        var engine = new WarningEngine(sources, clock);
        var otherKey = sources.All.First(source => source.Key != "battery-voltage").Key;
        var rule = Rule() with
        {
            AndConditions = [new(otherKey, WarningComparison.Greater, 5)]
        };
        engine.SetRules([rule]);
        WarningSnapshot Evaluate(double? second, DateTimeOffset timestamp) => engine.Evaluate(key =>
            key == rule.Source ? new(9, clock.GetUtcNow()) : new(second, timestamp))[0];
        Assert.Equal(WarningState.Active, Evaluate(6, clock.GetUtcNow()).State);
        Assert.Equal(WarningState.Cleared, Evaluate(4, clock.GetUtcNow()).State);
        Assert.Equal(WarningState.Unavailable, Evaluate(6, clock.GetUtcNow().AddSeconds(-4)).State);
        Assert.False(Evaluate(null, clock.GetUtcNow()).Notify);
        Assert.NotEmpty(sources.Validate(rule with { AndConditions = [new("unknown", WarningComparison.Less, 4)] }));
    }

    [Theory]
    [InlineData(WarningComparison.Less, 9, true)]
    [InlineData(WarningComparison.Less, 10, false)]
    [InlineData(WarningComparison.LessOrEqual, 10, true)]
    [InlineData(WarningComparison.Equal, 10, true)]
    [InlineData(WarningComparison.Equal, 11, false)]
    [InlineData(WarningComparison.NotEqual, 10, false)]
    [InlineData(WarningComparison.NotEqual, 11, true)]
    [InlineData(WarningComparison.GreaterOrEqual, 10, true)]
    [InlineData(WarningComparison.Greater, 10, false)]
    [InlineData(WarningComparison.Greater, 11, true)]
    [InlineData(WarningComparison.InsideRange, 10, true)]
    [InlineData(WarningComparison.InsideRange, 20, true)]
    [InlineData(WarningComparison.InsideRange, 21, false)]
    [InlineData(WarningComparison.OutsideRange, 10, false)]
    [InlineData(WarningComparison.OutsideRange, 20, false)]
    [InlineData(WarningComparison.OutsideRange, 21, true)]
    public void OperatorsRespectBoundaries(WarningComparison comparison, double value, bool expected)
    {
        Assert.Equal(expected, WarningEngine.Matches(Rule() with
        {
            Comparison = comparison
        }, value));
    }

    [Fact]
    public void DelayHysteresisCooldownAndAcknowledgementHaveIndependentLifecycles()
    {
        var clock = new TestClock();
        var engine = new WarningEngine(sources, clock);
        var rule = Rule() with
        {
            DelaySeconds = 2,
            Hysteresis = 1,
            CooldownSeconds = 10,
            RequiresAcknowledgement = true
        };
        var other = rule with
        {
            Id = Guid.NewGuid()
        };
        engine.SetRules([rule, other]);
        IReadOnlyList<WarningSnapshot> Tick(double value)
        {
            return engine.Evaluate(_ => new(value, clock.GetUtcNow()));
        }

        Assert.All(Tick(9), item => Assert.Equal(WarningState.Pending, item.State));
        clock.Advance(1);
        Assert.All(Tick(9), item => Assert.False(item.Notify));
        clock.Advance(1);
        Assert.All(Tick(9), item => Assert.True(item.Notify));
        Assert.All(Tick(10.5), item => Assert.Equal(WarningState.Active, item.State));
        clock.Advance(9);
        Assert.All(Tick(9), item => Assert.False(item.Notify));
        engine.Acknowledge(rule.Id);
        clock.Advance(1);
        var states = Tick(9);
        Assert.Equal(WarningState.Acknowledged, states[0].State);
        Assert.False(states[0].Notify);
        Assert.True(states[1].Notify);
        Assert.All(Tick(11), item => Assert.Equal(WarningState.Cleared, item.State));
    }

    [Fact]
    public void UnavailableSamplesResetDebounceAndNeverLoopNotifications()
    {
        var clock = new TestClock();
        var engine = new WarningEngine(sources, clock);
        engine.SetRules([Rule() with { DelaySeconds = 1 }]);
        Assert.Equal(WarningState.Pending, engine.Evaluate(_ => new(1, clock.GetUtcNow()))[0].State);
        foreach (var value in new double?[] { null, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var status = engine.Evaluate(_ => new(value, clock.GetUtcNow()))[0];
            Assert.Equal(WarningState.Unavailable, status.State);
            Assert.Null(status.Value);
            Assert.False(status.Notify);
        }
        Assert.Equal(WarningState.Unavailable, engine.Evaluate(_ => new(1, clock.GetUtcNow().AddSeconds(-4)))[0].State);
        Assert.Equal(WarningState.Unavailable, engine.Evaluate(_ => new(1, clock.GetUtcNow().AddSeconds(1)))[0].State);
        clock.Advance(2);
        Assert.Equal(WarningState.Pending, engine.Evaluate(_ => new(1, clock.GetUtcNow()))[0].State);
    }

    [Fact]
    public void ValidationRejectsUnknownAndNonNumericSourcesAndUnsafeTiming()
    {
        foreach (var rule in new[]
        {
            Rule() with { Source = "mode" }, Rule() with { Source = "missing" },
            Rule() with { Threshold = double.NaN }, Rule() with { Hysteresis = -1 },
            Rule() with { DelaySeconds = -1 }, Rule() with { CooldownSeconds = 0 },
            Rule() with { Comparison = WarningComparison.InsideRange, UpperThreshold = 9 },
            Rule() with { Comparison = WarningComparison.OutsideRange, Hysteresis = 6 },
            Rule() with { Comparison = (WarningComparison)99 }, Rule() with { Name = "" }
        })
        {
            Assert.NotEmpty(sources.Validate(rule));
            Assert.Throws<ArgumentException>(() => new WarningEngine(sources, TimeProvider.System).SetRules([rule]));
        }
    }

    [Fact]
    public async Task RepositoryRoundTripAndMixedCorruptionPreserveValidRules()
    {
        var store = new MemoryStore();
        var repository = new WarningRuleRepository(store, sources);
        var rule = Rule();
        await repository.SaveAsync([rule], TestContext.Current.CancellationToken);
        Assert.Equal(rule, Assert.Single((await repository.LoadAsync(TestContext.Current.CancellationToken)).Rules));
        store.Document = JsonSerializer.Serialize(new
        {
            Version = 1,
            Rules = new object[] { rule, new { Name = "broken" }, rule }
        });
        var loaded = await repository.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(rule, Assert.Single(loaded.Rules));
        Assert.Contains("2 invalid", loaded.Diagnostic);
        Assert.Equal(store.Document, store.Recovery);
        store.Document = "{bad json";
        Assert.Empty((await repository.LoadAsync(TestContext.Current.CancellationToken)).Rules);
        Assert.Equal("{bad json", store.Recovery);
    }

    [Fact]
    public async Task InvalidSaveAndStorageFailureNeverReportSuccess()
    {
        var store = new MemoryStore();
        var repository = new WarningRuleRepository(store, sources);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync([Rule() with { Source = "missing" }], TestContext.Current.CancellationToken));
        Assert.Null(store.Document);
        store.FailWrite = true;
        await Assert.ThrowsAsync<IOException>(() => repository.SaveAsync([Rule()], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BackgroundMonitorDeliversWithoutAnOpenPageAndStopsWithItsOwner()
    {
        var repository = new WarningRuleRepository(new MemoryStore(), sources);
        var rule = Rule() with { RequiresAcknowledgement = true };
        await repository.SaveAsync([rule], TestContext.Current.CancellationToken);
        var active = Substitute.For<IActiveVehicleContext>();
        var id = new VehicleId(1, 1);
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
        active.IsOnline.Returns(true); active.VehicleId.Returns(id);
        active.State.Returns(_ => state with { Power = state.Power with { BatteryVoltageVolts = 9, ObservedAt = DateTimeOffset.UtcNow } });
        var notifications = Substitute.For<IUserNotificationService>();
        var delivered = new TaskCompletionSource<UserNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        notifications.NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            delivered.TrySetResult(call.Arg<UserNotification>()); return Task.CompletedTask;
        });
        await using var monitor = new WarningMonitor(repository, sources, active, TimeProvider.System, notifications, NullLogger<WarningMonitor>.Instance);
        await monitor.StartAsync(TestContext.Current.CancellationToken);
        await monitor.StartAsync(TestContext.Current.CancellationToken); // Does not start another polling loop.
        var notification = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(UserNotificationSeverity.Warning, notification.Severity);
        Assert.Contains("Battery", notification.Message);
        Assert.Equal(WarningState.Active, Assert.Single(monitor.Snapshots).State);
        monitor.Acknowledge(rule.Id);
        await monitor.DisposeAsync();
        await repository.SaveAsync([], TestContext.Current.CancellationToken);
        Assert.Single(monitor.Snapshots); // Persistence updates are detached on shutdown.
        await notifications.Received(1).NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 7, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }

        internal void Advance(int seconds)
        {
            now += TimeSpan.FromSeconds(seconds);
        }
    }

    private sealed class MemoryStore : IWarningRuleStore
    {
        internal string? Document;
        internal string? Recovery;
        internal bool FailWrite;
        public ValueTask<string?> ReadAsync(CancellationToken token)
        {
            return ValueTask.FromResult(Document);
        }

        public ValueTask WriteAsync(string document, CancellationToken token)
        {
            if (FailWrite)
            {
                throw new IOException("storage unavailable");
            }
            Document = document;
            return ValueTask.CompletedTask;
        }
        public ValueTask QuarantineAsync(string document, CancellationToken token)
        {
            Recovery = document;
            return ValueTask.CompletedTask;
        }
    }
}

