namespace MissionPlanner.App.Theming;

/// <summary>
/// Adapts the current MAUI application appearance events for the theme manager.
/// </summary>
public sealed class MauiThemeEnvironment : IThemeEnvironment
{
    private Application? application;

    /// <inheritdoc />
    public AppTheme RequestedTheme => application?.RequestedTheme ?? AppTheme.Light;

    /// <inheritdoc />
    public event EventHandler<AppTheme>? RequestedThemeChanged;

    /// <inheritdoc />
    public void Attach()
    {
        if (application is not null)
        {
            application.RequestedThemeChanged -= OnRequestedThemeChanged;
        }

        application = Application.Current;
        if (application is not null)
        {
            application.RequestedThemeChanged += OnRequestedThemeChanged;
        }
    }

    /// <inheritdoc />
    public void SetUserTheme(AppTheme theme)
    {
        if (application is not null)
        {
            application.UserAppTheme = theme;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (application is not null)
        {
            application.RequestedThemeChanged -= OnRequestedThemeChanged;
            application = null;
        }
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs args)
    {
        RequestedThemeChanged?.Invoke(this, args.RequestedTheme);
    }
}
