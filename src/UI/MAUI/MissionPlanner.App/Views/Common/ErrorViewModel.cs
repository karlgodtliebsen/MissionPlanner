using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// 
/// </summary>
public partial class ErrorViewModel : ObservableObject
{
    private readonly ILogger<ErrorViewModel> logger;

    [ObservableProperty] public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorViewModel"/> class with the specified application state and options.
    /// </summary>
    /// <param name="errorMessage"></param>
    /// <param name="logger"></param>
    public ErrorViewModel(string errorMessage, ILogger<ErrorViewModel> logger)
    {
        ErrorMessage = errorMessage;
        this.logger = logger;
    }
}
