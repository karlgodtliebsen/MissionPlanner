using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.AvaloniaUI.App.Views.Introduction.Models;
using MissionPlanner.AvaloniaUI.App.Views.Navigation;

namespace MissionPlanner.AvaloniaUI.App.Views.Introduction;

/// <summary>Displays the bundled MissionPlanner quick guide.</summary>
public partial class IntroductionPage : NavigationViewBase<IntroductionViewModel>
{
    private const double CompactWidth = 820;
    private readonly INavigationService navigationService;
    private readonly IExternalLinkLauncher externalLinkLauncher;

    /// <summary>Initializes the Introduction page.</summary>
    public IntroductionPage()
    {
        InitializeComponent();
        navigationService = ServiceHelper.GetRequiredService<INavigationService>();
        externalLinkLauncher = ServiceHelper.GetRequiredService<IExternalLinkLauncher>();
        SizeChanged += OnPageSizeChanged;
    }

    private void OnPageSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        var compact = Bounds.Width is > 0 and < CompactWidth;
        ContentsBorder.IsVisible = !compact;
        MobileTopicPicker.IsVisible = compact;
        MainGrid.ColumnDefinitions[0].Width = compact ? new GridLength(0) : new GridLength(250);
        Grid.SetColumn(TopicHost, compact ? 0 : 1);
        Grid.SetColumnSpan(TopicHost, compact ? 2 : 1);
    }

    private async void OnActionRequested(object? sender, IntroductionActionRequestedEventArgs args)
    {
        try
        {
            switch (args.Action.Kind)
            {
                case IntroductionActionKind.Topic:
                    ViewModel.SelectTopic(args.Action.Target);
                    break;
                case IntroductionActionKind.Route when !string.IsNullOrWhiteSpace(args.Action.Target):
                    await navigationService.NavigateAsync(args.Action.Target);
                    break;
                case IntroductionActionKind.Uri when Uri.TryCreate(args.Action.Target, UriKind.Absolute, out var uri):
                    await externalLinkLauncher.OpenAsync(uri);
                    break;
                case IntroductionActionKind.Back:
                    await navigationService.GoBackAsync();
                    break;
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Introduction action failed.");
        }
    }
}
