using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using MissionPlanner.Avalonia.UI.ViewModels;
using MissionPlanner.Avalonia.UI.Views;

namespace MissionPlanner.Avalonia.UI;

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
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
