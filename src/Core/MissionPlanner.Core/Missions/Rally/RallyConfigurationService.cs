using System.Text.Json;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Active-vehicle rally workspace and typed mission-transfer workflow.</summary>
public sealed class RallyConfigurationService(IActiveVehicleContext activeVehicle, IMissionTransferService transfers,
    IRallyProtocolMapper mapper, IVehicleOperationGate operationGate) : IRallyConfigurationService
{
    private readonly Lock sync = new();
    private readonly Dictionary<VehicleId, Workspace> workspaces = [];
    /// <inheritdoc />
    public event EventHandler? Changed;
    /// <inheritdoc />
    public RallyPlanSnapshot GetSnapshot(VehicleId vehicleId) { lock (sync) return Get(vehicleId).Snapshot(vehicleId); }
    /// <inheritdoc />
    public RallyPlanSnapshot SetLocalPlan(VehicleId vehicleId, RallyPlan plan)
    {
        lock (sync) { var workspace = Get(vehicleId); workspace.Local = Freeze(plan); workspace.LocalRevision++; workspace.Dirty = workspace.Vehicle is null || !Equivalent(workspace.Local, workspace.Vehicle); }
        Changed?.Invoke(this, EventArgs.Empty); return GetSnapshot(vehicleId);
    }
    /// <inheritdoc />
    public async Task<RallyOperationResult> DownloadAsync(VehicleId vehicleId, bool replaceLocal, CancellationToken cancellationToken = default)
    {
        if (ScopeError(vehicleId) is { } error) return Failure(vehicleId, error);
        if (!operationGate.TryAcquire(vehicleId, "Rally download", out var lease) || lease is null) return Failure(vehicleId, "Another vehicle operation is active.");
        using (lease) using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken))
        {
            var result = await transfers.DownloadAsync(vehicleId, MissionPlanType.RallyPoints, linked.Token).ConfigureAwait(false);
            if (!result.Success) return Failure(vehicleId, result.Error ?? "Rally download failed.");
            RallyPlan plan; try { plan = mapper.FromProtocol(result.Items); } catch (InvalidDataException exception) { return Failure(vehicleId, exception.Message); }
            lock (sync) { var workspace = Get(vehicleId); workspace.Vehicle = Freeze(plan); workspace.VehicleRevision = (workspace.VehicleRevision ?? 0) + 1; workspace.LastDownload = DateTimeOffset.UtcNow;
                if (replaceLocal) { workspace.Local = Freeze(plan); workspace.LocalRevision++; } workspace.Dirty = !Equivalent(workspace.Local, workspace.Vehicle); }
            Changed?.Invoke(this, EventArgs.Empty); return new(true, $"Downloaded {plan.Points.Count} rally points.", GetSnapshot(vehicleId));
        }
    }
    /// <inheritdoc />
    public async Task<RallyOperationResult> UploadAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        if (ScopeError(vehicleId) is { } error) return Failure(vehicleId, error);
        if (!operationGate.TryAcquire(vehicleId, "Rally upload", out var lease) || lease is null) return Failure(vehicleId, "Another vehicle operation is active.");
        using (lease) using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken))
        {
            var local = GetSnapshot(vehicleId).LocalPlan;
            var result = await transfers.UploadItemsAsync(vehicleId, mapper.ToProtocol(local), MissionPlanType.RallyPoints, cancellationToken: linked.Token).ConfigureAwait(false);
            if (!result.Success) return Failure(vehicleId, result.Error ?? "Rally upload was rejected or unsupported.");
            lock (sync) { var workspace = Get(vehicleId); workspace.Vehicle = Freeze(local); workspace.VehicleRevision = (workspace.VehicleRevision ?? 0) + 1; workspace.Dirty = false; }
            Changed?.Invoke(this, EventArgs.Empty); return new(true, $"Uploaded {local.Points.Count} rally points.", GetSnapshot(vehicleId));
        }
    }
    /// <inheritdoc />
    public async Task<RallyOperationResult> ClearVehicleAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        if (ScopeError(vehicleId) is { } error) return Failure(vehicleId, error);
        if (!operationGate.TryAcquire(vehicleId, "Rally clear", out var lease) || lease is null) return Failure(vehicleId, "Another vehicle operation is active.");
        using (lease) { var result = await transfers.ClearAsync(vehicleId, MissionPlanType.RallyPoints, cancellationToken).ConfigureAwait(false);
            if (!result.Success) return Failure(vehicleId, result.Error ?? "Rally clear was rejected or unsupported."); }
        lock (sync) { var workspace = Get(vehicleId); workspace.Local = RallyPlan.Empty; workspace.Vehicle = RallyPlan.Empty; workspace.LocalRevision++; workspace.VehicleRevision = (workspace.VehicleRevision ?? 0) + 1; workspace.Dirty = false; }
        Changed?.Invoke(this, EventArgs.Empty); return new(true, "Vehicle rally points were cleared.", GetSnapshot(vehicleId));
    }
    private string? ScopeError(VehicleId id) => !activeVehicle.IsOnline || activeVehicle.VehicleId != id ? "Rally operations require the active online vehicle." : null;
    private RallyOperationResult Failure(VehicleId id, string message) => new(false, message, GetSnapshot(id));
    private Workspace Get(VehicleId id) => workspaces.TryGetValue(id, out var value) ? value : workspaces[id] = new();
    private static RallyPlan Freeze(RallyPlan plan) => new(plan.Points.ToArray());
    private static bool Equivalent(RallyPlan a, RallyPlan b) => a.Points.Select(x => (x.Position, x.Altitude)).SequenceEqual(b.Points.Select(x => (x.Position, x.Altitude)));
    private sealed class Workspace { public RallyPlan Local { get; set; } = RallyPlan.Empty; public RallyPlan? Vehicle { get; set; } public long LocalRevision { get; set; } public long? VehicleRevision { get; set; } public bool Dirty { get; set; } public DateTimeOffset? LastDownload { get; set; }
        public RallyPlanSnapshot Snapshot(VehicleId id) => new(id, Local, Vehicle, LocalRevision, VehicleRevision, Dirty, LastDownload); }
}

/// <summary>Bounded versioned rally file codec.</summary>
public sealed class RallyPlanFileCodec : IRallyPlanFileCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    /// <inheritdoc />
    public string Serialize(RallyPlan plan, DateTimeOffset createdAt) => JsonSerializer.Serialize(new Document(1, createdAt, plan), Options);
    /// <inheritdoc />
    public RallyPlan Deserialize(string json)
    {
        if (json.Length > 4 * 1024 * 1024) throw new InvalidDataException("Rally file exceeds the 4 MiB limit.");
        var document = JsonSerializer.Deserialize<Document>(json, Options) ?? throw new InvalidDataException("Rally file is empty.");
        if (document.SchemaVersion != 1 || document.Plan.Points.Any(point => !point.Position.IsValid)) throw new InvalidDataException("Rally file version or coordinates are invalid.");
        return document.Plan;
    }
    private sealed record Document(int SchemaVersion, DateTimeOffset CreatedAt, RallyPlan Plan);
}
