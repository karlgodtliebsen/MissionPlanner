using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace UraniumUI.Material.Extensions.Samples.AppViewModels;

/// <summary>
/// Synchronizes the Shell theme selector
public sealed partial class ThemeChangeViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<ThemeChangeViewModel> logger;
    private bool disposed;

    /// <summary>Initializes the theme selector.</summary>
    /// <param name="logger">The logger.</param>
    public ThemeChangeViewModel(ILogger<ThemeChangeViewModel> logger)
    {
        this.logger = logger;
        SelectedTheme = AppTheme.Dark;
    }

    /// <summary>Gets the system, light, and dark theme choices.</summary>
    public AppTheme[] AppThemeList { get; } = [AppTheme.Light, AppTheme.Dark];

    /// <summary>Gets the selected MAUI application theme.</summary>
    [ObservableProperty]
    public partial AppTheme SelectedTheme { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
    }
}
