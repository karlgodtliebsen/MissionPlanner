using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents one optional-hardware module as an independent group of settings.</summary>
public sealed class OptionalHardwareModuleViewModel
{
    /// <summary>Initializes a module group.</summary>
    /// <param name="module">The module projection.</param>
    /// <param name="action">The owning workflow.</param>
    public OptionalHardwareModuleViewModel(OptionalHardwareModuleView module, Action<(string, double)> action)
    {
        Title = module.Title;
        Description = module.Description;
        Issues = module.Issues.Select(issue => $"[{issue.Severity}] {issue.Message}").ToArray();
        Settings = module.Settings.Select(setting => new PeripheralSettingViewModel(setting, action)).ToArray();
    }

    /// <summary>Gets the module title.</summary>
    public string Title { get; }

    /// <summary>Gets the module description.</summary>
    public string Description { get; }

    /// <summary>Gets the module configuration issues.</summary>
    public IReadOnlyList<string> Issues { get; }

    /// <summary>Gets whether the module has issues.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Gets the module settings.</summary>
    public IReadOnlyList<PeripheralSettingViewModel> Settings { get; }
}
