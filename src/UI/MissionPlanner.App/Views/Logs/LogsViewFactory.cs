using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace MissionPlanner.App.Views.Logs;

/// <summary>Explicit UI activation boundary for keyed log views.</summary>
public sealed class LogsViewFactory(IServiceProvider services) : ILogsViewFactory
{
    /// <inheritdoc />
    public Control Create(LogsSection section)
    {
        return !Enum.IsDefined(section)
            ? throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown Logs section.")
            : services.GetRequiredKeyedService<Control>(section);
    }
}
