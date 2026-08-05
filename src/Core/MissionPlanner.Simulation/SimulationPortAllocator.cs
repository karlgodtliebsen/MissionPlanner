using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Simulation;

/// <summary>Reserves endpoint identities across concurrently owned simulator sessions.</summary>
public sealed class SimulationPortAllocator(ISimulatorHostEnvironment hostEnvironment) : ISimulationPortAllocator
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HashSet<PortIdentity> claimed = [];

    /// <inheritdoc />
    public async ValueTask<ISimulationPortLease> ReserveAsync(
        IReadOnlyList<SimulationEndpoint> endpoints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (endpoints.Count == 0)
        {
            throw new InvalidOperationException("At least one simulator endpoint must be reserved.");
        }

        var identities = endpoints.Select(PortIdentity.From).ToArray();
        if (identities.Distinct().Count() != identities.Length)
        {
            throw new InvalidOperationException("The simulator profile contains duplicate endpoint reservations.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var collision = identities.FirstOrDefault(claimed.Contains);
            if (collision is not null)
            {
                throw new InvalidOperationException(
                    $"Simulator port {collision.Transport.ToString().ToUpperInvariant()} {collision.Host}:{collision.Port} is already reserved.");
            }

            foreach (var endpoint in endpoints)
            {
                if (!await hostEnvironment.IsPortAvailableAsync(endpoint, cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException(
                        $"Simulator endpoint {endpoint.DisplayText} is already in use by another application.");
                }
            }

            claimed.UnionWith(identities);
            return new PortLease(this, endpoints.ToArray(), identities);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask ReleaseAsync(IReadOnlyList<PortIdentity> identities)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            claimed.ExceptWith(identities);
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record PortIdentity(SimulationEndpointTransport Transport, string Host, int Port)
    {
        public static PortIdentity From(SimulationEndpoint endpoint)
        {
            return new PortIdentity(endpoint.Transport, endpoint.Host.Trim().ToUpperInvariant(), endpoint.Port);
        }
    }

    private sealed class PortLease(
        SimulationPortAllocator owner,
        IReadOnlyList<SimulationEndpoint> endpoints,
        IReadOnlyList<PortIdentity> identities) : ISimulationPortLease
    {
        private int disposed;

        public IReadOnlyList<SimulationEndpoint> Endpoints { get; } = endpoints;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                await owner.ReleaseAsync(identities).ConfigureAwait(false);
            }
        }
    }
}
