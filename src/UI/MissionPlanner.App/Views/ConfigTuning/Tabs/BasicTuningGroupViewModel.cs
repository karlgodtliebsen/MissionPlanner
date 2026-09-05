using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one firmware-curated Basic Tuning group and its scoped actions.</summary>
public sealed partial class BasicTuningGroupViewModel : ObservableObject
{
    private readonly IParameterEditSession session;

    /// <summary>Initializes a group projection.</summary>
    /// <param name="group">The resolved group.</param>
    /// <param name="session">The shared editing session.</param>
    /// <param name="apply">The group apply callback.</param>
    /// <param name="revert">The group revert callback.</param>
    /// <param name="refresh">The group refresh callback.</param>
    public BasicTuningGroupViewModel(ResolvedBasicTuningGroup group, IParameterEditSession session,
        Func<BasicTuningGroupViewModel, Task> apply, Action<BasicTuningGroupViewModel> revert, Func<BasicTuningGroupViewModel, Task> refresh
    )
    {
        Definition = group.Definition;
        this.session = session;
        Parameters = new ObservableCollection<BasicTuningParameterViewModel>(
            group.Fields.Select(item => new BasicTuningParameterViewModel(item, session)));
        ApplyCommand = new AsyncRelayCommand(() => apply(this));
        RevertCommand = new RelayCommand(() => revert(this));
        RefreshCommand = new AsyncRelayCommand(() => refresh(this));
        Refresh();
    }

    /// <summary>Gets the curated group definition.</summary>
    public BasicTuningGroupDefinition Definition
    {
        get;
    }

    /// <summary>Gets the stable group key.</summary>
    public string Key => Definition.Key;

    /// <summary>Gets the group title.</summary>
    public string Title => Definition.Title;

    /// <summary>Gets the group description.</summary>
    public string Description => Definition.Description;

    /// <summary>Gets the optional stability warning.</summary>
    public string? Warning => Definition.Warning;

    /// <summary>Gets whether a group stability warning is present.</summary>
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);

    /// <summary>Gets the displayed tuning fields.</summary>
    public ObservableCollection<BasicTuningParameterViewModel> Parameters
    {
        get;
    }

    /// <summary>Gets the command that validates and applies this group.</summary>
    public IAsyncRelayCommand ApplyCommand
    {
        get;
    }

    /// <summary>Gets the command that reverts this group.</summary>
    public IRelayCommand RevertCommand
    {
        get;
    }

    /// <summary>Gets the command that refreshes this group.</summary>
    public IAsyncRelayCommand RefreshCommand
    {
        get;
    }

    /// <summary>Gets whether this group contains pending changes.</summary>
    [ObservableProperty]
    public partial bool IsModified
    {
        get;
        private set;
    }

    /// <summary>Gets the latest group validation message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    public partial string? ValidationMessage
    {
        get;
        set;
    }

    /// <summary>Gets whether coupled group validation currently fails.</summary>
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    /// <summary>Refreshes all editor values and derived state.</summary>
    public void Refresh()
    {
        foreach (var parameter in Parameters)
        {
            parameter.Refresh(session);
        }

        IsModified = Parameters.Any(parameter => parameter.Editor.IsModified);
    }
}

