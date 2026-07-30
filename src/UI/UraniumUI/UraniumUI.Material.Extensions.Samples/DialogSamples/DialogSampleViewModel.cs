using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UraniumUI.Material.Dialogs;

namespace UraniumUI.Material.Extensions.Samples.DialogSamples;

public partial class DialogSampleViewModel(IExtendedDialogService dialogService) : ObservableObject
{
    [RelayCommand]
    private async Task ShowExtendedDialog()
    {
        var view = new DialogContentView { MinimumHeightRequest = 768, MinimumWidthRequest = 1024, BindingContext = this };
        var page = Application.Current!.Windows[0].Page!;

        await dialogService.DisplayViewAsync(page, "Show a view", view,
            new ViewDialogOptions { RequestedSize = new Size(1024, 768), CanDismissByTappingOutside = true },
            "OK");
    }
}
