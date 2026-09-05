namespace MissionPlanner.App.Navigation;

/// <summary>Implements Setup-to-Config navigation through the application Shell hierarchy.</summary>
public sealed class ShellNavigationService(IDispatcher dispatch) : INavigationService
{
    /// <inheritdoc />
    public Task OpenPageAsync(string destination)
    {
        return dispatch.DispatchAsync(() =>
        {
            var shell = Shell.Current ?? throw new InvalidOperationException("Application Shell is not available.");
            var page = shell.Items.FirstOrDefault(item => string.Equals(item.Title, destination, StringComparison.Ordinal));
            if (page is null)
            {
                throw new InvalidOperationException($"The {destination} workspace is not registered in Shell.");
            }

            shell.CurrentItem = page;
        });
    }


    /// <inheritdoc />
    public Task OpenSubViewAsync(string root, string destination)
    {
        return dispatch.DispatchAsync(() =>
        {
            var shell = Shell.Current ?? throw new InvalidOperationException("Application Shell is not available.");
            var config = shell.Items.FirstOrDefault(item => string.Equals(item.Title, root, StringComparison.Ordinal));
            if (config is null)
            {
                throw new InvalidOperationException($"The {root} workspace is not registered in Shell.");
            }

            var targetSection = config.Items.FirstOrDefault(section =>
                section.Items.Any(content => string.Equals(content.Title, destination, StringComparison.Ordinal)));
            var targetContent = targetSection?.Items.FirstOrDefault(content =>
                string.Equals(content.Title, destination, StringComparison.Ordinal));
            if (targetSection is null || targetContent is null)
            {
                throw new InvalidOperationException($"Page '{destination}' is not registered in Shell.");
            }

            config.CurrentItem = targetSection;
            targetSection.CurrentItem = targetContent;
            shell.CurrentItem = config;
        });
    }
}
