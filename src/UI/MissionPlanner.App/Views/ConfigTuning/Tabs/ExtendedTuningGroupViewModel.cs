using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Provides one lazy, searchable advanced descriptor group.</summary>
public sealed partial class ExtendedTuningGroupViewModel : ObservableObject
{
    private readonly ResolvedAdvancedTuningGroup resolved;
    private readonly IParameterEditSession session;
    private readonly IExtendedTuningService service;
    private readonly List<AdvancedTuningFieldViewModel> materialized = [];
    private string filter = string.Empty;

    /// <summary>Initializes a lazy descriptor group.</summary>
    /// <param name="resolved">The presence-gated descriptor.</param>
    /// <param name="session">The shared parameter session.</param>
    /// <param name="service">The advanced tuning service.</param>
    /// <param name="apply">The group apply callback.</param>
    /// <param name="revert">The group revert callback.</param>
    /// <param name="refresh">The group refresh callback.</param>
    /// <param name="previewCopy">The axis-copy preview callback.</param>
    /// <param name="applyCopy">The reviewed-preview apply callback.</param>
    public ExtendedTuningGroupViewModel(
        ResolvedAdvancedTuningGroup resolved,
        IParameterEditSession session,
        IExtendedTuningService service,
        Func<ExtendedTuningGroupViewModel, Task> apply,
        Action<ExtendedTuningGroupViewModel> revert,
        Func<ExtendedTuningGroupViewModel, Task> refresh,
        Action<ExtendedTuningGroupViewModel> previewCopy,
        Func<ExtendedTuningGroupViewModel, Task> applyCopy)
    {
        this.resolved = resolved;
        this.session = session;
        this.service = service;
        Axes = resolved.Axes;
        SelectedSourceAxis = Axes.FirstOrDefault();
        SelectedTargetAxis = Axes.Skip(1).FirstOrDefault();
        ToggleExpandedCommand = new RelayCommand(ToggleExpanded);
        ApplyCommand = new AsyncRelayCommand(() => apply(this));
        RevertCommand = new RelayCommand(() => revert(this));
        RefreshCommand = new AsyncRelayCommand(() => refresh(this));
        PreviewCopyCommand = new RelayCommand(() => previewCopy(this));
        ApplyCopyCommand = new AsyncRelayCommand(() => applyCopy(this));
        Refresh();
    }

    /// <summary>Gets the descriptor key.</summary>
    public string Key => resolved.Descriptor.Key;

    /// <summary>Gets the category.</summary>
    public string Category => resolved.Descriptor.Category;

    /// <summary>Gets the title.</summary>
    public string Title => resolved.Descriptor.Title;

    /// <summary>Gets the description.</summary>
    public string Description => resolved.Descriptor.Description;

    /// <summary>Gets the required expert warning.</summary>
    public string ExpertWarning => resolved.Descriptor.ExpertWarning;

    /// <summary>Gets the number of supported fields without materializing editor rows.</summary>
    public int SupportedFieldCount => resolved.Fields.Count;

    /// <summary>Gets whether copying between axes is supported.</summary>
    public bool SupportsAxisCopy => resolved.Descriptor.SupportsAxisCopy && Axes.Count > 1;

    /// <summary>Gets the present axes.</summary>
    public IReadOnlyList<string> Axes
    {
        get;
    }

    /// <summary>Gets or sets the source copy axis.</summary>
    [ObservableProperty]
    public partial string? SelectedSourceAxis
    {
        get;
        set;
    }

    /// <summary>Gets or sets the target copy axis.</summary>
    [ObservableProperty]
    public partial string? SelectedTargetAxis
    {
        get;
        set;
    }

    /// <summary>Gets whether editor rows have been materialized.</summary>
    [ObservableProperty]
    public partial bool IsExpanded
    {
        get;
        private set;
    }

    /// <summary>Gets whether this group contains pending changes.</summary>
    [ObservableProperty]
    public partial bool IsModified
    {
        get;
        private set;
    }

    /// <summary>Gets the current coupled validation message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    public partial string? ValidationMessage
    {
        get;
        set;
    }

    /// <summary>Gets whether group validation currently fails.</summary>
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    /// <summary>Gets the currently visible, lazily materialized field rows.</summary>
    public ObservableRangeCollection<AdvancedTuningFieldViewModel> Fields
    {
        get;
    } = [];

    /// <summary>Gets the current explicit axis-copy preview rows.</summary>
    public ObservableRangeCollection<AxisCopyChangeViewModel> CopyPreview
    {
        get;
    } = [];

    /// <summary>Gets whether an axis-copy preview awaits explicit application.</summary>
    [ObservableProperty]
    public partial bool HasCopyPreview
    {
        get;
        private set;
    }

    /// <summary>Gets the expand/collapse command.</summary>
    public IRelayCommand ToggleExpandedCommand
    {
        get;
    }

    /// <summary>Gets the confirmed group apply command.</summary>
    public IAsyncRelayCommand ApplyCommand
    {
        get;
    }

    /// <summary>Gets the group revert command.</summary>
    public IRelayCommand RevertCommand
    {
        get;
    }

    /// <summary>Gets the group refresh command.</summary>
    public IAsyncRelayCommand RefreshCommand
    {
        get;
    }

    /// <summary>Gets the non-mutating axis-copy preview command.</summary>
    public IRelayCommand PreviewCopyCommand
    {
        get;
    }

    /// <summary>Gets the command that applies a reviewed preview to pending values.</summary>
    public IAsyncRelayCommand ApplyCopyCommand
    {
        get;
    }

    internal AxisCopyPreview? PendingCopyPreview
    {
        get;
        private set;
    }

    /// <summary>Determines whether the descriptor or one of its fields matches a search.</summary>
    /// <param name="search">The search text.</param>
    /// <returns><see langword="true"/> when the group should be shown.</returns>
    public bool Matches(string search)
    {
        return string.IsNullOrWhiteSpace(search)
            ? true
            : Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
              Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
              Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
              resolved.Fields.Any(item =>
                  item.ParameterName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                  item.Definition.Component.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Applies a field filter and optionally expands matching rows.</summary>
    /// <param name="search">The search text.</param>
    /// <param name="expand">Whether a matching group should expand.</param>
    public void SetFilter(string search, bool expand)
    {
        filter = search;
        if (expand)
        {
            EnsureMaterialized();
            IsExpanded = true;
        }

        RefreshVisibleFields();
    }

    /// <summary>Refreshes materialized fields, comparison values, and dirty state.</summary>
    public void Refresh()
    {
        foreach (var item in materialized)
        {
            item.Refresh(session);
        }

        IsModified = resolved.Fields.Any(item => session.GetField(item.ParameterName)?.IsModified == true);
        var comparison = service.CompareAxes(
                new ExtendedTuningWorkspace(
                    new ExtendedTuningProfile(session.Scope.FirmwareIdentity.Family, [resolved.Descriptor]),
                    session,
                    [resolved]),
                Key)
            .ToDictionary(item => item.ParameterName, StringComparer.Ordinal);
        foreach (var item in materialized)
        {
            item.NormalizedMagnitude = comparison.GetValueOrDefault(item.ParameterName)?.NormalizedMagnitude ?? 0;
        }
    }

    internal void SetCopyPreview(AxisCopyPreview preview)
    {
        PendingCopyPreview = preview;
        var copyPreview = new List<AxisCopyChangeViewModel>();

        foreach (var change in preview.Changes)
        {
            copyPreview.Add(new AxisCopyChangeViewModel(
                change.Component,
                change.SourceParameter,
                change.TargetParameter,
                change.TargetValue,
                change.SourceValue));
        }

        CopyPreview.ReplaceRange(copyPreview);

        HasCopyPreview = CopyPreview.Count > 0;
    }

    internal void ClearCopyPreview()
    {
        PendingCopyPreview = null;
        CopyPreview.Clear();
        HasCopyPreview = false;
    }

    internal IReadOnlyList<string> ParameterNames => resolved.Fields.Select(item => item.ParameterName).ToArray();

    private void ToggleExpanded()
    {
        if (!IsExpanded)
        {
            EnsureMaterialized();
        }

        IsExpanded = !IsExpanded;
        RefreshVisibleFields();
    }

    private void EnsureMaterialized()
    {
        if (materialized.Count != 0)
        {
            return;
        }

        materialized.AddRange(resolved.Fields.Select(item => new AdvancedTuningFieldViewModel(item, session)));
        Refresh();
    }

    private void RefreshVisibleFields()
    {
        if (!IsExpanded)
        {
            Fields.Clear();
            return;
        }

        var selected = string.IsNullOrWhiteSpace(filter)
            ? materialized
            : materialized.Where(item =>
                item.ParameterName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.AxisText.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();


        var fields = new List<AdvancedTuningFieldViewModel>();
        foreach (var item in selected)
        {
            fields.Add(item);
        }

        Fields.ReplaceRange(fields);
    }
}

