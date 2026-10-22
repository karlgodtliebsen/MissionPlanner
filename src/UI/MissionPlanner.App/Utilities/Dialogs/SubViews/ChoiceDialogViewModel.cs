using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

public sealed partial class ChoiceDialogViewModel : DialogViewModelBase
{
    /// <summary>Creates the view model for the given choices.</summary>
    public ChoiceDialogViewModel(IReadOnlyList<string> choices)
    {
        Choices = choices;
        SelectedChoice = choices.FirstOrDefault();
    }

    /// <summary>Gets the choices to pick from.</summary>
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
