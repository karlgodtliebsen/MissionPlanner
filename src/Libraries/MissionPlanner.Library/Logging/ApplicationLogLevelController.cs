using Serilog.Core;
using Serilog.Events;

namespace MissionPlanner.Library.Logging;

/// <summary>Controls the session's default diagnostic verbosity without changing configuration files.</summary>
public interface IApplicationLogLevelController
{
    /// <summary>Current global minimum severity.</summary>
    LogEventLevel MinimumLevel { get; }
    /// <summary>Changes the runtime minimum; category overrides remain authoritative.</summary>
    void SetMinimumLevel(LogEventLevel level);
}

/// <summary>Shares the same level switch used by the application logger.</summary>
public sealed class ApplicationLogLevelController(LoggingLevelSwitch levelSwitch) : IApplicationLogLevelController
{
    /// <inheritdoc />
    public LogEventLevel MinimumLevel => levelSwitch.MinimumLevel;

    /// <inheritdoc />
    public void SetMinimumLevel(LogEventLevel level)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        levelSwitch.MinimumLevel = level;
    }
}
