using System.Text.Json;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Setup.Advanced.Warnings;

namespace MissionPlanner.Core.Tests;

public sealed class WarningManagerTests
{
    private readonly WarningSources sources = new(new TelemetryFieldCatalog());
    private static WarningRule Rule() => new(Guid.NewGuid(), "Battery", true, "battery-voltage",
        WarningComparison.Less, 10, 20, WarningSeverity.Warning, "{name}: {value}");

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
        Assert.Equal(expected, WarningEngine.Matches(Rule() with { Comparison = comparison }, value));
    }

    [Fact]
    public void DelayHysteresisCooldownAndAcknowledgementHaveIndependentLifecycles()
    {
        var clock = new TestClock();
        var engine = new WarningEngine(sources, clock);
        var rule = Rule() with { DelaySeconds = 2, Hysteresis = 1, CooldownSeconds = 10, RequiresAcknowledgement = true };
        var other = rule with { Id = Guid.NewGuid() };
        engine.SetRules([rule, other]);
        IReadOnlyList<WarningSnapshot> Tick(double value) => engine.Evaluate(_ => new(value, clock.GetUtcNow()));
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
        Assert.Equal(rule, Assert.Single((await repository.LoadAsync(default)).Rules));
        store.Document = JsonSerializer.Serialize(new { Version = 1, Rules = new object[] { rule, new { Name = "broken" }, rule } });
        var loaded = await repository.LoadAsync(default);
        Assert.Equal(rule, Assert.Single(loaded.Rules));
        Assert.Contains("2 invalid", loaded.Diagnostic);
        Assert.Equal(store.Document, store.Recovery);
        store.Document = "{bad json";
        Assert.Empty((await repository.LoadAsync(default)).Rules);
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

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 9, 7, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(int seconds) => now += TimeSpan.FromSeconds(seconds);
    }

    private sealed class MemoryStore : IWarningRuleStore
    {
        internal string? Document;
        internal string? Recovery;
        internal bool FailWrite;
        public ValueTask<string?> ReadAsync(CancellationToken token) => ValueTask.FromResult(Document);
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

