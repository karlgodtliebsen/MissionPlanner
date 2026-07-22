using Microsoft.Extensions.Options;
using UraniumUI.Dialogs;
using UraniumUI.Infrastructure;
using UraniumUI.Resources;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// Provides extended dialog services.
/// </summary>
public class ExtendedDialogService : DefaultDialogService, IExtendedDialogService
{
    private readonly IDispatcher dispatcher;

    /// <inheritdoc />
    public ExtendedDialogService(IDispatcher dispatcher, IOptions<DialogOptions> options) : base(options)
    {
        this.dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public async Task<IDisposable> DisplayProgressCancellableAsync(string title, Func<string> message, string cancelText = "Cancel", CancellationTokenSource? tokenSource = default)
    {
        tokenSource ??= new CancellationTokenSource();

        var progress = new ActivityIndicator
        {
            IsRunning = true,
            IsVisible = true,
            HorizontalOptions = LayoutOptions.Center,
            Color = ColorResource.GetColor("Primary", "PrimaryDark", Colors.Blue),
            Margin = 20
        };
        var label = new Label { Text = message(), Margin = 20 };
        var verticalStackLayout = new VerticalStackLayout { Children = { GetHeader(title), label, progress } };

        if (!string.IsNullOrEmpty(cancelText))
        {
            verticalStackLayout.Children.Add(GetDivider());
            verticalStackLayout.Children.Add(GetFooter(new Dictionary<string, Command> { { cancelText, new Command(() => tokenSource?.Cancel()) } }));
        }

        var popupPage = new DefaultDialogAnimatedContentPage { BackgroundColor = GetBackdropColor(), Content = GetFrame(Page.Width, verticalStackLayout) };

        await dispatcher.DispatchAsync(async () => await Page.Navigation.PushModalAsync(ConfigurePopupPage(popupPage), false));

        var timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.Tick += (s, e) => label.Text = message();

        var cancelAction = new DisposableAction(() =>
        {
            timer.Stop();
            dispatcher.DispatchAsync(() =>
            {
                if (Page.Navigation.ModalStack.LastOrDefault() == popupPage)
                {
                    Page.Navigation.PopModalAsync(false);
                }
            });
        });

        tokenSource.Token.Register(cancelAction.Dispose);
        timer.Start();
        return cancelAction;
    }
}
