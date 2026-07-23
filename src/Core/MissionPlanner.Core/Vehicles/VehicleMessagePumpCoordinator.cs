using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.Core.Vehicles;

/// <summary>Reference-counts a single message-pump subscription for all active transports.</summary>
public sealed class VehicleMessagePumpCoordinator(IServiceFactory serviceFactory) : IVehicleMessagePumpCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IVehicleMessagePump? pump;
    private int references;

    /// <inheritdoc />
    public async Task<IVehicleMessagePumpLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (pump is null)
            {
                pump = serviceFactory.Create<IVehicleMessagePump>();
                await pump.StartAsync(CancellationToken.None).ConfigureAwait(false);
            }

            references++;
            return new Lease(this, pump);
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask ReleaseAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (references == 0 || --references != 0 || pump is null)
            {
                return;
            }

            await pump.DisposeAsync().ConfigureAwait(false);
            pump = null;
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed class Lease(VehicleMessagePumpCoordinator owner, IVehicleMessagePump pump) : IVehicleMessagePumpLease
    {
        private int disposed;

        public IVehicleMessagePump Pump { get; } = pump;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                await owner.ReleaseAsync().ConfigureAwait(false);
            }
        }
    }
}
