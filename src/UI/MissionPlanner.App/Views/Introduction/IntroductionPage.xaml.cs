using MissionPlanner.App.Navigation;
using MissionPlanner.App.Views.Introduction.Models;
using MissionPlanner.Library;

namespace MissionPlanner.App.Views.Introduction;

/// <summary>
/// Interaction logic for IntroductionPage.xaml
/// </summary>
public partial class IntroductionPage : ExtendedContentPage<IntroductionViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntroductionPage"/> class.
    /// </summary>
    public IntroductionPage()
    {
        InitializeComponent();
    }

    private const double CompactWidth = 820;


    /// <inheritdoc />
    protected override async Task OnModelCreatedAsync(IntroductionViewModel viewModel)
    {
        DomainException.ThrowIfNull(viewModel);
        SizeChanged += OnPageSizeChanged;
        await base.OnModelCreatedAsync(viewModel);
        await viewModel.InitializeAsync();
    }

    /// <inheritdoc />
    protected override void OnDestroyingModel(IntroductionViewModel viewModel)
    {
        base.OnDestroyingModel(viewModel);
        SizeChanged -= OnPageSizeChanged;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        var compact = Width is > 0 and < CompactWidth;

        ContentsBorder.IsVisible = !compact;
        MobileTopicPicker.IsVisible = compact;

        MainGrid.ColumnDefinitions[0].Width = compact
            ? new GridLength(0)
            : new GridLength(250);

        Grid.SetColumn(TopicHost, compact ? 0 : 1);
        Grid.SetColumnSpan(TopicHost, compact ? 2 : 1);
    }


    private async void OnActionRequested(object? sender, IntroductionActionRequestedEventArgs e)
    {
        var action = e.Action;

        try
        {
            switch (action.Kind)
            {
                case IntroductionActionKind.Topic:
                    ViewModel?.SelectTopic(action.Target);
                    break;

                case IntroductionActionKind.Route:
                    if (!string.IsNullOrWhiteSpace(action.Target) && Shell.Current is not null)
                    {
                        await Shell.Current.GoToAsync(action.Target);
                    }

                    break;

                case IntroductionActionKind.Uri:
                    if (Uri.TryCreate(action.Target, UriKind.Absolute, out var uri))
                    {
                        await Launcher.Default.OpenAsync(uri);
                    }

                    break;

                case IntroductionActionKind.Back:
                    if (Shell.Current is not null)
                    {
                        await Shell.Current.GoToAsync("..");
                    }

                    break;
            }
        }
        catch (Exception)
        {
            // Introduction navigation should never make the page unusable.
            // Application-level navigation logging can be added here if desired.
        }
    }
}
