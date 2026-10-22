namespace MissionPlanner.App.Views.Introduction.Models;

/// <summary>
/// Specifies the kind of action that can be performed in the introduction view.
/// </summary>
public enum IntroductionActionKind
{
    /// <summary>Shows another topic of the Introduction.</summary>
    Topic,
    /// <summary>Navigates to an application route.</summary>
    Route,
    /// <summary>Opens an external link.</summary>
    Uri,
    /// <summary>Goes back to the previous page.</summary>
    Back
}

