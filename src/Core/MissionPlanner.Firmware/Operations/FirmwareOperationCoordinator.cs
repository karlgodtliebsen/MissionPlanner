using Microsoft.Extensions.Logging;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Operations;

/// <summary>Implements global operation ownership and deterministic lifecycle transitions.</summary>
public sealed class FirmwareOperationCoordinator(ILogger<FirmwareOperationCoordinator> logger) : IFirmwareOperationCoordinator
{
    private readonly object sync = new();
    private FirmwareOperationSession? active;

    /// <inheritdoc />
    public IFirmwareOperationSession Begin(FirmwareOperationKind kind)
    {
        lock (sync)
        {
            if (active is not null)
            {
                throw new FirmwareBusyException($"Firmware operation {active.OperationId} is already active.");
            }

            active = new FirmwareOperationSession(kind, logger, Release);
            return active;
        }
    }

    private void Release(FirmwareOperationSession session)
    {
        lock (sync)
        {
            if (ReferenceEquals(active, session)) active = null;
        }
    }

    private sealed class FirmwareOperationSession : IFirmwareOperationSession
    {
        private static readonly IReadOnlyDictionary<FirmwareOperationState, HashSet<FirmwareOperationState>> Transitions =
            CreateTransitions();
        private readonly ILogger logger;
        private readonly Action<FirmwareOperationSession> release;
        private bool disposed;

        public FirmwareOperationSession(
            FirmwareOperationKind kind,
            ILogger logger,
            Action<FirmwareOperationSession> release)
        {
            Kind = kind;
            this.logger = logger;
            this.release = release;
            OperationId = Guid.NewGuid();
            State = FirmwareOperationState.Idle;
            logger.LogInformation("Firmware operation {OperationId} started for {Kind}.", OperationId, Kind);
        }

        public Guid OperationId { get; }
        public FirmwareOperationKind Kind { get; }
        public FirmwareOperationState State { get; private set; }
        public bool CancellationRequested { get; private set; }
        public event EventHandler<FirmwareProgress>? ProgressChanged;

        public void Transition(FirmwareProgress progress)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentNullException.ThrowIfNull(progress);
            if (!Transitions.TryGetValue(State, out var allowed) || !allowed.Contains(progress.State))
            {
                throw new FirmwareStateTransitionException(
                    $"Firmware operation {OperationId} cannot transition from {State} to {progress.State}.");
            }

            var previous = State;
            State = progress.State;
            logger.LogInformation(
                "Firmware operation {OperationId} transitioned from {PreviousState} to {State} ({MessageCode}).",
                OperationId,
                previous,
                State,
                progress.MessageCode);
            ProgressChanged?.Invoke(this, progress);
        }

        public bool RequestCancellation(string messageCode = "operation.cancelled")
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (IsTerminal(State)) return State == FirmwareOperationState.Cancelled;

            CancellationRequested = true;
            if (State is FirmwareOperationState.Erasing or FirmwareOperationState.Programming or
                FirmwareOperationState.Verifying or FirmwareOperationState.Rebooting or
                FirmwareOperationState.WaitingForApplication)
            {
                logger.LogWarning(
                    "Cancellation for firmware operation {OperationId} was deferred in destructive state {State}.",
                    OperationId,
                    State);
                return false;
            }

            Transition(new FirmwareProgress(FirmwareOperationState.Cancelled, null, messageCode));
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            if (!IsTerminal(State))
            {
                throw new FirmwareStateTransitionException(
                    $"Active firmware operation {OperationId} must enter a terminal state before it is released.");
            }

            disposed = true;
            release(this);
        }

        private static bool IsTerminal(FirmwareOperationState state) =>
            state is FirmwareOperationState.Completed or FirmwareOperationState.Cancelled or FirmwareOperationState.Failed;

        private static IReadOnlyDictionary<FirmwareOperationState, HashSet<FirmwareOperationState>> CreateTransitions()
        {
            var map = new Dictionary<FirmwareOperationState, HashSet<FirmwareOperationState>>();
            Add(map, FirmwareOperationState.Idle, FirmwareOperationState.LoadingCatalog, FirmwareOperationState.SelectingFirmware,
                FirmwareOperationState.Downloading, FirmwareOperationState.ValidatingPackage, FirmwareOperationState.WaitingForDevice, FirmwareOperationState.EnteringBootloader);
            map[FirmwareOperationState.Idle].Add(FirmwareOperationState.CheckingCompatibility);
            Add(map, FirmwareOperationState.LoadingCatalog, FirmwareOperationState.SelectingFirmware);
            Add(map, FirmwareOperationState.SelectingFirmware, FirmwareOperationState.Downloading, FirmwareOperationState.ValidatingPackage);
            Add(map, FirmwareOperationState.Downloading, FirmwareOperationState.ValidatingPackage);
            Add(map, FirmwareOperationState.ValidatingPackage, FirmwareOperationState.WaitingForDevice, FirmwareOperationState.EnteringBootloader);
            Add(map, FirmwareOperationState.WaitingForDevice, FirmwareOperationState.EnteringBootloader, FirmwareOperationState.IdentifyingBootloader);
            Add(map, FirmwareOperationState.EnteringBootloader, FirmwareOperationState.WaitingForDevice, FirmwareOperationState.IdentifyingBootloader);
            Add(map, FirmwareOperationState.IdentifyingBootloader, FirmwareOperationState.CheckingCompatibility);
            Add(map, FirmwareOperationState.CheckingCompatibility, FirmwareOperationState.Erasing);
            map[FirmwareOperationState.CheckingCompatibility].Add(FirmwareOperationState.Programming);
            Add(map, FirmwareOperationState.Erasing, FirmwareOperationState.Programming);
            Add(map, FirmwareOperationState.Programming, FirmwareOperationState.Verifying);
            map[FirmwareOperationState.Programming].Add(FirmwareOperationState.Completed);
            Add(map, FirmwareOperationState.Verifying, FirmwareOperationState.Rebooting);
            Add(map, FirmwareOperationState.Rebooting, FirmwareOperationState.WaitingForApplication, FirmwareOperationState.Completed);
            Add(map, FirmwareOperationState.WaitingForApplication, FirmwareOperationState.Completed);

            foreach (var state in Enum.GetValues<FirmwareOperationState>().Where(state => !IsTerminal(state)))
            {
                map.TryAdd(state, []);
                map[state].Add(FirmwareOperationState.Failed);
                if (state is not (FirmwareOperationState.Erasing or FirmwareOperationState.Programming or
                    FirmwareOperationState.Verifying or FirmwareOperationState.Rebooting or FirmwareOperationState.WaitingForApplication))
                {
                    map[state].Add(FirmwareOperationState.Cancelled);
                }
            }

            return map;
        }

        private static void Add(
            IDictionary<FirmwareOperationState, HashSet<FirmwareOperationState>> map,
            FirmwareOperationState state,
            params FirmwareOperationState[] allowed) => map[state] = [.. allowed];
    }
}
