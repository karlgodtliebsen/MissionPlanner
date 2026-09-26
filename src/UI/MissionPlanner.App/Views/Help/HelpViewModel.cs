using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;

namespace MissionPlanner.App.Views.Help;

/// <summary>Owns the general Help topic catalogue and selected article.</summary>
public partial class HelpViewModel : ViewModelBase
{
    private readonly HelpArticleViewModel installFirmware;
    /// <summary>Creates the Help hub with its first offline topic.</summary>
    public HelpViewModel(FirmwareHelpViewModel firmwareHelp, ILogger<HelpViewModel> logger) : base(logger)
    {
        installFirmware = firmwareHelp.Article;
        Topics = [firmwareHelp.Article];
        SelectedTopic = Topics[0];
    }

    /// <summary>Gets available help topics.</summary>
    public IReadOnlyList<HelpArticleViewModel> Topics { get; }

    /// <summary>Gets or sets the article displayed in the Help hub.</summary>
    [ObservableProperty]
    public partial HelpArticleViewModel? SelectedTopic { get; set; }

    /// <summary>Opens the firmware article independently of the current topic selection.</summary>
    public void SelectInstallFirmware() => SelectedTopic = installFirmware;
}
