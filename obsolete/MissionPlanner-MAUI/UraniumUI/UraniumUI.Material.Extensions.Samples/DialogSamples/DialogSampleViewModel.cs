using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Extensions;
using UraniumUI.Material.Dialogs;

namespace UraniumUI.Material.Extensions.Samples.DialogSamples;

public partial class DialogSampleViewModel(IExtendedDialogService dialogService, IDispatcher dispatcher) : ObservableObject
{
    [ObservableProperty] public partial string Message { get; set; } = "This is a sample message that can be updated dynamically.";

    [RelayCommand]
    private async Task ShowExtendedDialog()
    {
        var view = new DialogContentView { MinimumHeightRequest = 768, MinimumWidthRequest = 1024, BindingContext = this };
        var page = Application.Current!.Windows[0].Page!;

        await dialogService.DisplayViewExtendedAsync(page, "A large view with close resilience", view,
            new ViewDialogOptions { RequestedSize = new Size(1024, 768), CanDismissByTappingOutside = true },
            "OK");
    }

    [RelayCommand]
    private async Task ShowExtendedCloseResilientView()
    {
        var view = new DialogContentView { BindingContext = this, Margin = new Thickness(20) };
        await dialogService.DisplayViewExtendedAsync("A view with close resilience", view, "OK");
    }

    private IDispatcherTimer? timer;
    private IDisposable? disposable;

    [RelayCommand]
    private async Task ShowProgressBar()
    {
        timer?.Stop();
        timer?.DisposeIfDisposable();
        disposable?.Dispose();

        timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (s, e) => Message = "Updated progress message: " + DateTime.Now.ToString("HH:mm:ss");
        timer.Start();

        disposable = await dialogService.DisplayProgressCancellableAsync("Progressing", () => Message, "Cancel");
    }
}
