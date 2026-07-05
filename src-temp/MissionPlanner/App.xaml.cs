namespace MissionPlanner;

/// <summary>
/// Represents the main application.
/// </summary>
public partial class App : Application
{
    private readonly AppShell _shell;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <param name="shell">The application shell.</param>
    public App(AppShell shell)
    {
        _shell = shell;
        InitializeComponent();
    }

    /// <summary>
    /// Creates the main window for the application.
    /// </summary>
    /// <param name="activationState">The activation state.</param>
    /// <returns>The main window.</returns>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_shell);
    }
}