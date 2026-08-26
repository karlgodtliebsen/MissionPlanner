using System.ComponentModel;
using System.Diagnostics;
using MissionPlanner.App.Navigation;
using MissionPlanner.App.Views.Introduction.Models;
using MissionPlanner.Library;

namespace MissionPlanner.App.Views.Introduction;

/// <summary>
/// Interaction logic for IntroductionPage.xaml
/// </summary>
public partial class IntroductionPage : ExtendedContentPage<IntroductionViewModel>
{
    private const double CompactWidth = 820;
    private int topicTransitionVersion;


    /// <summary>
    /// Initializes a new instance of the <see cref="IntroductionPage"/> class.
    /// </summary>
    public IntroductionPage()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override async Task OnActivateAsync()
    {
        DomainException.ThrowIfNull(ViewModel);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SizeChanged += OnPageSizeChanged;
        await base.OnActivateAsync();
        QueueTopicPresentation(ViewModel.SelectedTopic);
    }

    /// <inheritdoc />
    protected override Task OnDeactivateAsync()
    {
        DomainException.ThrowIfNull(ViewModel);
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        SizeChanged -= OnPageSizeChanged;
        return base.OnDeactivateAsync();
    }


    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IntroductionViewModel.SelectedTopic) && sender is IntroductionViewModel viewModel)
        {
            QueueTopicPresentation(viewModel.SelectedTopic);
        }
    }

    private void QueueTopicPresentation(IntroductionTopic? topic)
    {
        var version = ++topicTransitionVersion;
        Dispatcher.Dispatch(() =>
        {
            TopicView.IsVisible = false;
            TopicView.BindingContext = null;
            if (version != topicTransitionVersion || ViewModel?.SelectedTopic != topic)
            {
                return;
            }
            TopicView.BindingContext = topic;
            TopicView.IsVisible = topic is not null;
        });
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
                    var target = action.Target;
                    // The action button belongs to the topic tree that SelectTopic replaces.
                    // Let its native Clicked callback return before detaching that tree.
                    Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1), () => ViewModel?.SelectTopic(target));
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
        catch (Exception ex)
        {
            Debug.Print("Exception in OnActionRequested\n" + ex.Message);
            // Introduction navigation should never make the page unusable.
            // Application-level navigation logging can be added here if desired.
        }
    }
}
