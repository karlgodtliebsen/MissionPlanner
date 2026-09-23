using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Views.InitSetup.Arming;

/// <summary>Owns Optional Hardware availability and selected-tab state.</summary>
public sealed partial class ArmingViewModel : ViewModelBase
{

    /// <summary>Initializes the workspace.</summary>
    public ArmingViewModel(ILogger<ArmingViewModel> logger) : base(logger)
    {
    }


}

