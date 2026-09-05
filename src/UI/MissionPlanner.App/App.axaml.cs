using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.Main;

namespace MissionPlanner.App;

/// <summary>
/// The main application class for the Avalonia UI.
/// </summary>
/// <param name="serviceProvider">The service provider for dependency injection.</param>
public partial class App(IServiceProvider serviceProvider) : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
            ServiceProvider
                .GetRequiredService<IWindowProvider>()
                .SetMainWindow(mainWindow);


        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
