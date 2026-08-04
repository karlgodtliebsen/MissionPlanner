namespace MissionPlanner.Firmware.Dfu;

/// <summary>Locates and validates an installed DFU provider tool.</summary>
public interface IDfuToolLocator
{
    /// <summary>Locates and validates the configured or installed tool.</summary>
    Task<DfuToolStatus> LocateAsync(CancellationToken cancellationToken = default);
}

/// <summary>Provides point-in-time USB DFU device snapshots.</summary>
public interface IDfuDeviceCatalog
{
    /// <summary>Gets the current typed USB DFU device snapshot.</summary>
    Task<IReadOnlyList<DfuDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Observes USB DFU device arrival, removal, and driver-state changes.</summary>
public interface IDfuDeviceMonitor
{
    /// <summary>Watches changing typed USB DFU device snapshots.</summary>
    IAsyncEnumerable<IReadOnlyList<DfuDeviceDescriptor>> WatchAsync(CancellationToken cancellationToken = default);
}

/// <summary>Controls a validated DFU provider.</summary>
public interface IDfuProgrammer
{
    /// <summary>Gets capabilities of the validated provider version.</summary>
    Task<DfuProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    /// <summary>Inspects one selected USB DFU device.</summary>
    Task<DfuDeviceInformation> InspectAsync(DfuDeviceDescriptor device, CancellationToken cancellationToken = default);
    /// <summary>Programs and immediately verifies one inspected Intel HEX artifact.</summary>
    Task<DfuProgrammingResult> ProgramAndVerifyAsync(DfuProgrammingRequest request, IProgress<DfuProgress>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Resolves official or local Intel HEX artifacts.</summary>
public interface IDfuArtifactResolver
{
    /// <summary>Resolves, downloads when required, and inspects a requested artifact.</summary>
    Task<DfuArtifact> ResolveAsync(DfuInstallationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Evaluates target evidence without treating an STM32 identity as proof of board platform.</summary>
public interface IDfuTargetSafetyService
{
    /// <summary>Returns a typed allow, warning, or block decision before provider execution.</summary>
    DfuTargetSafetyResult Evaluate(DfuTargetSafetyRequest request);
}

/// <summary>Owns final DFU confirmation and manual recovery prompts in the host UI.</summary>
public interface IDfuUserInteraction
{
    /// <summary>Requests final confirmation after all target evidence is available.</summary>
    Task<bool> ConfirmAsync(DfuInstallationConfirmation confirmation, CancellationToken cancellationToken = default);
    /// <summary>Requests an acknowledged power-cycle or reset action when automatic detach is unavailable.</summary>
    Task<bool> AcknowledgePowerCycleAsync(DfuInstallationConfirmation confirmation, CancellationToken cancellationToken = default);
}

/// <summary>Performs bounded Intel HEX parsing and policy inspection.</summary>
public interface IIntelHexInspector
{
    /// <summary>Parses a bounded Intel HEX stream into sorted validated ranges.</summary>
    Task<DfuArtifactMetadata> InspectAsync(Stream stream, CancellationToken cancellationToken = default);
}

/// <summary>Orchestrates the complete, separately modeled DFU workflow.</summary>
public interface IDfuInstallationService
{
    /// <summary>Runs the separately modeled DFU installation workflow.</summary>
    Task<DfuProgrammingResult> InstallAsync(DfuInstallationRequest request, IProgress<DfuProgress>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Runs only controlled DFU-provider process requests.</summary>
public interface IDfuProcessRunner
{
    /// <summary>Runs a controlled direct provider invocation and captures bounded output.</summary>
    Task<DfuProcessResult> RunAsync(DfuProcessRequest request, IProgress<DfuProcessOutput>? output = null, CancellationToken cancellationToken = default);
}
