namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies a verified SITL package archive format.</summary>
public enum SitlArchiveFormat
{
    /// <summary>ZIP archive.</summary>
    Zip,

    /// <summary>GZip-compressed POSIX tar archive.</summary>
    TarGzip
}
