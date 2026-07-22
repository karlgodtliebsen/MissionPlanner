namespace MissionPlanner.App.Views.InitSetup.Tabs;

/// <summary>Implements Setup-to-Config navigation through the application Shell hierarchy.</summary>
public sealed class ShellSetupNavigationService : ISetupNavigationService
{
    /// <inheritdoc />
    public Task OpenConfigAsync(string destination)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var shell = Shell.Current ?? throw new InvalidOperationException("Application Shell is not available.");
            var config = shell.Items.FirstOrDefault(item => string.Equals(item.Title, "Config", StringComparison.Ordinal));
            if (config is null)
            {
                throw new InvalidOperationException("The Config workspace is not registered in Shell.");
            }

            var targetSection = config.Items.FirstOrDefault(section =>
                section.Items.Any(content => string.Equals(content.Title, destination, StringComparison.Ordinal)));
            var targetContent = targetSection?.Items.FirstOrDefault(content =>
                string.Equals(content.Title, destination, StringComparison.Ordinal));
            if (targetSection is null || targetContent is null)
            {
                throw new InvalidOperationException($"Config page '{destination}' is not registered in Shell.");
            }

            config.CurrentItem = targetSection;
            targetSection.CurrentItem = targetContent;
            shell.CurrentItem = config;
        });
    }
}
