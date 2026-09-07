using MissionPlanner.Core.Setup.Advanced.Output;

namespace MissionPlanner.Library.Browser;

/// <summary>Explicitly denies native forwarding until BrowserBridge exposes an audited output capability.</summary>
public sealed class BrowserOutputSinkFactory : IOutputSinkFactory
{
    /// <inheritdoc />
    public string? UnavailableReason(OutputEndpoint endpoint) =>
        "Browser output requires an audited output bridge. The current vehicle bridge does not expose this capability.";
    /// <inheritdoc />
    public Task<IOutputSink> OpenAsync(OutputEndpoint endpoint, CancellationToken token) =>
        Task.FromException<IOutputSink>(new NotSupportedException(UnavailableReason(endpoint)));
}
