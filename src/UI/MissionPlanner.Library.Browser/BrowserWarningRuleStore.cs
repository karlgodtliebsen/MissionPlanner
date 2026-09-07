using MissionPlanner.Core.Setup.Advanced.Warnings;
using MissionPlanner.Library.Browser.Interop;

namespace MissionPlanner.Library.Browser;

/// <summary>Persists bounded non-secret rules in origin-local browser storage.</summary>
public sealed class BrowserWarningRuleStore : IWarningRuleStore
{
    /// <inheritdoc />
    public ValueTask<string?> ReadAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(BrowserInterop.ReadWarningRules());
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string document, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        BrowserInterop.WriteWarningRules(document);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask QuarantineAsync(string document, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        BrowserInterop.QuarantineWarningRules(document);
        return ValueTask.CompletedTask;
    }
}
