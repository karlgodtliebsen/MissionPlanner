using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Coordinates safe fence parameter edits and typed mission-fence synchronization.</summary>
public sealed class FenceConfigurationService(
    IActiveVehicleContext activeVehicle,
    IVehicleParameterRegistry parameterRegistry,
    IParameterEditSessionFactory editSessions,
    IMissionTransferService missionTransfer,
    IFenceProtocolMapper protocolMapper,
    IFenceGeometryValidator validator,
    IVehicleOperationGate operationGate,
    ILogger<FenceConfigurationService> logger) : IFenceConfigurationService
{
    private static readonly ParameterFieldDefinition[] definitions =
    [
        ParameterFieldDefinition.Exact("FENCE_ENABLE"),
        ParameterFieldDefinition.Exact("FENCE_TYPE"),
        ParameterFieldDefinition.Exact("FENCE_ACTION"),
        ParameterFieldDefinition.Exact("FENCE_ALT_MAX"),
        ParameterFieldDefinition.Exact("FENCE_ALT_MIN"),
        ParameterFieldDefinition.Exact("FENCE_RADIUS"),
        ParameterFieldDefinition.Exact("FENCE_MARGIN"),
        ParameterFieldDefinition.Exact("FENCE_RET_ALT"),
        ParameterFieldDefinition.Exact("FENCE_RET_RALLY"),
        ParameterFieldDefinition.Exact("FENCE_AUTOENABLE"),
        ParameterFieldDefinition.Exact("FENCE_OPTIONS")
    ];

    private readonly object sync = new();
    private readonly Dictionary<VehicleId, Workspace> workspaces = [];

    /// <inheritdoc />
    public IReadOnlyList<ParameterFieldDefinition> ParameterDefinitions => definitions;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public FenceConfigurationSnapshot GetSnapshot(VehicleId vehicleId)
    {
        lock (sync)
        {
            return GetWorkspace(vehicleId).Snapshot(vehicleId);
        }
    }

    /// <inheritdoc />
    public async Task<IParameterEditSession> OpenParameterSessionAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var session = editSessions.Create(vehicleId);
        await session.LoadDefinitionsAsync(definitions, cancellationToken).ConfigureAwait(false);
        return session;
    }

    /// <inheritdoc />
    public bool SupportsTypedGeometry(VehicleId vehicleId)
    {
        var state = activeVehicle.Current;
        if (!state.IsOnline || state.VehicleId != vehicleId || state.State is null)
        {
            return false;
        }

        if (state.State.Identity.Firmware.Supports(MavProtocolCapability.MissionFence))
        {
            return true;
        }

        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        return state.State.Identity.Firmware.Family is
                   FirmwareFamily.ArduCopter or FirmwareFamily.ArduPlane or FirmwareFamily.Rover or FirmwareFamily.ArduSub &&
               (parameters.ContainsKey("FENCE_TOTAL") || parameters.ContainsKey("FENCE_ENABLE"));
    }

    /// <inheritdoc />
    public FenceConfigurationSnapshot SetLocalPlan(VehicleId vehicleId, FencePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        FenceConfigurationSnapshot snapshot;
        lock (sync)
        {
            var workspace = GetWorkspace(vehicleId);
            workspace.LocalPlan = Freeze(plan);
            workspace.LocalRevision++;
            workspace.IsDirty = workspace.VehiclePlan is null || !Equivalent(workspace.LocalPlan, workspace.VehiclePlan);
            snapshot = workspace.Snapshot(vehicleId);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return snapshot;
    }

    /// <inheritdoc />
    public FenceConfigurationSnapshot RestoreBackup(VehicleId vehicleId)
    {
        FenceConfigurationSnapshot snapshot;
        lock (sync)
        {
            var workspace = GetWorkspace(vehicleId);
            if (workspace.BackupPlan is not null)
            {
                workspace.LocalPlan = Freeze(workspace.BackupPlan);
                workspace.LocalRevision++;
                workspace.IsDirty = workspace.VehiclePlan is null || !Equivalent(workspace.LocalPlan, workspace.VehiclePlan);
            }

            snapshot = workspace.Snapshot(vehicleId);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return snapshot;
    }

    /// <inheritdoc />
    public async Task<FenceOperationReport> DownloadAsync(
        VehicleId vehicleId,
        bool replaceLocal,
        IProgress<FenceTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (ScopeError(vehicleId) is { } scopeError)
        {
            return Failure(vehicleId, scopeError);
        }

        if (!SupportsTypedGeometry(vehicleId))
        {
            return Failure(vehicleId, "The active firmware does not advertise typed fence geometry support.");
        }

        logger.LogInformation("Downloading fence geometry for {VehicleId}. ReplaceLocal={ReplaceLocal}.", vehicleId, replaceLocal);
        if (!operationGate.TryAcquire(vehicleId, "Fence download", out var lease) || lease is null)
        {
            return Failure(vehicleId, $"Cannot download the fence while {operationGate.GetCurrentOperation(vehicleId) ?? "another vehicle operation"} is active.");
        }

        using var operationLease = lease;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
        var adapter = new InlineProgress<MissionDownloadProgress>(value =>
            progress?.Report(new FenceTransferProgress("Downloading", value.ReceivedItems, value.TotalItems)));
        MissionDownloadResult transfer;
        try
        {
            transfer = await missionTransfer.DownloadAsync(vehicleId, MissionPlanType.Geofence, adapter, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(vehicleId, "The vehicle connection changed during fence download.");
        }

        if (!transfer.Success)
        {
            return Failure(vehicleId, $"{transfer.Error} Local edits were preserved; {transfer.Items.Count} partial items were ignored.");
        }

        var parsed = protocolMapper.FromProtocol(transfer.Items);
        var geometryValidation = validator.Validate(parsed.Plan);
        if (!parsed.Success || !geometryValidation.IsValid)
        {
            var details = parsed.Errors.Concat(geometryValidation.Issues.Select(issue => issue.Message));
            return Failure(vehicleId, $"Downloaded fence data was invalid: {string.Join(" ", details)}", geometryValidation);
        }

        FenceConfigurationSnapshot snapshot;
        lock (sync)
        {
            var workspace = GetWorkspace(vehicleId);
            if (replaceLocal)
            {
                workspace.BackupPlan = Freeze(workspace.LocalPlan);
                workspace.LocalPlan = Freeze(parsed.Plan);
                workspace.LocalRevision++;
            }

            workspace.VehiclePlan = Freeze(parsed.Plan);
            workspace.VehicleRevision = (workspace.VehicleRevision ?? 0) + 1;
            workspace.IsDirty = !Equivalent(workspace.LocalPlan, workspace.VehiclePlan);
            snapshot = workspace.Snapshot(vehicleId);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        logger.LogInformation("Fence download for {VehicleId} completed with {AreaCount} areas.", vehicleId, parsed.Plan.Areas.Count);
        return new FenceOperationReport(true, $"Downloaded {parsed.Plan.Areas.Count} fence areas.", snapshot, FenceValidationResult.Valid);
    }

    /// <inheritdoc />
    public async Task<FenceOperationReport> ApplyAsync(
        VehicleId vehicleId,
        IParameterEditSession parameterSession,
        IProgress<FenceTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameterSession);
        var scopeError = ScopeError(vehicleId);
        if (scopeError is not null || !parameterSession.IsValid || parameterSession.VehicleId != vehicleId)
        {
            return Failure(vehicleId, scopeError ?? parameterSession.InvalidReason ?? "The parameter session is stale.");
        }

        var snapshot = GetSnapshot(vehicleId);
        var geometry = validator.Validate(snapshot.LocalPlan);
        var parameters = validator.ValidateParameters(parameterSession);
        var validation = new FenceValidationResult(geometry.Issues.Concat(parameters.Issues).ToArray());
        var hasGeometry = snapshot.LocalPlan.Areas.Count > 0 || snapshot.LocalPlan.ReturnPoint is not null;
        if (hasGeometry && !SupportsTypedGeometry(vehicleId))
        {
            validation = new FenceValidationResult(validation.Issues.Append(
                new FenceValidationIssue("capability", "The active firmware does not advertise typed fence geometry support.")).ToArray());
        }

        if (!validation.IsValid)
        {
            return new FenceOperationReport(false, "Fence validation failed.", snapshot, validation);
        }

        if (!operationGate.TryAcquire(vehicleId, "Fence apply", out var lease) || lease is null)
        {
            return new FenceOperationReport(
                false,
                $"Cannot apply the fence while {operationGate.GetCurrentOperation(vehicleId) ?? "another vehicle operation"} is active.",
                snapshot,
                validation);
        }

        using var operationLease = lease;

        logger.LogInformation("Applying fence parameters and {AreaCount} areas to {VehicleId}.", snapshot.LocalPlan.Areas.Count, vehicleId);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
        var parameterNames = definitions
            .SelectMany(definition => definition.ParameterNames)
            .Where(name => parameterSession.GetField(name) is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var parameterReport = await parameterSession.ApplyAsync(parameterNames, linked.Token).ConfigureAwait(false);
        if (!parameterReport.Success)
        {
            return new FenceOperationReport(false, "One or more fence parameters were not confirmed; geometry was not replaced.", GetSnapshot(vehicleId), validation, parameterReport);
        }

        if (!SupportsTypedGeometry(vehicleId) && !hasGeometry)
        {
            return new FenceOperationReport(
                true,
                "Fence parameters were confirmed. Typed geometry is not supported by this firmware.",
                GetSnapshot(vehicleId),
                validation,
                parameterReport);
        }

        var items = protocolMapper.ToProtocol(snapshot.LocalPlan);
        lock (sync)
        {
            var workspace = GetWorkspace(vehicleId);
            workspace.BackupPlan = Freeze(workspace.VehiclePlan ?? workspace.LocalPlan);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        var adapter = new InlineProgress<MissionUploadProgress>(value =>
            progress?.Report(new FenceTransferProgress("Uploading", value.SentItems, value.TotalItems)));
        MissionUploadResult transfer;
        try
        {
            transfer = await missionTransfer.UploadItemsAsync(
                vehicleId,
                items,
                MissionPlanType.Geofence,
                adapter,
                linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new FenceOperationReport(false, "The vehicle connection changed during fence upload; local edits and backup were preserved.", GetSnapshot(vehicleId), validation, parameterReport);
        }

        if (!transfer.Success)
        {
            return new FenceOperationReport(false, transfer.Error ?? "The vehicle rejected fence geometry.", GetSnapshot(vehicleId), validation, parameterReport);
        }

        lock (sync)
        {
            var workspace = GetWorkspace(vehicleId);
            workspace.VehiclePlan = Freeze(workspace.LocalPlan);
            workspace.VehicleRevision = (workspace.VehicleRevision ?? 0) + 1;
            workspace.IsDirty = false;
            snapshot = workspace.Snapshot(vehicleId);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        logger.LogInformation("Fence upload for {VehicleId} completed and was acknowledged.", vehicleId);
        return new FenceOperationReport(true, "Fence parameters and geometry were confirmed by the vehicle.", snapshot, validation, parameterReport);
    }

    /// <inheritdoc />
    public async Task<FenceOperationReport> ClearAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        if (ScopeError(vehicleId) is { } scopeError)
        {
            return Failure(vehicleId, scopeError);
        }

        if (!SupportsTypedGeometry(vehicleId))
        {
            return Failure(vehicleId, "The active firmware does not advertise typed fence geometry support.");
        }

        if (!operationGate.TryAcquire(vehicleId, "Fence clear", out var lease) || lease is null)
        {
            return Failure(vehicleId, $"Cannot clear the fence while {operationGate.GetCurrentOperation(vehicleId) ?? "another vehicle operation"} is active.");
        }

        using var operationLease = lease;

        lock (sync)
        {
            var workspace = GetWorkspace(vehicleId);
            workspace.BackupPlan = Freeze(workspace.LocalPlan.Areas.Count > 0 || workspace.LocalPlan.ReturnPoint is not null
                ? workspace.LocalPlan
                : workspace.VehiclePlan ?? FencePlan.Empty);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        logger.LogInformation("Clearing fence geometry for {VehicleId}.", vehicleId);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
        MissionUploadResult result;
        try
        {
            result = await missionTransfer.ClearAsync(vehicleId, MissionPlanType.Geofence, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(vehicleId, "The vehicle connection changed before fence clear was acknowledged.");
        }

        if (!result.Success)
        {
            return Failure(vehicleId, result.Error ?? "The vehicle rejected fence clear.");
        }

        FenceConfigurationSnapshot snapshot;
        lock (sync)
        {
            var workspace = GetWorkspace(vehicleId);
            workspace.LocalPlan = FencePlan.Empty;
            workspace.VehiclePlan = FencePlan.Empty;
            workspace.LocalRevision++;
            workspace.VehicleRevision = (workspace.VehicleRevision ?? 0) + 1;
            workspace.IsDirty = false;
            snapshot = workspace.Snapshot(vehicleId);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return new FenceOperationReport(true, "Vehicle fence geometry was cleared and acknowledged; a local backup is available.", snapshot, FenceValidationResult.Valid);
    }

    private FenceOperationReport Failure(VehicleId vehicleId, string message, FenceValidationResult? validation = null)
    {
        return new FenceOperationReport(false, message, GetSnapshot(vehicleId), validation ?? FenceValidationResult.Valid);
    }

    private string? ScopeError(VehicleId vehicleId)
    {
        var snapshot = activeVehicle.Current;
        return !snapshot.IsOnline || snapshot.VehicleId != vehicleId
            ? "Fence operations require the target vehicle to be active and online."
            : null;
    }

    private Workspace GetWorkspace(VehicleId vehicleId)
    {
        if (!workspaces.TryGetValue(vehicleId, out var workspace))
        {
            workspace = new Workspace();
            workspaces.Add(vehicleId, workspace);
        }

        return workspace;
    }

    private bool Equivalent(FencePlan first, FencePlan second)
    {
        try
        {
            return protocolMapper.ToProtocol(first).SequenceEqual(protocolMapper.ToProtocol(second));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static FencePlan Freeze(FencePlan plan)
    {
        return new FencePlan(
            plan.ReturnPoint,
            plan.Areas.Select(area => area with { Vertices = area.Vertices.ToArray() }).ToArray());
    }

    private sealed class Workspace
    {
        public FencePlan LocalPlan { get; set; } = FencePlan.Empty;

        public FencePlan? VehiclePlan { get; set; }

        public FencePlan? BackupPlan { get; set; }

        public long LocalRevision { get; set; }

        public long? VehicleRevision { get; set; }

        public bool IsDirty { get; set; }

        public FenceConfigurationSnapshot Snapshot(VehicleId vehicleId)
        {
            return new FenceConfigurationSnapshot(
                vehicleId,
                Freeze(LocalPlan),
                VehiclePlan is null ? null : Freeze(VehiclePlan),
                BackupPlan is null ? null : Freeze(BackupPlan),
                LocalRevision,
                VehicleRevision,
                IsDirty);
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value)
        {
            report(value);
        }
    }
}
