using System.Globalization;

namespace MissionPlanner.Core.Setup.Advanced.Warnings;

/// <summary>Deterministic, single-owner warning evaluation with delay, hysteresis and cooldown.</summary>
public sealed class WarningEngine(WarningSources sources, TimeProvider clock)
{
    private readonly Dictionary<Guid, Entry> entries = [];

    /// <summary>Replaces rules after complete validation, preserving unchanged rule state.</summary>
    public void SetRules(IReadOnlyList<WarningRule> rules)
    {
        if (rules.Count > 256 || rules.Select(rule => rule.Id).Distinct().Count() != rules.Count
            || rules.Any(rule => sources.Validate(rule).Count != 0))
        {
            throw new ArgumentException("Use at most 256 valid rules with unique identities.", nameof(rules));
        }
        var removed = entries.Keys.Except(rules.Select(rule => rule.Id)).ToArray();
        foreach (var id in removed)
        {
            entries.Remove(id);
        }
        foreach (var rule in rules)
        {
            if (!entries.TryGetValue(rule.Id, out var previous) || previous.Rule != rule)
            {
                entries[rule.Id] = new Entry(rule, clock.GetUtcNow());
            }
        }
    }

    /// <summary>Evaluates current samples. Values older than three seconds or in the future are unavailable.</summary>
    public IReadOnlyList<WarningSnapshot> Evaluate(Func<string, WarningSample> sample)
    {
        var now = clock.GetUtcNow();
        return entries.Values.Select(entry => Evaluate(entry, sample(entry.Rule.Source), now)).ToArray();
    }

    /// <summary>Acknowledges only the selected active rule when acknowledgement is configured.</summary>
    public void Acknowledge(Guid id)
    {
        if (entries.TryGetValue(id, out var entry) && entry.Rule.RequiresAcknowledgement && entry.State == WarningState.Active)
        {
            entry.State = WarningState.Acknowledged;
            entry.ChangedAt = clock.GetUtcNow();
        }
    }

    /// <summary>Tests the comparison without modifying live engine state.</summary>
    public static bool Matches(WarningRule rule, double value, bool holding = false)
    {
        if (!double.IsFinite(value))
        {
            return false;
        }
        var h = holding ? rule.Hysteresis : 0;
        return rule.Comparison switch
        {
            WarningComparison.Less => value < rule.Threshold + h,
            WarningComparison.LessOrEqual => value <= rule.Threshold + h,
            WarningComparison.Greater => value > rule.Threshold - h,
            WarningComparison.GreaterOrEqual => value >= rule.Threshold - h,
            WarningComparison.Equal => Math.Abs(value - rule.Threshold) <= h,
            WarningComparison.NotEqual => Math.Abs(value - rule.Threshold) > h,
            WarningComparison.InsideRange => value >= rule.Threshold - h && value <= rule.UpperThreshold + h,
            WarningComparison.OutsideRange => value < rule.Threshold + h || value > rule.UpperThreshold - h,
            _ => false
        };
    }

    private static WarningSnapshot Evaluate(Entry entry, WarningSample sample, DateTimeOffset now)
    {
        var rule = entry.Rule;
        var previous = entry.State;
        var notify = false;
        var valid = sample.Value.HasValue && double.IsFinite(sample.Value.Value)
            && sample.ObservedAt.HasValue && now >= sample.ObservedAt && now - sample.ObservedAt <= TimeSpan.FromSeconds(3);
        if (!rule.Enabled || !valid)
        {
            entry.State = rule.Enabled ? WarningState.Unavailable : WarningState.Disabled;
            entry.PendingSince = null;
        }
        else if (!Matches(rule, sample.Value!.Value, entry.State is WarningState.Active or WarningState.Acknowledged))
        {
            entry.State = entry.State is WarningState.Active or WarningState.Acknowledged or WarningState.Cleared
                ? WarningState.Cleared : WarningState.Inactive;
            entry.PendingSince = null;
        }
        else if (entry.State is not (WarningState.Active or WarningState.Acknowledged))
        {
            entry.PendingSince ??= now;
            entry.State = now - entry.PendingSince >= TimeSpan.FromSeconds(rule.DelaySeconds) ? WarningState.Active : WarningState.Pending;
            notify = entry.State == WarningState.Active && now - entry.LastNotification >= TimeSpan.FromSeconds(rule.CooldownSeconds);
        }
        else if (entry.State == WarningState.Active && now - entry.LastNotification >= TimeSpan.FromSeconds(rule.CooldownSeconds))
        {
            notify = true;
        }
        if (notify)
        {
            entry.LastNotification = now;
        }
        if (entry.State != previous)
        {
            entry.ChangedAt = now;
        }
        var value = valid ? sample.Value : null;
        var message = rule.Message.Replace("{name}", rule.Name, StringComparison.Ordinal)
            .Replace("{value}", value?.ToString("G", CultureInfo.InvariantCulture) ?? "unavailable", StringComparison.Ordinal);
        return new(rule.Id, entry.State, entry.ChangedAt, value, message, notify);
    }

    private sealed class Entry(WarningRule rule, DateTimeOffset now)
    {
        internal WarningRule Rule { get; } = rule;
        internal WarningState State { get; set; }
        internal DateTimeOffset ChangedAt { get; set; } = now;
        internal DateTimeOffset? PendingSince { get; set; }
        internal DateTimeOffset LastNotification { get; set; } = DateTimeOffset.MinValue;
    }
}
