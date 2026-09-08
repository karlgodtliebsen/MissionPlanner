using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;

/// <summary>Presents one summary section for display.</summary>
public sealed class SetupSummarySectionViewModel
{
    /// <summary>Initializes a section view model.</summary>
    /// <param name="section">The summary section.</param>
    public SetupSummarySectionViewModel(SetupSummarySection section)
    {
        Title = section.Title;
        Entries = section.Entries;
    }

    /// <summary>Gets the section title.</summary>
    public string Title
    {
        get;
    }

    /// <summary>Gets the section entries.</summary>
    public IReadOnlyList<SetupSummaryEntry> Entries
    {
        get;
    }
}
