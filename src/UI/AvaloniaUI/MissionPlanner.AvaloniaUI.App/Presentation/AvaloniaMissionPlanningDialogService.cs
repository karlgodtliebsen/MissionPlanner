using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;

namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>Avalonia implementation of mission-planning prompts and choices.</summary>
public sealed class AvaloniaMissionPlanningDialogService(IUiDispatcher dispatcher, IWindowProvider windowProvider)
    : IUserPromptService, IUserChoiceService
{
    /// <summary>Displays an explicit accept/cancel confirmation.</summary>
    public Task<bool> ConfirmAsync(string title, string message, string acceptText,
        CancellationToken cancellationToken = default) => dispatcher.DispatchAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
        var dialog = new Window
        {
            Title = title, Width = 460, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var accept = new Button { Content = acceptText, IsDefault = true, MinWidth = 90 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90 };
        accept.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 14,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8,
                    Children = { cancel, accept }
                }
            }
        };
        using var registration = cancellationToken.Register(() => dispatcher.Dispatch(() => dialog.Close(false)));
        return await dialog.ShowDialog<bool>(owner);
    });

    /// <inheritdoc />
    public Task<string?> PromptAsync(string title, string message, string? initialValue = null,
        CancellationToken cancellationToken = default) => dispatcher.DispatchAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = new TextBox { Text = initialValue ?? string.Empty, MinWidth = 360 };
        return await ShowAsync(title, message, input, () => input.Text, cancellationToken);
    });

    /// <inheritdoc />
    public Task<string?> ChooseAsync(string title, IReadOnlyList<string> choices,
        CancellationToken cancellationToken = default) => dispatcher.DispatchAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (choices.Count == 0)
        {
            return null;
        }

        var choice = new ComboBox
        {
            ItemsSource = choices,
            SelectedIndex = 0,
            MinWidth = 360
        };
        return await ShowAsync(title, "Select an option", choice, () => choice.SelectedItem as string, cancellationToken);
    });

    private async Task<string?> ShowAsync(string title, string message, Control editor, Func<string?> result,
        CancellationToken cancellationToken)
    {
        var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var accept = new Button { Content = "OK", IsDefault = true, MinWidth = 90 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90 };
        accept.Click += (_, _) => dialog.Close(result());
        cancel.Click += (_, _) => dialog.Close(null);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                editor,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, accept }
                }
            }
        };

        using var registration = cancellationToken.Register(() => dispatcher.Dispatch(() => dialog.Close(null)));
        return await dialog.ShowDialog<string?>(owner);
    }
}
