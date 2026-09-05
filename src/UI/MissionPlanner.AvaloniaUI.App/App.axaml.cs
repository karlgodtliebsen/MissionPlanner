using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Views.Main;
using Application = Avalonia.Application;

namespace MissionPlanner.AvaloniaUI.App;

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
        base.OnFrameworkInitializationCompleted();
    }
}
