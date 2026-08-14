using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.Preferences;

/// <summary>
/// Provides the public API for PreferencesPage.
/// </summary>
public partial class PreferencesPage : ExtendedContentPage<PreferencesViewModel>
{
    /// <summary>
    /// Provides the public API for PreferencesPage.
    /// </summary>
    public PreferencesPage()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override async Task OnModelCreatedAsync(PreferencesViewModel viewModel)
    {
        await viewModel.ActivateAsync();
    }
}
