namespace UraniumUI.Material.Extensions.Samples;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        ArgumentNullException.ThrowIfNull(activationState);
        var window = new Window(new AppShell());
        return window;
    }
}
