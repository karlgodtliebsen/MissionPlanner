namespace MissionPlanner.Maps.Hosted;

/// <summary>Exception carrying a presentation-safe hosted provider failure category.</summary>
public sealed class HostedMapException : Exception
{
    /// <summary>Initializes a hosted provider exception.</summary>
    public HostedMapException(HostedMapFailureKind kind, string message, Exception? innerException = null) : base(message, innerException) => Kind = kind;

    /// <summary>Gets the failure category.</summary>
    public HostedMapFailureKind Kind { get; }
}
