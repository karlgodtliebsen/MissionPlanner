namespace MissionPlanner.Firmware.Dfu;

/// <summary>Identifies the DFU installation lifecycle.</summary>
public enum DfuOperationState
{
    /// <summary>No DFU operation is active.</summary>
    Idle,
    /// <summary>The external provider is being located.</summary>
    LocatingTool,
    /// <summary>The workflow is waiting for a DFU USB device.</summary>
    WaitingForDevice,
    /// <summary>The provider is inspecting MCU and driver evidence.</summary>
    InspectingDevice,
    /// <summary>An official or local artifact is being resolved.</summary>
    ResolvingArtifact,
    /// <summary>The resolved artifact is downloading.</summary>
    DownloadingArtifact,
    /// <summary>Intel HEX structure and address policy are being inspected.</summary>
    InspectingHex,
    /// <summary>The workflow is waiting for explicit target confirmation.</summary>
    AwaitingConfirmation,
    /// <summary>The provider is erasing or writing flash.</summary>
    Programming,
    /// <summary>The provider is verifying programmed flash.</summary>
    Verifying,
    /// <summary>The provider is requesting detach, reset, or start.</summary>
    Detaching,
    /// <summary>The workflow is waiting for application firmware to enumerate.</summary>
    WaitingForApplication,
    /// <summary>Programming and required verification completed.</summary>
    Completed,
    /// <summary>The operation stopped at a safe boundary.</summary>
    Cancelled,
    /// <summary>The operation failed.</summary>
    Failed
}

/// <summary>Describes the host driver state for a DFU USB device.</summary>
public enum DfuDriverState
{
    /// <summary>No matching DFU USB device is present.</summary>
    NotPresent,
    /// <summary>The device and expected provider driver are ready.</summary>
    PresentReady,
    /// <summary>The device is present with an incompatible driver.</summary>
    PresentWrongDriver,
    /// <summary>Windows reports a device or driver problem.</summary>
    PresentWithProblem,
    /// <summary>The provider reports that the device is busy.</summary>
    Busy,
    /// <summary>Available evidence cannot determine driver readiness.</summary>
    Unknown
}

/// <summary>Describes STM32CubeProgrammer availability.</summary>
public enum DfuToolAvailability
{
    /// <summary>A validated supported tool is available.</summary>
    Available,
    /// <summary>No installation was discovered.</summary>
    NotInstalled,
    /// <summary>A configured or discovered path is invalid.</summary>
    PathInvalid,
    /// <summary>The discovered tool version is not supported.</summary>
    UnsupportedVersion,
    /// <summary>The host prevented validation or execution.</summary>
    ExecutionBlocked
}

/// <summary>Contains validated external-tool discovery evidence.</summary>
public sealed record DfuToolStatus(
    DfuToolAvailability Availability,
    string? ExecutablePath = null,
    Version? Version = null,
    string? Diagnostic = null);

/// <summary>Describes version-dependent operations supported by a DFU provider.</summary>
public sealed record DfuProviderCapabilities(
    bool CanListDevices,
    bool CanInspectDevice,
    bool CanProgramIntelHex,
    bool CanVerify,
    bool CanDetach,
    bool CanSafelyCancelProgramming,
    Version? ProviderVersion = null);

/// <summary>Identifies one USB DFU device without pretending it is a serial port.</summary>
public sealed record DfuDeviceDescriptor(
    string ProviderId,
    ushort VendorId,
    ushort ProductId,
    DfuDriverState DriverState,
    string? ProductName = null,
    string? Manufacturer = null,
    string? SerialNumber = null,
    string? DevicePath = null,
    string? PnpInstanceId = null,
    string? DriverProvider = null,
    string? DriverVersion = null,
    int? ProblemCode = null,
    DateTimeOffset? ObservedAt = null,
    DateTimeOffset? ArrivedAt = null,
    DateTimeOffset? RemovedAt = null,
    int? ProviderUsbIndex = null);

/// <summary>Contains provider-reported MCU evidence that does not prove the flight-controller PCB.</summary>
public sealed record DfuDeviceInformation(
    DfuDeviceDescriptor Device,
    string? McuDeviceId,
    string? Revision,
    long? InternalFlashBytes,
    IReadOnlyList<DfuMemoryRange> WritableRanges,
    IReadOnlyList<string> Warnings,
    string? ProviderLog = null);

/// <summary>Contains one bounded contiguous Intel HEX data range.</summary>
public sealed record DfuMemoryRange
{
    /// <summary>Initializes a range and takes an immutable copy of its data.</summary>
    public DfuMemoryRange(uint startAddress, ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty) throw new ArgumentException("A DFU memory range cannot be empty.", nameof(data));
        if ((ulong)startAddress + (ulong)data.Length - 1 > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(data), "The DFU memory range exceeds the 32-bit address space.");
        StartAddress = startAddress;
        Data = data.ToArray();
    }

    /// <summary>Gets the first represented address.</summary>
    public uint StartAddress { get; }
    /// <summary>Gets an immutable snapshot of represented bytes.</summary>
    public ReadOnlyMemory<byte> Data { get; }
    /// <summary>Gets the inclusive final represented address.</summary>
    public uint EndAddress => checked(StartAddress + (uint)Data.Length - 1);
}

/// <summary>Contains inspected Intel HEX metadata and provenance.</summary>
public sealed record DfuArtifactMetadata(
    long SourceBytes,
    long DataBytes,
    uint LowestAddress,
    uint HighestAddress,
    string Sha256,
    IReadOnlyList<DfuMemoryRange> Ranges,
    IReadOnlyList<string> Warnings,
    uint? EntryAddress = null,
    bool AppearsToContainBootloader = false,
    DateTimeOffset? InspectedAt = null,
    bool AppearsToContainApplication = false);

/// <summary>Represents a locally available, inspected DFU artifact.</summary>
public sealed record DfuArtifact(
    string FileName,
    string LocalPath,
    DfuArtifactMetadata Metadata,
    Uri? SourceUri = null,
    string? Platform = null,
    int? BoardId = null);

/// <summary>Requests one typed provider program-and-verify operation.</summary>
public sealed record DfuProgrammingRequest(
    DfuDeviceDescriptor Device,
    DfuArtifact Artifact,
    bool Verify = true,
    bool RequestDetach = false);

/// <summary>Describes a typed DFU failure.</summary>
public sealed record DfuFailure(string Code, DfuOperationState Stage, string Message, string? TechnicalDetail = null);

/// <summary>Reports DFU operation progress.</summary>
public sealed record DfuProgress(
    DfuOperationState State,
    string MessageCode,
    double? Percentage = null,
    long? CompletedBytes = null,
    long? TotalBytes = null,
    string? TechnicalDetail = null);

/// <summary>Contains a provider or orchestration result.</summary>
public sealed record DfuProgrammingResult(
    DfuOperationState State,
    bool ProgrammingSucceeded,
    bool VerificationSucceeded,
    bool ApplicationRediscovered,
    DfuFailure? Failure = null,
    string? ProviderLog = null,
    int? ExitCode = null,
    DfuProgrammingOutcome Outcome = DfuProgrammingOutcome.ProgrammingFailed);

/// <summary>Identifies the conservative outcome of a provider program-and-verify operation.</summary>
public enum DfuProgrammingOutcome
{
    /// <summary>The external tool was not available.</summary>
    ToolNotFound,
    /// <summary>No selected USB DFU device was available.</summary>
    NoDfuDevice,
    /// <summary>The provider could not connect to the selected device.</summary>
    ConnectionFailed,
    /// <summary>The provider rejected the firmware file.</summary>
    FileRejected,
    /// <summary>The provider reported an erase failure.</summary>
    EraseFailed,
    /// <summary>The provider did not prove programming success.</summary>
    ProgrammingFailed,
    /// <summary>The provider reported or failed to prove verification success.</summary>
    VerificationFailed,
    /// <summary>A requested detach operation failed.</summary>
    DetachFailed,
    /// <summary>Programming and immediate verification both succeeded.</summary>
    Succeeded
}

/// <summary>Requests the complete DFU installation use case.</summary>
public sealed record DfuInstallationRequest(
    string SelectedPlatform,
    int? SelectedBoardId,
    DfuDeviceDescriptor Device,
    DfuArtifact? Artifact = null,
    Uri? ArtifactSource = null,
    string? ConfirmationPhrase = null);

/// <summary>Contains one timestamped external-provider output line.</summary>
public sealed record DfuProcessOutput(DateTimeOffset Timestamp, bool IsError, string Text);

/// <summary>Restricts external-process arguments to a known DFU provider operation.</summary>
public enum DfuProcessPurpose
{
    /// <summary>Runs a non-mutating help or version probe.</summary>
    ValidateTool,
    /// <summary>Lists USB DFU devices.</summary>
    ListDevices,
    /// <summary>Inspects one selected USB DFU device.</summary>
    InspectDevice,
    /// <summary>Programs and verifies one validated Intel HEX artifact.</summary>
    ProgramAndVerify
}

/// <summary>Describes a controlled direct process invocation.</summary>
public sealed record DfuProcessRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    TimeSpan StartupTimeout,
    TimeSpan ExecutionTimeout,
    bool MayKillProcessTreeOnCancellation = false,
    DfuProcessPurpose Purpose = DfuProcessPurpose.ValidateTool);

/// <summary>Contains bounded process output and termination evidence.</summary>
public sealed record DfuProcessResult(
    int? ExitCode,
    IReadOnlyList<DfuProcessOutput> Output,
    bool TimedOut = false,
    bool WasCancelled = false,
    string? FailureCode = null,
    bool OutputTruncated = false);
