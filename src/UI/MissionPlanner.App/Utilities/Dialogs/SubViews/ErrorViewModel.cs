using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;


/// <summary>
/// Represents the view model for the error dialog, providing properties and commands for displaying error messages. 
/// </summary>
public partial class ErrorViewModel : DialogViewModelBase
{

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorViewModel"/> class with the specified application state and options.
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <param name="logger"></param>
    public ErrorViewModel(string errorMessage, ILogger<ErrorViewModel> logger)
    {
        ErrorMessage = errorMessage;
    }
}
