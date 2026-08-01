namespace UraniumUI.Material.Extensions.Samples;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Navigating += OnNavigating;
        Navigated += OnNavigated;
    }


    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        var previous = e.Current?.Location.ToString();
        var current = e.Target?.Location.ToString();
        //navigationEventHub.Publish(new NavigatingEvent(previous, current, e));
        //Shell shell when shell.CurrentPage is not null => shell.CurrentPage,
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        var previous = e.Previous?.Location.ToString();
        var current = e.Current?.Location.ToString();
        //navigationEventHub.Publish(new NavigatedEvent(previous, current, e));
    }
}
