using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews;

public sealed partial class ChoiceDialogViewModel : DialogViewModelBase
{
    public ChoiceDialogViewModel(IReadOnlyList<string> choices)
    {
        Choices = choices;
        SelectedChoice = choices.FirstOrDefault();
    }

    public IReadOnlyList<string> Choices
    {
        get;
    }

    [ObservableProperty]
    public partial string? SelectedChoice
    {
        get; set;
    }
}
