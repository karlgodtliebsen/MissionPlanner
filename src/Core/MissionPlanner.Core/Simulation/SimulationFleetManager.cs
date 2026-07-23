using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.Core.Simulation;

/// <summary>Creates independent single-session lifecycle coordinators for fleet members.</summary>
public sealed class SimulationSessionManagerFactory(
    ISimulatorProfileValidator profileValidator,
    ISimulatorRuntime runtime,
    IDateTimeProvider clock,
    IOptions<SimulationWorkspaceOptions> options,
    ILoggerFactory loggerFactory) : ISimulationSessionManagerFactory
{
    /// <inheritdoc />
    public ISimulationSessionManager Create() => new SimulationSessionManager(
        profileValidator,
        runtime,
        clock,
        options,
        loggerFactory.CreateLogger<SimulationSessionManager>());
}

/// <summary>Coordinates a set of independently owned simulator runtime sessions.</summary>
public sealed class SimulationFleetManager(
    ISimulationFleetAllocator allocator,
    ISimulationSessionManagerFactory sessionManagerFactory,
    ISimulationOwnershipStore ownershipStore,
    ILogger<SimulationFleetManager> logger) : ISimulationFleetManager
{
    private readonly Dictionary<Guid, Member> members = [];
    private readonly object stateLock = new();
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private Guid? selectedSessionId;
    private bool disposed;

    /// <inheritdoc />
    public IReadOnlyList<SimulationFleetSessionSnapshot> Sessions
    {
        get
        {
            lock (stateLock)
            {
                return members.Values
                    .OrderBy(member => member.Allocation.Index)
                    .Select(CreateSnapshot)
                    .ToArray();
            }
        }
    }

    /// <inheritdoc />
    public Guid? SelectedSessionId
    {
        get
        {
            lock (stateLock)
            {
                return selectedSessionId;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<SimulationFleetChangedEventArgs>? Changed;

    /// <inheritdoc />
    public async Task<SimulationFleetOperationReport> StartAllAsync(
        SimulationFleetLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<Member> terminalMembers;
            lock (stateLock)
            {
                if (members.Values.Any(member => !IsTerminal(member.Manager.Current.State)))
                {
                    throw new InvalidOperationException("Stop all active simulator sessions before allocating another fleet.");
                }

                terminalMembers = members.Values.ToList();
                members.Clear();
                selectedSessionId = null;
            }

            foreach (var member in terminalMembers)
            {
                member.Manager.Changed -= OnMemberChanged;
                await member.Manager.DisposeAsync().ConfigureAwait(false);
            }

            var recovery = await ownershipStore.RecoverOrphansAsync(cancellationToken).ConfigureAwait(false);
            if (recovery.Any(result => result.State is SimulationOrphanRecoveryState.IdentityMismatch or SimulationOrphanRecoveryState.Failed))
            {
                throw new InvalidOperationException(
                    "One or more persisted simulator processes could not be identified safely. Review the ownership recovery diagnostics before launching colliding instances.");
            }

            var allocations = allocator.Allocate(request, []);
            var created = allocations.Select(allocation =>
            {
                var manager = sessionManagerFactory.Create();
                var member = new Member(allocation, manager);
                manager.Changed += OnMemberChanged;
                return member;
            }).ToArray();
            lock (stateLock)
            {
                foreach (var member in created)
                {
                    members.Add(member.Allocation.FleetSessionId, member);
                }

                selectedSessionId = created[0].Allocation.FleetSessionId;
            }

            Publish(created[0]);
            var results = await RunBoundedAsync(
                created,
                request.MaximumConcurrency,
                StartMemberAsync,
                cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Fleet start completed: {Succeeded}/{Total} simulator sessions running.",
                results.Count(result => result.Succeeded),
                results.Count);
            return new SimulationFleetOperationReport(results.OrderBy(ResultIndex).ToArray());
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SimulationFleetOperationReport> StopAllAsync(
        int maximumConcurrency = 3,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (maximumConcurrency is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
        }

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Member[] current;
            lock (stateLock)
            {
                current = members.Values.ToArray();
            }

            var results = await RunBoundedAsync(
                current,
                maximumConcurrency,
                StopMemberAsync,
                cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Fleet stop completed: {Succeeded}/{Total} simulator sessions stopped.",
                results.Count(result => result.Succeeded),
                results.Count);
            return new SimulationFleetOperationReport(results.OrderBy(ResultIndex).ToArray());
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SimulationFleetOperationResult> StopAsync(
        Guid fleetSessionId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Member member;
            lock (stateLock)
            {
                member = GetMember(fleetSessionId);
            }

            return await StopMemberAsync(member, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    /// <inheritdoc />
    public void Select(Guid fleetSessionId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Member member;
        lock (stateLock)
        {
            member = GetMember(fleetSessionId);
            selectedSessionId = fleetSessionId;
        }

        Publish(member);
    }

    /// <inheritdoc />
    public SimulationFleetSessionSnapshot GetRunnableTarget(Guid fleetSessionId)
    {
        lock (stateLock)
        {
            var snapshot = CreateSnapshot(GetMember(fleetSessionId));
            if (snapshot.Session.State != SimulationSessionState.Running || snapshot.VehicleId is null)
            {
                throw new InvalidOperationException($"Simulation session {fleetSessionId} does not have a verified running vehicle target.");
            }

            return snapshot;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        await StopAllAsync().ConfigureAwait(false);
        Member[] current;
        lock (stateLock)
        {
            current = members.Values.ToArray();
            members.Clear();
            selectedSessionId = null;
        }

        foreach (var member in current)
        {
            member.Manager.Changed -= OnMemberChanged;
            await member.Manager.DisposeAsync().ConfigureAwait(false);
        }

        disposed = true;
        operationGate.Dispose();
    }

    private async Task<SimulationFleetOperationResult> StartMemberAsync(Member member, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await member.Manager.StartAsync(member.Allocation.Profile, cancellationToken).ConfigureAwait(false);
            return new SimulationFleetOperationResult(
                member.Allocation.FleetSessionId,
                snapshot.State == SimulationSessionState.Running,
                snapshot,
                snapshot.State == SimulationSessionState.Running ? null : snapshot.Failure ?? snapshot.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Fleet member {FleetSessionId} failed to start.", member.Allocation.FleetSessionId);
            return new SimulationFleetOperationResult(
                member.Allocation.FleetSessionId,
                false,
                member.Manager.Current,
                exception.Message);
        }
    }

    private async Task<SimulationFleetOperationResult> StopMemberAsync(Member member, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await member.Manager.StopAsync(cancellationToken).ConfigureAwait(false);
            var succeeded = snapshot.State == SimulationSessionState.Stopped;
            return new SimulationFleetOperationResult(
                member.Allocation.FleetSessionId,
                succeeded,
                snapshot,
                succeeded ? null : snapshot.Failure ?? snapshot.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Fleet member {FleetSessionId} failed to stop.", member.Allocation.FleetSessionId);
            return new SimulationFleetOperationResult(
                member.Allocation.FleetSessionId,
                false,
                member.Manager.Current,
                exception.Message);
        }
    }

    private static async Task<IReadOnlyList<SimulationFleetOperationResult>> RunBoundedAsync(
        IReadOnlyCollection<Member> source,
        int maximumConcurrency,
        Func<Member, CancellationToken, Task<SimulationFleetOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        using var concurrency = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        var tasks = source.Select(async member =>
        {
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await operation(member, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                concurrency.Release();
            }
        });
        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private void OnMemberChanged(object? sender, SimulationSessionChangedEventArgs args)
    {
        Member? member;
        lock (stateLock)
        {
            member = members.Values.FirstOrDefault(candidate => ReferenceEquals(candidate.Manager, sender));
        }

        if (member is not null)
        {
            Publish(member);
        }
    }

    private void Publish(Member member)
    {
        SimulationFleetSessionSnapshot snapshot;
        lock (stateLock)
        {
            snapshot = CreateSnapshot(member);
        }

        Changed?.Invoke(this, new SimulationFleetChangedEventArgs(snapshot));
    }

    private SimulationFleetSessionSnapshot CreateSnapshot(Member member) =>
        new(member.Allocation, member.Manager.Current, selectedSessionId == member.Allocation.FleetSessionId);

    private Member GetMember(Guid fleetSessionId) => members.TryGetValue(fleetSessionId, out var member)
        ? member
        : throw new KeyNotFoundException($"Simulation fleet session {fleetSessionId} was not found.");

    private int ResultIndex(SimulationFleetOperationResult result) =>
        members.TryGetValue(result.FleetSessionId, out var member) ? member.Allocation.Index : int.MaxValue;

    private static bool IsTerminal(SimulationSessionState state) =>
        state is SimulationSessionState.Stopped or SimulationSessionState.Completed or SimulationSessionState.Failed;

    private sealed record Member(SimulationInstanceAllocation Allocation, ISimulationSessionManager Manager);
}
