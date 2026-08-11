namespace MissionPlanner.Firmware.Dfu;

/// <summary>Requests one typed provider program-and-verify operation.</summary>
public sealed record DfuProgrammingRequest(
    DfuDeviceDescriptor Device,
    DfuArtifact Artifact,
    bool Verify = true,
    bool RequestDetach = false);
