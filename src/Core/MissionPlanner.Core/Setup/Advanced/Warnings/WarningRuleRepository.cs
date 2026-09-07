using System.Text.Json;

namespace MissionPlanner.Core.Setup.Advanced.Warnings;

/// <summary>Platform persistence for a bounded warning document and one recovery copy.</summary>
public interface IWarningRuleStore
{
    /// <summary>Reads the persisted document, reporting access failures to the caller.</summary>
    ValueTask<string?> ReadAsync(CancellationToken token);
    /// <summary>Atomically replaces the document.</summary>
    ValueTask WriteAsync(string document, CancellationToken token);
    /// <summary>Preserves malformed input separately before the operator saves a repaired document.</summary>
    ValueTask QuarantineAsync(string document, CancellationToken token);
}

/// <summary>Recovered rules plus actionable non-sensitive diagnostics.</summary>
public sealed record WarningRuleLoadResult(IReadOnlyList<WarningRule> Rules, string Diagnostic);

/// <summary>Validates versioned documents and isolates malformed rules without preventing startup.</summary>
public sealed class WarningRuleRepository(IWarningRuleStore store, WarningSources sources)
{
    /// <summary>Maximum persisted size in characters; browser storage and recovery are bounded.</summary>
    public const int MaximumDocumentLength = 262144;

    /// <summary>Loads valid rules independently, retaining a recovery copy when invalid entries exist.</summary>
    public async Task<WarningRuleLoadResult> LoadAsync(CancellationToken token)
    {
        string? text;
        try
        {
            text = await store.ReadAsync(token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new([], "Warning rules could not be read. Check storage permissions before saving changes.");
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return new([], string.Empty);
        }
        var rules = new List<WarningRule>();
        var invalid = 0;
        try
        {
            if (text.Length > MaximumDocumentLength)
            {
                return new([], "The warning document exceeds 256 KiB. The original has been left unchanged.");
            }
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.GetProperty("Version").GetInt32() != 1)
            {
                return new([], "This warning document uses an unsupported version. The original has been left unchanged.");
            }
            foreach (var element in document.RootElement.GetProperty("Rules").EnumerateArray())
            {
                try
                {
                    var rule = element.Deserialize<WarningRule>();
                    if (rule is null || sources.Validate(rule).Count != 0 || rules.Any(item => item.Id == rule.Id) || rules.Count >= 256)
                    {
                        invalid++;
                    }
                    else
                    {
                        rules.Add(rule);
                    }
                }
                catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
                {
                    invalid++;
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            invalid++;
        }
        if (invalid == 0)
        {
            return new(rules, string.Empty);
        }
        try
        {
            await store.QuarantineAsync(text, token);
            return new(rules, $"Skipped {invalid} invalid warning entries. A recovery copy was preserved; review before saving.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(rules, $"Skipped {invalid} invalid warning entries. Recovery storage failed; preserve the original before saving.");
        }
    }

    /// <summary>Saves only a fully valid bounded document; storage failures are never reported as success.</summary>
    public async Task SaveAsync(IReadOnlyList<WarningRule> rules, CancellationToken token)
    {
        if (rules.Count > 256 || rules.Select(rule => rule.Id).Distinct().Count() != rules.Count
            || rules.Any(rule => sources.Validate(rule).Count != 0))
        {
            throw new ArgumentException("The warning rules contain invalid or duplicate entries.", nameof(rules));
        }
        var document = JsonSerializer.Serialize(new { Version = 1, Rules = rules });
        if (document.Length > MaximumDocumentLength)
        {
            throw new ArgumentException("The warning document exceeds 256 KiB.", nameof(rules));
        }
        await store.WriteAsync(document, token);
    }
}
